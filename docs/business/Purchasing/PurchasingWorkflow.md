# Purchasing Workflow — Procure-to-Pay (end-to-end)

> Luồng nghiệp vụ thực tế dựa trên code hiện có, tổng hợp từ
> `PurchaseOrder.md`/`Supplier.md` và các consumer ở service khác. Đây là bản
> chi tiết hoá sơ đồ "Luồng Vận Hành" đã có ở
> `docs/business/ERP-Business-Analysis.md` (mục Purchase Order Service).

## Sơ đồ luồng

```
Nhân viên mua hàng
   │  CreatePurchaseOrderCommand
   ▼
PurchaseOrder (Draft) ──AddItem/RemoveItem──> (vẫn Draft)
   │  Submit()  [phải có >=1 item]
   ▼
Submitted
   │  Approve(approvedBy)                     ApprovedEvent ──> (chưa có consumer nào)
   ▼
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

1. **`PurchaseOrderApprovedDomainEvent` không có consumer** — Warehouse hiện
   chỉ hành động khi hàng *thực sự* về (`PurchaseOrderReceivedDomainEvent`),
   không chuẩn bị trước dock/nhân lực ngay khi PO được duyệt như mô tả tổng
   quan ở `event-catalog.md`.
2. **`ReceiptScheduledDomainEvent` không có consumer** — lịch nhận hàng đã đặt
   (`ScheduleReceipt`) không kích hoạt bất kỳ hành động nào ở phía Warehouse.
3. **Three-way match là thao tác đồng bộ, không lưu trạng thái** — mỗi lần gọi
   là một phép so sánh tức thời; không có bản ghi lịch sử "đơn này đã đối
   chiếu, kết quả X" để tra cứu lại sau.
4. **Không có luồng Reject riêng** (xem `PurchaseOrder.md` mục 6.1) — ảnh
   hưởng tới bước Submitted→Approved: người duyệt không có cách nào từ chối
   có lý do, đưa PO về Draft để sửa, chỉ có thể Cancel (kết thúc vĩnh viễn).

## Đề xuất backlog (không thuộc phạm vi triển khai code đợt này)

- Consumer ở WarehouseService cho `PurchaseOrderApprovedDomainEvent` (chuẩn bị
  vị trí nhận hàng) và `ReceiptScheduledDomainEvent` (lên lịch nhân lực/dock).
- Consumer ở AccountingService cho `PurchaseOrderReceivedDomainEvent` để tự
  động đối chiếu ba chiều khi hoá đơn nhà cung cấp về, thay vì chờ gọi API thủ
  công.
- Phê duyệt theo ngưỡng số tiền / nhiều cấp, kiểm tra ngân sách trước Approve.
