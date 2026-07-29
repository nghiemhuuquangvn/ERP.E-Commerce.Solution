# Module 13 — Shipping
## Technical Design Specification | ERP.E-Commerce.Solution

> Số module (13) được gán mới trong đợt rà soát 2026-07-29 — ShippingService trước đó chưa có tài liệu BA riêng. Viết theo hướng **reconciliation** hoàn toàn.

---

## 1. Mục tiêu và phạm vi

ShippingService (Port 5010, Code: SHP, database `ShippingDB`) quản lý vòng đời vận chuyển: tạo Shipment từ đơn hàng → đặt vận đơn với hãng vận chuyển thật (GHN) → theo dõi trạng thái → xác nhận giao hàng (kèm bằng chứng giao hàng — POD) → xử lý trả hàng (Return/RMA) như một luồng logistics ngược độc lập.

Phạm vi **không bao gồm**: tính phí ship hiển thị cho khách ở giỏ hàng (thuộc Sales/Storefront, dùng `TenantShippingSettings.DefaultShippingFee`/`FreeShippingThreshold` — hiện các setting này **chưa được nối vào logic chọn hãng/tính phí lúc runtime**, chỉ mới có CRUD).

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Warehouse/Kho vận | Xác nhận đã lấy hàng (PickedUp), cập nhật tracking |
| Hãng vận chuyển (GHN) | Nhận lệnh tạo đơn, trả về mã vận đơn thật, cập nhật trạng thái |
| Khách hàng | Nhận hàng (ký nhận/ảnh chụp), khởi tạo yêu cầu trả hàng |
| OrderFulfillmentSaga (Sales) | Ra lệnh tạo Shipment sau khi đơn được duyệt |

## 3. Quy trình nghiệp vụ chính

1. Saga của Sales phát lệnh `sales_order.CreateShipmentCommand` → `CreateShipmentCommandHandler` tải thông tin `Carrier`, gọi `Shipment.Create(...)` (trạng thái `Pending`) → **gọi thật `ICarrierClient.CreateOrderAsync`** đặt vận đơn với GHN → nếu thành công, gán mã vận đơn thật trước khi lưu.
2. Kho xác nhận lấy hàng → `MarkAsPickedUp()` (`Pending`/`ReadyForPickup` → `PickedUp`).
3. Mỗi lần cập nhật trạng thái (`MarkInTransit`, `MarkOutForDelivery`, ...) đều tự động ghi một `TrackingEvent` — **lịch sử tracking chính là nhật ký trạng thái được hệ thống ép buộc ghi, không phải log rời rạc tự nguyện.**
4. Giao hàng thành công → `MarkAsDelivered(location, receivedByName?, signatureUrl?, photoUrl?)` — ghi nhận **POD (Proof of Delivery)**: tên người nhận + ảnh chữ ký/ảnh chụp (tuỳ chọn, không bắt buộc).
5. Giao thất bại → `MarkDeliveryFailed()` (tăng `DeliveryAttempts`) → có thể `RetryDelivery()` để quay lại `OutForDelivery`.
6. Trả hàng (Return) là một **aggregate độc lập hoàn toàn** với Shipment gốc — vì đây là một chặng vận chuyển vật lý ngược chiều (khách → kho) có mã vận đơn/hãng vận chuyển riêng, không tái sử dụng state machine của Shipment xuôi chiều: `InitiateReturnCommand` (yêu cầu Shipment gốc phải tồn tại) → `MarkReturnPickedUp` → `MarkReturnReceived`.

## 4. Domain Model (Aggregate Roots — DDD, đã xác nhận đúng code thật)

```
Shipment (Aggregate Root)
 ├─ Status: Pending → PickedUp/ReadyForPickup → InTransit → OutForDelivery → Delivered
 │                                                        └→ DeliveryFailed → (retry)
 │          (Cancelled: chặn nếu đã Delivered/Returned; "Returned"(6) là giá trị enum
 │           tồn tại nhưng KHÔNG method nào của Shipment từng gán — return là aggregate riêng)
 ├─ Items (owned collection): ProductId, Quantity, Weight
 └─ TrackingEvents (owned collection, tự động ghi ở mọi transition)

Carrier (Aggregate Root) — BaseRatePerKg, InterProvinceSurcharge, TrackingUrlTemplate
 └─ CalculateRate()/EstimateDeliveryDate() — mô hình đơn giản nội tỉnh/liên tỉnh
    (2 ngày nội tỉnh, 4 ngày liên tỉnh — cố định, không phải tính khoảng cách thật)

ReturnShipment (Aggregate Root, ĐỘC LẬP với Shipment)
 └─ Status: Requested → PickedUp → Received | Cancelled
```

## 5. Database Design (`ShippingDB`, đã đối chiếu `src/Database/Shipping/001-003*.sql`)

| # | Bảng | Cột chính |
|---|---|---|
| 1 | `Carriers` | Id, Name, Code (unique), BaseRatePerKg, InterProvinceSurcharge, OriginProvince, TrackingUrlTemplate |
| 2 | `Shipments` | Id, OrderId, CarrierId, TrackingNumber, Status, AddressWard/District/Province (đổi tên từ City/State ở migration 002), DeliveryAttempts, ReceivedByName, SignatureUrl, PhotoUrl |
| 3 | `ShipmentItems` | Id, ShipmentId (FK), ProductId, Quantity, Weight |
| 4 | `ShipmentTrackingEvents` | Id, ShipmentId (FK), Status (**đổi kiểu từ INT sang NVARCHAR ở migration 002** — xem ghi chú) |
| 5 | `ReturnShipments` | Id, OriginalShipmentId, OrderId, Reason, TrackingNumber, Status |
| 6 | `TenantShippingSettings` / `SystemShippingSettings` | DefaultShippingFee, FreeShippingThreshold, CustomCarriersAllowed |
| 7 | `OutboxMessages` | chuẩn Outbox pattern |

**Ghi chú migration quan trọng — lỗi kiểu dữ liệu đã được vá**: `001_CreateShippingDB.sql` khai báo `ShipmentTrackingEvents.Status` kiểu `INT`, nhưng entity C# `TrackingEvent.Status` **luôn là `string`** — nếu migration 002 không sửa lại (đổi cột tạm rồi swap kiểu `NVARCHAR(50)`), dòng ghi tracking-event **đầu tiên** sau khi deploy sẽ lỗi ngay lập tức. Migration 002's header comment tự thừa nhận điều này.

**Địa chỉ theo chuẩn Việt Nam**: migration 002 đổi `AddressCity`→`AddressDistrict`, `AddressState`→`AddressProvince`, thêm `AddressWard` — khớp mô hình Phường/Xã–Quận/Huyện–Tỉnh/Thành phố mà GHN yêu cầu, không phải mô hình City/State kiểu US ban đầu.

## 6. API Design (REST)

```
POST   /api/v1/shipments                              (shipping:create)
GET    /api/v1/shipments/{id}, /{id}/track, /order/{orderId}   (shipping:view)
POST   /api/v1/shipments/{id}/tracking, /tracking-number, /deliver, /delivery-failed, /retry-delivery   (shipping:update)
POST   /api/v1/shipments/bulk-tracking                 (shipping:update)
GET    /api/v1/shipments/{id}/label                    (shipping:view — trả về PDF nhãn vận chuyển 100×150mm thật)

GET/POST  /api/v1/shipments/{shipmentId}/returns, /{returnId}   (shipping:view / :update)
POST   .../returns/{returnId}/tracking-number, /picked-up, /received, /cancel

GET    /api/v1/carriers                                (shipping:view)
POST   /api/v1/carriers/calculate-fee                  (shipping:view — gọi API tính phí thật của GHN)
GET    /api/v1/carriers/{id}/quote                      (shipping:view — dùng công thức nội bộ đơn giản, KHÔNG phải GHN)
```

**Ghi chú quan trọng**: hiện có **2 cơ chế tính phí song song, chưa hợp nhất** — `POST /carriers/calculate-fee` gọi API giá thật của GHN, còn `GET /carriers/{id}/quote` dùng công thức nội tỉnh/liên tỉnh đơn giản trong domain model. Cũng chưa có action tạo/sửa/xoá Carrier dù quyền `shipping:carriers:manage` đã tồn tại — dành cho tính năng tương lai.

## 7. Event Design

**Published** (Outbox → Kafka topic `shipping-topic`, một topic chung, không tách theo từng loại sự kiện):
- `ShipmentCreatedEvent`, `TrackingUpdatedEvent` (phát ở MỌI lần đổi trạng thái, vì mọi transition đều gọi `AddTrackingEvent`), `DeliveryConfirmedEvent`, `ShipmentFailedEvent`
- `ReturnInitiatedEvent`, `ReturnReceivedEvent`

**Consumed** (đã xác nhận, sửa lại phần CLAUDE.md ghi lỗi thời):
| Nguồn | Transport | Sự kiện thật | Ghi chú |
|---|---|---|---|
| Sales | RabbitMQ `sales_order_events` | `sales_order.CreateShipmentCommand` | **Không còn** subscribe trực tiếp `SalesOrderApprovedEvent` như CLAUDE.md ghi — đã đổi sang lệnh do saga phát, từ 2026-07-14, để tránh tạo Shipment trùng lặp. Địa chỉ người nhận vẫn là placeholder tạm (`"(pending address sync)"`) vì sự kiện saga chưa mang dữ liệu địa chỉ thật. |
| Warehouse | RabbitMQ `warehouse_exchange` | `warehouse.InventoryReservedEvent` | Tìm Shipment theo OrderId, gọi `MarkReadyForPickup()` nếu còn `Pending` |

## 8. Business Rules & Validation

- Tích hợp hãng vận chuyển là **thật**, không giả lập: `GhnCarrierClient` gọi API GHN thật (tạo đơn/tra cứu/tính phí); có `NullCarrierClient` dự phòng khi thiếu cấu hình token (môi trường dev).
- Mọi transition trạng thái Shipment đều tự động ghi `TrackingEvent` — không cần thao tác thủ công riêng để có audit trail.
- Return **luôn đòi hỏi Shipment gốc đã tồn tại**, độc lập vòng đời với Shipment gốc — trạng thái `Returned`(6) trên Shipment không bao giờ thực sự được set bởi bất kỳ code nào.
- POD (ảnh/chữ ký người nhận) là **tuỳ chọn**, không bắt buộc để xác nhận giao hàng thành công.

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 5 (Sales) | Nhận lệnh `sales_order.CreateShipmentCommand` từ saga |
| Module 7 (Warehouse) | Nhận `InventoryReservedEvent` → chuyển Shipment sang sẵn sàng lấy hàng |
| GHN (bên ngoài) | Đặt đơn, tra cứu vận đơn, tính phí thật |

## 10. So sánh & Reconciliation với project hiện tại (2026-07-29)

Phát hiện chính:
1. **Tên sự kiện tiêu thụ đã lỗi thời trong CLAUDE.md** — không còn subscribe trực tiếp `SalesOrderApprovedEvent`, đã chuyển sang lệnh saga.
2. **Lỗi kiểu dữ liệu đã được vá đúng lúc**: `ShipmentTrackingEvents.Status` từ `INT`→`NVARCHAR` — nếu migration 002 không chạy, dòng tracking đầu tiên sẽ lỗi.
3. **2 cơ chế tính phí song song chưa hợp nhất** (GHN thật vs. công thức nội bộ đơn giản) — cần quyết định giữ 1 hay tách rõ mục đích sử dụng từng cái.
4. **Địa chỉ người nhận vẫn là placeholder** trong luồng tạo Shipment tự động từ saga — cần Sales bổ sung dữ liệu địa chỉ thật vào lệnh `CreateShipmentCommand`.
5. `TenantShippingSettings`/`SystemShippingSettings` mới chỉ CRUD, chưa nối vào logic chọn hãng/tính phí lúc runtime thật.
