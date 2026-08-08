# Sales Order — Nghiệp vụ

> Mô tả hành vi thực tế trong code của `src/services/SaleOrderService` (Port
> 5002, Code: SO), không phải hành vi lý tưởng mô tả trong README. Khoảng
> trống được liệt kê ở mục cuối.

## 1. Aggregate `SalesOrder`

`ERP.SO.Domain/Entities/SalesOrder.cs` — **không có aggregate Customer
riêng**: `CustomerId/CustomerName/CustomerEmail` chỉ là snapshot 3 field copy
vào order lúc tạo, không tham chiếu tới một hồ sơ khách hàng đầy đủ (xem
`Customers.md`). Các field khác: `TotalAmount` (decimal, tự tính lại qua
`RecalculateTotal`), `ShippingAddress`, `Notes`, các mốc thời gian
`ApprovedAt/ShippedAt/DeliveredAt/CancelledAt/ReturnRequestedAt/ReturnedAt`,
`TrackingNumber/Carrier`, `CancellationReason/ReturnReason/RefundAmount`,
`HasPriceChangeWarning` (cờ do `ProductPriceChangedConsumer` set).

Con sưu tập trên aggregate: `LineItems` (`OrderLineItem` — có
`ShippedQuantity`/`RemainingQuantity` để hỗ trợ giao hàng từng phần),
`InternalNotes` (`OrderNote`), `StatusHistory` (`OrderStatusHistoryEntry` — ghi
lại mọi lần đổi trạng thái kèm lý do/người thực hiện, dùng cho audit trail).

**Không có** trên entity này: hạn mức tín dụng (credit limit) dù README mô tả
có; trạng thái thanh toán (payment status) dù README liệt kê
`PaymentReceivedEvent` là consumed event.

## 2. State machine (`OrderStatus`)

```
Draft --Submit--> Submitted --Approve--> Approved --MarkAsPicked--> Picked
                       │
                       └--Reject(reason)--> Rejected

Picked --ShipLineItems (đủ số lượng còn lại)--> Shipped --MarkAsDelivered--> Delivered
Picked --ShipLineItems (một phần)--> PartiallyShipped --ShipLineItems (phần còn lại)--> Shipped

Delivered --RequestReturn(reason)--> ReturnRequested --CompleteReturn(refundAmount)--> Returned

Draft/Submitted/Approved/Picked/PartiallyShipped --Cancel(reason)--> Cancelled
(Không thể Cancel khi đã Shipped/Delivered/Cancelled/ReturnRequested/Returned)
```

Ghi chú quan trọng:
- **`Approve()` chỉ kiểm tra `Status == Submitted`, tự nó không kiểm tra tồn
  kho** — nhưng từ 2026-07-14, `ApproveOrderCommandHandler` gọi thêm
  `OrderFulfillmentSaga.HandleOrderApprovedAsync()` trong cùng tiến trình:
  saga sẽ chỉ yêu cầu tạo shipment sau khi **cả hai** điều kiện đã đúng — đơn
  đã Approved **và** Warehouse đã xác nhận giữ đủ hàng (`InventoryReservedEvent`)
  — xem `SalesWorkflow.md` mục Saga.
- **`Ship()`** (giao toàn bộ) chỉ là tiện ích gọi `ShipLineItems` với toàn bộ
  `RemainingQuantity` mỗi dòng — không phải luồng riêng.
- **`ShipLineItems()`**: sau khi cập nhật `ShippedQuantity` từng dòng, nếu
  *mọi* dòng đã hết `RemainingQuantity` → `Shipped` (phát
  `SalesOrderShippedEvent`), nếu còn dòng chưa xong → `PartiallyShipped` (phát
  `SalesOrderPartiallyShippedEvent`).
- **`UpdateOrder()`** (đổi địa chỉ giao/ghi chú) chỉ cho phép ở `Draft`/`Submitted`.
- **`FlagPriceChanged()`**: khi `ProductPriceChangedConsumer` (Kafka
  `product-topic`) báo giá sản phẩm đổi, nếu đơn chưa `Shipped/PartiallyShipped/
  Delivered/Cancelled/Returned` và còn dòng chứa sản phẩm đó → set
  `HasPriceChangeWarning = true` + ghi `InternalNote`. Chỉ là cảnh báo, **không
  tự động đổi giá đã chốt trên đơn**.
- **`CompleteReturn(refundAmount)`**: chỉ ghi nhận số tiền hoàn và phát
  `SalesOrderReturnedEvent`; **không tự xác nhận** Payment/Accounting đã hoàn
  tiền/đảo bút toán thành công — hoàn toàn phụ thuộc consumer phía nhận sự
  kiện xử lý đúng.

## 3. API Endpoints (`SalesOrdersController`)

| Method | Route | Ghi chú |
|---|---|---|
| POST | `/api/sales-orders` | Create — validate coupon qua `IPromotionServiceClient` (fail-open nếu PromotionService lỗi) |
| GET | `/my` | Đơn của khách hàng hiện tại |
| GET | `/{id}` | Chi tiết đơn |
| GET | `/` | Danh sách (phân trang) |
| GET | `/customer/{customerId}` | Đơn theo khách hàng |
| PUT | `/{id}` | `UpdateOrder` — chỉ Draft/Submitted |
| POST | `/{id}/notes` | Thêm ghi chú nội bộ |
| POST | `/{id}/ship-items` | Giao hàng từng phần (`ShipLineItems`) |
| POST | `/{id}/return` | `RequestReturn` — chỉ từ Delivered |
| POST | `/{id}/return/complete` | `CompleteReturn` — chỉ từ ReturnRequested |
| POST | `/{id}/submit` | Draft → Submitted |
| POST | `/{id}/approve` | Submitted → Approved (không kiểm tra tồn kho) |
| POST | `/{id}/ship` | Giao toàn bộ (tiện ích của ship-items) |
| POST | `/{id}/cancel` | Huỷ đơn (chặn nếu đã Shipped/Delivered/Cancelled/ReturnRequested/Returned) |
| POST | `/{id}/mark-delivered` | Shipped → Delivered |
| POST | `/{id}/reject` | Submitted → Rejected (có lý do — **PO không có luồng tương đương**, xem Purchasing) |
| POST | `/{id}/pick` | Approved → Picked |

## 4. Domain Events

Phát qua RabbitMQ exchange `sales_order_events` (không phải Outbox/Kafka như
PO): `SalesOrderCreatedEvent`, `SalesOrderApprovedEvent`,
`SalesOrderCancelledEvent`, `SalesOrderShippedEvent`,
`SalesOrderPartiallyShippedEvent`, `SalesOrderDeliveredEvent`,
`SalesOrderReturnedEvent`. Chi tiết ai tiêu thụ event nào ở `SalesWorkflow.md`.

## 4b. Phát hiện đơn hàng bất thường (Anomaly Detection, thêm 2026-08-04)

Rule-based, không dùng ML — chi tiết đầy đủ ở
`docs/fixes/2026-08-04_ERP4Features_SeedDataAndFeatures_Summary.md` §D.2.

**Quy tắc**: 1 đơn bị đánh dấu bất thường nếu `TotalAmount` lớn hơn **3 lần**
giá trị đơn trung bình của khách hàng đó (`ThresholdMultiplier`), **và** khách
hàng đã có **tối thiểu 3 đơn trước đó** (`MinHistoryCount`) để có đủ dữ liệu
lịch sử làm baseline — khách hàng mới (dưới 3 đơn) không bao giờ bị đánh dấu,
tránh false-positive cho đơn đầu tiên lớn của khách mới.

```csharp
// ERP.SO.Application/Handlers/GetOrderQueryHandler.cs
public static class OrderAnomalyDetector
{
    private const int MinHistoryCount = 3;
    private const decimal ThresholdMultiplier = 3.0m;

    public static (bool IsAnomalous, string? Reason) Evaluate(
        Guid orderId, decimal orderTotal, IReadOnlyCollection<SalesOrder> customerOrders)
    { /* ... */ }
}
```

- Được tính cho cả chi tiết đơn (`GetOrderQueryHandler`) lẫn danh sách đơn
  (`ListOrdersQueryHandler` — tính 1 lần cho mỗi khách hàng khác nhau trong
  trang kết quả, không tính lại theo từng dòng, để tránh N+1 query).
- Lộ ra ngoài API qua `isAnomalous`/`anomalyReason` trên cả `SalesOrderDto` và
  `SalesOrderSummaryDto` — không phải field lưu trong DB, tính lại mỗi lần đọc.
- Admin hiển thị badge đỏ "⚠ Unusual order" cạnh số đơn hàng ở cả trang danh
  sách và chi tiết, kèm lý do đầy đủ ở tooltip.
- **Ngưỡng 3× là hằng số cố định trong code**, chưa cấu hình theo tenant/danh
  mục sản phẩm; tính năng cũng chỉ áp dụng cho Sales Order, chưa mở rộng sang
  Purchase Order hay giao dịch POS.

## 5. Khoảng trống & Đề xuất

1. ~~**Không kiểm tra tồn kho trước khi Approve**~~ — **Đã bổ sung 2026-07-14**
   qua `OrderFulfillmentSaga` (xem mục 2 ngay dưới và `SalesWorkflow.md`):
   shipment chỉ được tạo sau khi cả Approve lẫn Inventory Reserved đều đã xảy
   ra, thay vì tạo ngay khi Approve như cách hiểu cũ.
2. ~~**`OrderFulfillmentSaga` là dead code**~~ — **Đã gỡ bỏ 2026-07-10, sau đó
   nối dây thật lại 2026-07-14** theo yêu cầu nghiệp vụ (cần đảm bảo tính nhất
   quán dữ liệu giữa Order/Payment/Inventory/Shipment). Khi gỡ ban đầu, saga
   là dead code hoàn toàn: chưa từng đăng ký DI, `OrderFulfillmentSagaState`
   chưa được cấu hình trong `SalesOrderDbContext` (mọi lần gọi
   `_dbContext.Set<OrderFulfillmentSagaState>()` sẽ ném lỗi), và không
   handler/consumer nào gọi tới các phương thức `Handle*Async` của nó. Lần nối
   dây thứ hai đã sửa toàn bộ những vấn đề đó cộng thêm một số bug khác phát
   hiện khi rà soát kỹ — xem chi tiết đầy đủ ở `SalesWorkflow.md` mục Saga.
3. **Không có hạn mức tín dụng (credit limit)** dù README mô tả — không có
   field/kiểm tra nào trên `SalesOrder` hoặc CustomerId liên quan.
4. **Không có trạng thái thanh toán trên đơn** — README liệt kê
   `PaymentReceivedEvent` là consumed event nhưng không có consumer/field nào
   thực sự lưu trạng thái thanh toán vào `SalesOrder`.
5. **Không có back-order / nhận hàng đa kho** — `RemainingQuantity` chỉ hỗ trợ
   giao từng phần theo số lượng có sẵn, không có logic dự kiến ngày có hàng
   hoặc lấy hàng từ nhiều kho.
6. **Chỉ có 1 coupon phẳng lúc tạo đơn** — không có chiết khấu theo bậc số
   lượng, khuyến mãi theo dòng hàng, hay áp dụng lại khuyến mãi sau khi sửa đơn.
