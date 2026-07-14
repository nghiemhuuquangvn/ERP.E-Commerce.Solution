# Sales Workflow — Order-to-Cash (end-to-end)

> Luồng nghiệp vụ thực tế dựa trên code hiện có. Bản chi tiết hoá sơ đồ "Luồng
> Vận Hành" đã có ở `docs/business/ERP-Business-Analysis.md` (mục Sale Order
> Service).
>
> **Cập nhật 2026-07-14**: `OrderFulfillmentSaga` (từng bị gỡ bỏ 2026-07-10 vì
> là dead code) đã được khôi phục và nối dây thật theo yêu cầu nghiệp vụ — giờ
> đây saga là nguồn điều phối thật cho 3 bước Payment → Inventory → Shipment,
> thay thế phần choreography tương ứng ở 3 service đó. Mục "Sơ đồ luồng" và
> "Saga" bên dưới đã cập nhật theo hành vi mới; các consumer khác (Accounting,
> CRM, Promotion, Notification, Reporting, và các nhánh Warehouse xử lý
> Cancelled/Shipped) không thay đổi.

## Sơ đồ luồng

```
Khách hàng / nhân viên bán hàng
   │  CreateOrderCommand (validate coupon qua PromotionServiceClient, fail-open nếu Promotion lỗi)
   ▼
SalesOrder (Draft) ──AddLineItem/RemoveLineItem/UpdateOrder── (vẫn Draft)
   │  Submit()
   ▼
Submitted ─┬─Approve()───────────────────────────┐
           └─Reject(reason)──> Rejected           │  (trong tiến trình, cùng transaction)
                                                    ▼
                                     saga.HandleOrderApprovedAsync(orderId)
                                     ── ghi nhận Approved=true, chỉ tạo shipment
                                        nếu Inventory cũng đã Reserved (xem mục Saga)

Ngay khi CreateOrderCommand thành công (cùng tiến trình):
   saga.HandleOrderCreatedAsync(orderId, tenantId, totalAmount)
   ── khởi tạo OrderFulfillmentSagaState, publish InitiatePaymentCommand
      (RabbitMQ "sales_order_events", routing key "sales_order.InitiatePaymentCommand")
      ──> PaymentService khởi tạo Payment (idempotent theo OrderId)
      ──(khi gateway callback báo Paid)──> PaymentProcessedEvent (Kafka "payment-topic")
      ──> saga.HandlePaymentProcessedAsync
          ── publish ReserveInventoryCommand kèm line items
             (RabbitMQ "sales_order_events", routing key "sales_order.ReserveInventoryCommand")
             ──> WarehouseService kiểm tra & giữ hàng (2 pha: check hết rồi mới reserve)
                 ── đủ hàng ──> InventoryReservedEvent (RabbitMQ "warehouse_exchange")
                     ──> saga.HandleInventoryReservedAsync
                         ── nếu Approved đã true ──> publish CreateShipmentCommand
                         ── nếu chưa Approved ──> chỉ ghi nhận, chờ Approve
                 ── thiếu hàng ──> InventoryInsufficientEvent (RabbitMQ "warehouse_exchange")
                     ──> saga.HandleInventoryInsufficientAsync
                         ── refund (nếu đã có Payment) + Cancel đơn (in-process)

Khi CẢ HAI (Approved=true VÀ InventoryReserved=true) đều đúng, bất kể thứ tự:
   saga publish CreateShipmentCommand
   (RabbitMQ "sales_order_events", routing key "sales_order.CreateShipmentCommand")
   ──> ShippingService tạo Shipment (địa chỉ/người nhận vẫn là placeholder — xem Gap)
       ──> ShipmentCreatedEvent (Kafka "shipping-topic")
           ──> saga.HandleShipmentCreatedAsync ── Saga hoàn tất (Status=Completed)

Nếu Payment thất bại (PaymentFailedEvent, Kafka "payment-topic"):
   saga.HandlePaymentFailedAsync ── Cancel đơn (in-process), không refund (chưa từng thanh toán)

────────────────────────── Các bước KHÔNG do saga điều phối (không đổi) ──────────────────────────

Approved ──MarkAsPicked()──> Picked
Picked ──ShipLineItems(một phần)──> PartiallyShipped ──ShipLineItems(còn lại)──> Shipped
Picked ──ShipLineItems(toàn bộ)/Ship()──> Shipped
Shipped ──MarkAsDelivered()──> Delivered
Delivered ──RequestReturn(reason)──> ReturnRequested ──CompleteReturn(refund)──> Returned
Draft/Submitted/Approved/Picked/PartiallyShipped ──Cancel(reason)──> Cancelled

SalesOrderCreatedEvent (không đổi, vẫn phát song song với việc gọi saga)
   ──> AccountingService (SalesOrderRabbitMqConsumer — ghi nhận doanh thu/hoá đơn)
   ──> CRMService        (SalesOrderCreatedEventHandler — liên kết Contact/Lead)
SalesOrderCancelledEvent  ──> WarehouseService (SalesOrderCancelledEventHandler — trả tồn kho đã giữ)
SalesOrderShippedEvent    ──> WarehouseService (SalesOrderShippedEventHandler)
SalesOrderPartiallyShippedEvent, SalesOrderDeliveredEvent, SalesOrderReturnedEvent
   ──> Chưa xác nhận có consumer nào ở service khác

Song song, độc lập với luồng đơn hàng:
ProductPriceChangedEvent (Kafka "product-topic") ──> ProductPriceChangedConsumer
   ──> SalesOrder.FlagPriceChanged() cho mọi đơn còn mở chứa sản phẩm đó (chỉ cảnh báo, không đổi giá)
```

## Saga — `OrderFulfillmentSaga`

**Lịch sử**: tồn tại từ trước nhưng hoàn toàn là dead code (chưa đăng ký DI,
state entity chưa cấu hình EF Core, không gì gọi tới các `Handle*Async`) —
bị gỡ bỏ 2026-07-10. Sau đó được khôi phục và nối dây thật 2026-07-14 theo
yêu cầu nghiệp vụ: cần đảm bảo tính nhất quán dữ liệu (data consistency) giữa
Order/Payment/Inventory/Shipment, không chỉ dựa vào choreography độc lập từng
service.

**Các bug đã sửa khi nối dây** (không chỉ đơn thuần "bật lại"):
1. **Không lưu trạng thái**: trước đây không gọi `SaveChangesAsync` sau bất kỳ
   thay đổi nào trên saga state — mọi transition bị âm thầm mất. Nay đi qua
   `ISalesOrderUnitOfWork.Sagas` + `SaveChangesAsync` sau mỗi bước.
2. **Không có idempotency guard**: một message bị gửi lại (Kafka
   `EnableAutoCommit`, RabbitMQ nack-requeue) sẽ khiến saga publish lại bước kế
   tiếp lần nữa. Nay mỗi `Handle*Async` kiểm tra `CurrentStep` trước khi xử lý
   — message gửi lại là no-op.
3. **`ReserveInventoryCommand` của saga thiếu line items** — WarehouseService
   cần danh sách sản phẩm/số lượng để biết giữ gì; saga nay tự lấy đơn qua
   `ISalesOrderRepository` ở bước này.
4. **`RefundPaymentCommand` của saga không có `Amount`** — PaymentService cần
   số tiền hoàn. Thay vì thêm field, consumer bên PaymentService tự tra
   `Payment` theo `PaymentId` và hoàn toàn bộ `payment.Amount`.
5. **`CancelOrderCommand` không còn round-trip qua message bus** — vì saga
   sống trong cùng SaleOrderService với lệnh Cancel thật
   (`ERP.SO.Application.Commands.CancelOrderCommand`), nay gọi thẳng qua
   `IMediator.Send` trong cùng tiến trình thay vì tự publish rồi tự nghe lại.
6. **WarehouseService: sửa 2 bug liền khi thêm `InventoryInsufficientEvent`**:
   (a) trước đây thiếu hàng thì ném exception, khiến RabbitMQ nack-requeue vô
   hạn (tình trạng tồn kho không đổi nên không bao giờ retry thành công) — nay
   publish `InventoryInsufficientEvent` thay vì ném lỗi; (b) trước đây giữ
   hàng từng dòng một, nếu dòng sau thiếu hàng thì các dòng trước đã giữ không
   được hoàn tác — nay kiểm tra đủ hàng cho **toàn bộ** các dòng trước, chỉ
   giữ hàng khi tất cả đều đủ.

**Cổng phê duyệt (Approval gate) được giữ nguyên** — quyết định rõ ràng của
người dùng khi thiết kế lại: saga **không** tự động tạo shipment ngay khi giữ
xong tồn kho. `OrderFulfillmentSagaState` có 2 cờ độc lập `Approved` và
`InventoryReserved` (con người có thể duyệt đơn trước, trong, hoặc sau khi
Warehouse xác nhận giữ hàng xong) — `CreateShipmentCommand` chỉ được phát khi
**cả hai** đều `true`, bất kể thứ tự xảy ra trước.

**Điều phối lai (hybrid) có chủ đích**: saga chỉ thay thế 3 bước Payment/
Inventory/Shipment. Accounting, CRM, Promotion, Notification, Reporting, và
2 nhánh Warehouse xử lý `SalesOrderCancelledEvent`/`SalesOrderShippedEvent`
tiếp tục hoạt động theo choreography độc lập như trước — không đổi.

## Đề xuất backlog / rủi ro còn lại (không thuộc phạm vi triển khai đợt này)

- **Không có cơ chế rollout theo giai đoạn** (không có feature flag trong hệ
  thống) — việc bật saga đòi hỏi deploy đồng thời cả 4 service (SO, Payment,
  Warehouse, Shipping) cùng lúc; đơn hàng tạo ra đúng lúc các service redeploy
  lệch nhau có thể bị "kẹt" một bước nếu binding routing key cũ mất trước khi
  binding mới của service kế tiếp kịp lên.
- **Payment ở bước "chờ xử lý" (`PaymentPending`) có thể kéo dài không xác
  định** — vì `PaymentProcessedEvent` chỉ phát khi có gateway callback thật
  (khách hàng hoàn tất thanh toán), không phải ngay sau `InitiatePaymentCommand`.
  `SagaRecoveryHostedService` (job định kỳ 5 phút, dùng
  `GetStuckSagasAsync` vốn có sẵn nhưng chưa từng được gọi) cố tình loại trừ
  bước này khỏi cảnh báo "stuck" để tránh báo động giả.
- Địa chỉ/người nhận trên `CreateShipmentCommand` vẫn là placeholder — SO
  chưa truyền địa chỉ giao hàng thật qua sự kiện/command nào cho Shipping.
- Message Kafka không được partition theo `OrderId` (key ngẫu nhiên) — rủi ro
  thấp với saga hiện tại (mỗi service chỉ phát tối đa 1 sự kiện kết thúc mỗi
  đơn) nhưng cần lưu ý nếu mở rộng sau này.
- Consumer cho `SalesOrderPartiallyShippedEvent`/`SalesOrderDeliveredEvent`/
  `SalesOrderReturnedEvent` ở các service liên quan vẫn chưa có.
- Hạn mức tín dụng khách hàng trước khi Approve — vẫn chưa có.
