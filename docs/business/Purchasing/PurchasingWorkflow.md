> **2026-10-26 (P1-8):** the supplier invoice is now stored and matched with tolerances, and only an approved invoice becomes a bill (not the order approval); returns to vendor exist. This flow is described in `ProcureToPay.md`.

# Purchasing Workflow — Procure-to-Pay (end-to-end)

> Luồng nghiệp vụ thực tế dựa trên code hiện có, tổng hợp từ
> `PurchaseOrder.md`/`Supplier.md` và các consumer ở service khác. Đây là bản
> chi tiết hoá sơ đồ "Luồng Vận Hành" đã có ở
> `docs/business/ERP-Business-Analysis.md` (mục Purchase Order Service).
>
> **Cập nhật 2026-08-04**: bước `Approve()` giờ rẽ nhánh theo giá trị đơn hàng
> — duyệt đa cấp Quản lý → Tài chính cho đơn lớn. Xem mục "Duyệt đa cấp" bên
> dưới. Chi tiết đầy đủ (kèm các bug đã sửa khi xây dựng) ở
> `docs/fixes/2026-08-04_ERP4Features_SeedDataAndFeatures_Summary.md` §C.1.

## Sơ đồ luồng

```
Nhân viên mua hàng
   │  CreatePurchaseOrderCommand
   ▼
PurchaseOrder (Draft) ──AddItem/RemoveItem──> (vẫn Draft)
   │  Submit()  [phải có >=1 item]
   ▼
Submitted ─┬─Reject(reason)──> Rejected  [từ 2026-08-04: khả dụng cả từ
           │                              PendingFinanceApproval, không chỉ Submitted]
           │
           └─Approve(approvedBy)
                │
                ├─ TotalAmount < FinanceApprovalThreshold (5000, hardcoded)
                │  ──> Approved trực tiếp (như trước 2026-08-04)
                │
                └─ TotalAmount >= FinanceApprovalThreshold
                   ──> PendingFinanceApproval (ghi ManagerApprovedBy/At)
                       PendingFinanceApprovalEvent ──Kafka "purchasing-topic"──>
                         NotificationService (báo người có quyền
                         purchase_order:finance_approve)
                       │  ApproveFinance(approvedBy)  [POST .../finance-approve,
                       │  quyền purchase_order:finance_approve]
                       │  (ghi FinanceApprovedBy/At)
                       ▼
                     Approved
                        │
                        ▼
         ApprovedEvent ──Kafka "purchasing-topic"──> AccountingService,
                          NotificationService (PurchasingTopic, từ 2026-08-04 —
                          trước đó publish nhưng không ai tiêu thụ)
                        │
Approved ───────────────────────────────────────────────────────────────────┐
   │  ScheduleReceipt(date)                                                 │
   ▼                                     ReceiptScheduledEvent              │
Approved (ScheduledReceiptDate set)  ──> (chưa có consumer nào)             │
   │  ReceiveGoods(receivedQuantities)  — Goods Receipt Note (GRN)          │
   ▼                                                                        │
PartiallyReceived / Received  ──PurchaseOrderReceivedDomainEvent──> WarehouseService
                                        (PurchaseOrderReceivedConsumer,        │
                                         cộng tồn kho theo ProductCode/SKU)    │
                                                                               │
   Cancel(cancelledBy) khả dụng từ mọi trạng thái ở trên  <────────────────────┘
   (trừ Received/Cancelled) ──PurchaseOrderCancelledDomainEvent──> (chưa xác nhận consumer)

Song song, khi PO được TẠO (không phải Approve):
PurchaseOrderCreatedDomainEvent ──Outbox──> Kafka "purchasing-topic"
                                                  │
                                                  ▼
                                         AccountingService (AccountingKafkaConsumer)
                                         ghi nhận công nợ phải trả (AP liability)

Đối chiếu (không qua event, gọi trực tiếp qua API, do Accounting hoặc người dùng chủ động gọi):
ThreeWayMatchQuery(id, invoiceLines) ──so sánh──> PO (ordered) vs GRN (received) vs Invoice (caller cung cấp)
  Kết quả mỗi dòng: Matched | QuantityMismatch | PriceMismatch | NotOnPurchaseOrder | NotYetReceived
```

## Các điểm nghẽn / thiếu liên kết đã xác nhận

1. **`PurchaseOrderApprovedDomainEvent` chưa có consumer ở Warehouse** —
   Warehouse hiện chỉ hành động khi hàng *thực sự* về
   (`PurchaseOrderReceivedDomainEvent`), không chuẩn bị trước dock/nhân lực
   ngay khi PO được duyệt như mô tả tổng quan ở `event-catalog.md`.
   *(Cập nhật 2026-08-04: NotificationService giờ đã tiêu thụ event này —
   trước đó hoàn toàn không ai tiêu thụ, kể cả Accounting dù được ghi trong
   catalog là subscriber từ trước; Warehouse vẫn chưa có consumer.)*
2. **`ReceiptScheduledDomainEvent` không có consumer** — lịch nhận hàng đã đặt
   (`ScheduleReceipt`) không kích hoạt bất kỳ hành động nào ở phía Warehouse.
3. **Three-way match là thao tác đồng bộ, không lưu trạng thái** — mỗi lần gọi
   là một phép so sánh tức thời; không có bản ghi lịch sử "đơn này đã đối
   chiếu, kết quả X" để tra cứu lại sau.
4. ~~**Không có luồng Reject riêng**~~ — **đã có từ trước** qua
   `Reject(reason)` (xem `PurchaseOrder.md` mục 6.1); từ 2026-08-04 mở rộng
   thêm để khả dụng cả khi PO đang `PendingFinanceApproval`, không chỉ
   `Submitted`.
5. **Ngưỡng duyệt tài chính (`FinanceApprovalThreshold = 5000`) là hằng số
   hardcode trên aggregate** (từ 2026-08-04) — chưa cấu hình được theo tenant
   hay theo danh mục hàng hoá.

## Đề xuất backlog (không thuộc phạm vi triển khai code đợt này)

- Consumer ở WarehouseService cho `PurchaseOrderApprovedDomainEvent` (chuẩn bị
  vị trí nhận hàng) và `ReceiptScheduledDomainEvent` (lên lịch nhân lực/dock).
- Consumer ở AccountingService cho `PurchaseOrderReceivedDomainEvent` để tự
  động đối chiếu ba chiều khi hoá đơn nhà cung cấp về, thay vì chờ gọi API thủ
  công.
- Ngưỡng duyệt tài chính cấu hình được theo tenant/danh mục thay vì hardcode;
  kiểm tra ngân sách trước khi cho Approve.
