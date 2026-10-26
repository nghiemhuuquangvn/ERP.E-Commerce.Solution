> **2026-10-26 (P1-9):** purchase requests are now replenishment proposals that create draft orders / RFQs — see `docs/business/Warehouse/Replenishment.md`.
> **2026-10-26 (P1-8):** the gaps "no real RFQ" and the stateless three-way match are closed; the bill is no longer created at order approval. See `ProcureToPay.md`.

# Purchase Order — Nghiệp vụ

> Tài liệu này mô tả hành vi **thực tế trong code** của
> `src/services/PurchaseOrderService` (Port 5003, Code: PO), không phải hành vi
> lý tưởng. Các khoảng trống so với một quy trình mua hàng ERP đầy đủ được liệt
> kê ở mục cuối.

## 1. Mục đích

Quản lý vòng đời đơn mua hàng (Purchase Order) từ khi tạo nháp đến khi nhận đủ
hàng, bao gồm: lập đơn, gửi duyệt, phê duyệt, lên lịch nhận hàng, ghi nhận
phiếu nhập (GRN), so sánh giá theo lịch sử, và đối chiếu ba chiều (three-way
match) với hoá đơn.

## 2. Aggregate & Entity

### `PurchaseOrder` (aggregate root)
`src/services/PurchaseOrderService/src/ERP.PO.Domain/Entities/PurchaseOrder.cs`

| Field | Kiểu | Ghi chú |
|---|---|---|
| `PONumber` | string | Bắt buộc, set khi tạo |
| `SupplierId` | Guid | Bắt buộc |
| `Status` | `PurchaseOrderStatus` | Xem state machine bên dưới |
| `OrderDate` | DateTime | = `UtcNow` lúc tạo |
| `ExpectedDeliveryDate` | DateTime? | Phải ở tương lai nếu có |
| `TotalAmount` | `Money` | Tự tính lại (`RecalculateTotal`) mỗi khi thêm/xoá item |
| `ScheduledReceiptDate` | DateTime? | Set bởi `ScheduleReceipt` |
| `Items` | `IReadOnlyCollection<PurchaseOrderItem>` | Danh sách dòng hàng |

### `PurchaseOrderItem`
`ERP.PO.Domain/Entities/PurchaseOrderItem.cs` — `ProductCode/ProductName`,
`Quantity`, `Unit`, `UnitPrice` (Money), `Status` (`PurchaseOrderItemStatus`:
Pending/PartiallyReceived/Received), `ReceivedQuantity` (lũy kế),
`RemainingQuantity` (tính = `Quantity - ReceivedQuantity`).

## 3. State machine (`PurchaseOrderStatus`)

```
Draft --Submit--> Submitted --Approve--> Approved --(ScheduleReceipt)--> Approved/Ordered/PartiallyReceived
                                              |
                                              +--ReceiveGoods--> PartiallyReceived --ReceiveGoods--> Received
Draft/Submitted/Approved/Ordered/PartiallyReceived --Cancel--> Cancelled
```

- **Draft → Submitted** (`Submit()`): chỉ hợp lệ từ `Draft`, và đơn phải có ít
  nhất 1 item, nếu không ném `InvalidOperationException`.
- **Submitted → Approved** (`Approve(approvedBy)`): chỉ hợp lệ từ `Submitted`.
  Phát `PurchaseOrderApprovedDomainEvent`.
- **→ Cancelled** (`Cancel(cancelledBy)`): hợp lệ ở mọi trạng thái **trừ**
  `Received` và `Cancelled`. **Không có state "Rejected" riêng** — từ chối một
  đơn Submitted hiện phải dùng chung `Cancel`, mất thông tin lý do từ chối và
  không phân biệt được với việc huỷ đơn thông thường (xem mục Gap).
- **ScheduleReceipt(date)**: hợp lệ từ `Approved`, `Ordered`, hoặc
  `PartiallyReceived`. Chỉ ghi ngày dự kiến nhận hàng, không đổi `Status`. Phát
  `ReceiptScheduledDomainEvent`.
- **ReceiveGoods(receivedQuantities)** — Goods Receipt Note (GRN): hợp lệ từ
  `Approved`, `Ordered`, hoặc `PartiallyReceived`. Với mỗi dòng, truyền
  `(QuantityAccepted, QuantityRejected)` — gọi
  `PurchaseOrderItem.ReceiveQuantity(quantityAccepted, quantityRejected)` (ném
  lỗi nếu tổng hai số vượt `RemainingQuantity`). **`RemainingQuantity` chỉ tính
  theo số lượng đã chấp nhận** (`Quantity - ReceivedQuantity`, trong đó
  `ReceivedQuantity` là luỹ kế accepted) — số lượng bị từ chối không tính là đã
  hoàn thành dòng hàng, nhà cung cấp vẫn còn nợ phần đó. Sau khi cập nhật, nếu
  **tất cả** item có `RemainingQuantity == 0` → `Status = Received`, ngược lại
  → `PartiallyReceived`. Phát `PurchaseOrderReceivedDomainEvent` (gồm danh sách
  `ReceivedLine{ProductCode, QuantityReceived (= accepted), QuantityRejected}`
  — thêm 2026-07-10). WarehouseService hiện chỉ đọc `QuantityReceived`
  (accepted) để cộng tồn kho — `QuantityRejected` chưa có consumer nào tiêu
  thụ, chỉ mang tính ghi nhận/audit ở bước này.

> Lưu ý lịch sử: trước 2026-07-03, `Approve`/`ReceiveGoods` tồn tại trên entity
> nhưng **không có Command Handler nào gọi tới** — toàn bộ luồng phê duyệt và
> nhận hàng thực chất là dead code cho đến khi được nối lại.

## 4. API Endpoints (`PurchaseOrdersController`, quyền `purchase_order:*`)

| Method | Route | Quyền | Command/Query |
|---|---|---|---|
| POST | `/api/purchase-orders` | `Create` | `CreatePurchaseOrderCommand` |
| GET | `/api/purchase-orders/{id}` | `Read` | `GetPurchaseOrderByIdQuery` |
| GET | `/api/purchase-orders` | `Read` | `GetPurchaseOrdersQuery` (lọc theo supplier/status, phân trang) |
| POST | `/{id}/submit` | `Update` | `SubmitPurchaseOrderCommand` |
| POST | `/{id}/approve` | `Approve` | `ApprovePurchaseOrderCommand` |
| POST | `/{id}/reject` | `Approve` | `RejectPurchaseOrderCommand` — distinct from Cancel, requires a reason, only valid from `Submitted` (added 2026-07-10) |
| POST | `/{id}/cancel` | `Cancel` | `CancelPurchaseOrderCommand` |
| POST | `/{id}/items` | `AddItem` | `AddItemCommand` |
| DELETE | `/{id}/items/{itemId}` | `RemoveItem` | `RemoveItemCommand` |
| POST | `/{id}/receive` | `Receive` | `ReceiveGoodsCommand` (GRN) |
| POST | `/{id}/schedule-receipt` | `Update` | `ScheduleReceiptCommand` |
| GET | `/compare-prices?productCode=` | `Read` | `ComparePricesForProductQuery` — so giá theo lịch sử đơn hàng, **không phải RFQ thật** |
| POST | `/{id}/three-way-match` | `Read` | `ThreeWayMatchQuery` — so PO vs GRN (đã nhận) vs hoá đơn (caller tự truyền dòng hoá đơn, không lưu trữ) |

**Không phải gap** (đã kiểm tra kỹ, tránh hiểu lầm): `SubmitPurchaseOrder`,
`ApprovePurchaseOrder`, `CancelPurchaseOrder`, `RejectPurchaseOrder` trong
controller truyền `"system"` làm giá trị `*By` xuống Command, nhưng mỗi
Handler tương ứng (`ApprovePurchaseOrderCommandHandler`,
`CancelPurchaseOrderCommandHandler`, `RejectPurchaseOrderCommandHandler`) đều
tự phát hiện chuỗi `"system"` và thay bằng
`_userContextService.GetUserId()` — audit trail vẫn ghi đúng người thực hiện.

## 5. Domain Events (Outbox → Kafka `purchasing-topic`)

| Event | Khi nào | Ai tiêu thụ hiện tại |
|---|---|---|
| `PurchaseOrderCreatedDomainEvent` | `Create()` | AccountingService (`AccountingKafkaConsumer`) — ghi nhận công nợ phải trả (AP) |
| `PurchaseOrderApprovedDomainEvent` | `Approve()` | **Chưa có consumer nào** |
| `ReceiptScheduledDomainEvent` | `ScheduleReceipt()` | **Chưa có consumer nào** |
| `PurchaseOrderReceivedDomainEvent` | `ReceiveGoods()` | WarehouseService (`PurchaseOrderReceivedConsumer`) — cộng tồn kho theo `ProductCode` khớp SKU |
| `PurchaseOrderCancelledDomainEvent` | `Cancel()` | Chưa xác nhận có consumer |

## 6. Khoảng trống & Đề xuất

1. ~~**Không có state/luồng Reject riêng**~~ — **Đã bổ sung 2026-07-10**:
   `PurchaseOrder.Reject(reason, rejectedBy)`, trạng thái `Rejected` mới, chỉ
   hợp lệ từ `Submitted`, endpoint `POST /{id}/reject` (quyền
   `purchase_order:approve`, cùng quyền với Approve — giống mẫu
   `sales_order:approve` gate cả Approve/Reject bên SaleOrderService).
2. ~~**GRN không phân biệt số lượng chấp nhận/bị từ chối**~~ — **Đã bổ sung
   2026-07-10**: `ReceiveGoodsCommand` nay nhận `QuantityAccepted`/
   `QuantityRejected` theo từng dòng; `RemainingQuantity` chỉ tính theo phần
   accepted.
3. **`PurchaseOrderApprovedDomainEvent` và `ReceiptScheduledDomainEvent` không
   có consumer nào** — Warehouse chỉ phản ứng khi hàng thực sự về
   (`PurchaseOrderReceivedDomainEvent`), không chuẩn bị dock/nhân lực trước dựa
   trên việc PO được duyệt hoặc lịch nhận hàng đã đặt.
4. **Không có duyệt theo ngưỡng số tiền / chuỗi phê duyệt nhiều cấp** — chỉ có
   một hành động Approve phẳng, không phân biệt PO nhỏ/lớn, không có
   delegated approver.
5. **Không kiểm tra ngân sách/hạn mức chi tiêu** theo cost center hoặc kỳ tài
   chính trước khi Approve.
6. **Không hỗ trợ đa tiền tệ thực sự** — `Money.Zero` mặc định USD
   (`PurchaseOrderConstants.DefaultCurrency`), không có tỷ giá quy đổi.
7. **Audit trail của Submit/Approve/Cancel dùng "system" cố định** thay vì
   người dùng thật (mục 4 ở trên).
8. **Không có RFQ (Request for Quotation) thật** — chỉ so sánh giá lịch sử đơn
   hàng cũ (`ComparePricesForProductQuery`), không phải quy trình mời/nhận báo
   giá từ nhiều nhà cung cấp.
