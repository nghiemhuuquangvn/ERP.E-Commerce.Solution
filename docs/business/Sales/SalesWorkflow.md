# Sales Workflow — Order-to-Cash (end-to-end)

> Luồng nghiệp vụ thực tế dựa trên code hiện có. Bản chi tiết hoá sơ đồ "Luồng
> Vận Hành" đã có ở `docs/business/ERP-Business-Analysis.md` (mục Sale Order
> Service).

## Sơ đồ luồng

```
Khách hàng / nhân viên bán hàng
   │  CreateOrderCommand (validate coupon qua PromotionServiceClient, fail-open nếu Promotion lỗi)
   ▼
SalesOrder (Draft) ──AddLineItem/RemoveLineItem/UpdateOrder── (vẫn Draft)
   │  Submit()
   ▼                                    SalesOrderCreatedEvent ──RabbitMQ "sales_order_events"──┐
Submitted ─┬─Approve()──────────────────────────────────────────────────────────────────────┐   │
           └─Reject(reason)──> Rejected (kết thúc, có lý do — khác PO chỉ có Cancel)         │   │
                                                                                              │   │
Approved ──MarkAsPicked()──> Picked                    SalesOrderApprovedEvent               │   │
   (KHÔNG kiểm tra tồn kho trước khi Approve) ─────────────────────────────────┐              │   │
                                                                                │              │   │
Picked ──ShipLineItems(một phần)──> PartiallyShipped ──ShipLineItems(còn lại)──┤              │   │
Picked ──ShipLineItems(toàn bộ)/Ship()─────────────────────────────────────────┴──> Shipped   │   │
                                                          SalesOrderShippedEvent /             │   │
                                                          SalesOrderPartiallyShippedEvent       │   │
Shipped ──MarkAsDelivered()──> Delivered                SalesOrderDeliveredEvent               │   │
Delivered ──RequestReturn(reason)──> ReturnRequested ──CompleteReturn(refund)──> Returned       │   │
                                                          SalesOrderReturnedEvent               │   │
Draft/Submitted/Approved/Picked/PartiallyShipped ──Cancel(reason)──> Cancelled                 │   │
                                                          SalesOrderCancelledEvent              │   │
                                                                                                 ▼   ▼
                                                          ┌──────────────────────────────────────────┐
                                                          │        Consumer theo TỪNG service         │
                                                          ├──────────────────────────────────────────┤
   SalesOrderCreatedEvent    ──> WarehouseService  (SalesOrderCreatedEventHandler — reserve inventory)
                              ──> PaymentService   (SalesOrderRabbitMqConsumer  — tự khởi tạo thanh toán)
                              ──> AccountingService(SalesOrderRabbitMqConsumer  — ghi nhận doanh thu/hoá đơn)
                              ──> CRMService       (SalesOrderCreatedEventHandler— liên kết Contact/Lead)
   SalesOrderApprovedEvent    ──> ShippingService  (SalesOrderApprovedEventHandler — tạo shipment nháp)
   SalesOrderCancelledEvent   ──> WarehouseService (SalesOrderCancelledEventHandler — trả tồn kho đã giữ)
   SalesOrderShippedEvent     ──> WarehouseService (SalesOrderShippedEventHandler)
   SalesOrderPartiallyShippedEvent, SalesOrderDeliveredEvent, SalesOrderReturnedEvent
                              ──> Chưa xác nhận có consumer nào ở service khác
                                                          └──────────────────────────────────────────┘

Song song, độc lập với luồng đơn hàng:
ProductPriceChangedEvent (Kafka "product-topic") ──> ProductPriceChangedConsumer
   ──> SalesOrder.FlagPriceChanged() cho mọi đơn còn mở chứa sản phẩm đó (chỉ cảnh báo, không đổi giá)
```

## Ghi chú kiến trúc quan trọng

- **Đây là choreography (biên đạo phi tập trung)**: mỗi service tự đăng ký
  consumer và tự quyết định phản ứng khi thấy sự kiện của SO — không có một
  "nhạc trưởng" điều phối tuần tự các bước.
- **`OrderFulfillmentSaga`** đã được **gỡ bỏ khỏi codebase 2026-07-10** (cùng
  `OrderFulfillmentSagaState`, `IOrderFulfillmentSagaRepository`,
  `OrderFulfillmentSagaRepository`, và DI registration trong `Program.cs`).
  Trước đó nó được viết theo mô hình **orchestration** khác hẳn (tự định nghĩa
  `InitiatePaymentCommand`/`ReserveInventoryCommand`/`CreateShipmentCommand`
  riêng để chỉ đạo từng bước) nhưng chưa bao giờ được đăng ký trong DI, không
  có gì gọi tới các phương thức của nó, và `OrderFulfillmentSagaState` còn
  chưa từng được cấu hình trong `SalesOrderDbContext`. Nối nó vào hoạt động sẽ
  gây trùng lặp xử lý với các consumer choreography hiện có (khởi tạo thanh
  toán 2 lần, giữ tồn kho 2 lần...) thay vì bổ khuyết compensation thật, nên
  quyết định là gỡ bỏ thay vì nối dây.
- **Approve không đồng bộ với việc giữ tồn kho** — `Approve()` chỉ đổi trạng
  thái, việc Warehouse thực sự giữ được hàng hay không hoàn toàn phụ thuộc vào
  việc consumer `SalesOrderCreatedEventHandler` (chạy async, độc lập) có xử lý
  kịp và thành công hay không.

## Đề xuất backlog (không thuộc phạm vi triển khai code đợt này)

- Kiểm tra tồn kho đồng bộ (gọi API WarehouseService) trước khi cho phép
  `Approve()` — cần cân nhắc kỹ vì phá vỡ mẫu choreography thuần async hiện
  tại.
- Consumer cho `SalesOrderPartiallyShippedEvent`/`SalesOrderDeliveredEvent`/
  `SalesOrderReturnedEvent` ở các service liên quan (đặc biệt Payment/
  Accounting cho việc hoàn tiền khi Returned).
- Hạn mức tín dụng khách hàng trước khi Approve.
