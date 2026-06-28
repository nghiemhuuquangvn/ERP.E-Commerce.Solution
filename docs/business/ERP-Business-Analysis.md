# ERP E-Commerce Solution — Phân Tích Business Logic Toàn Diện

> **Cập nhật:** 2026-06-28  
> **Phạm vi:** 14 microservices + 4 frontend layers  
> **Mục đích:** Kiểm kê tính năng đã có, tính năng còn thiếu, và luồng vận hành toàn hệ thống

---

## Mục Lục

1. [Tổng Quan Kiến Trúc](#1-tổng-quan-kiến-trúc)
2. [Đánh Giá Mức Độ Hoàn Thiện](#2-đánh-giá-mức-độ-hoàn-thiện)
3. [Phân Tích Từng Nghiệp Vụ](#3-phân-tích-từng-nghiệp-vụ)
   - [3.1 Identity Service](#31-identity-service--xác-thực--phân-quyền)
   - [3.2 Product Service](#32-product-service--quản-lý-sản-phẩm)
   - [3.3 Sale Order Service](#33-sale-order-service--đơn-hàng-bán)
   - [3.4 Purchase Order Service](#34-purchase-order-service--đơn-hàng-mua)
   - [3.5 Warehouse Service](#35-warehouse-service--kho-hàng)
   - [3.6 Payment Service](#36-payment-service--thanh-toán)
   - [3.7 Shipping Service](#37-shipping-service--vận-chuyển)
   - [3.8 POS Service](#38-pos-service--bán-lẻ-tại-quầy)
   - [3.9 Promotion Service](#39-promotion-service--khuyến-mãi)
   - [3.10 CRM Service](#310-crm-service--quản-lý-khách-hàng)
   - [3.11 HRM Service](#311-hrm-service--nhân-sự)
   - [3.12 Accounting Service](#312-accounting-service--kế-toán)
   - [3.13 Notification Service](#313-notification-service--thông-báo)
   - [3.14 Reporting Service](#314-reporting-service--báo-cáo--phân-tích)
4. [Luồng Business End-to-End](#4-luồng-business-end-to-end)
   - [4.1 E-Commerce Order Flow](#41-luồng-đặt-hàng-e-commerce)
   - [4.2 POS Retail Flow](#42-luồng-bán-lẻ-tại-quầy-pos)
   - [4.3 Procurement Flow](#43-luồng-mua-hàng-procurement)
   - [4.4 CRM to Sale Flow](#44-luồng-crm---bán-hàng)
   - [4.5 HR Payroll Flow](#45-luồng-nhân-sự--bảng-lương)
5. [Ma Trận Sự Kiện Tích Hợp](#5-ma-trận-sự-kiện-tích-hợp)
6. [Gap Analysis Tổng Hợp](#6-gap-analysis-tổng-hợp)
7. [Thứ Tự Ưu Tiên Bổ Sung](#7-thứ-tự-ưu-tiên-bổ-sung)

---

## 1. Tổng Quan Kiến Trúc

### Pattern Áp Dụng Nhất Quán

```
Domain Layer          Application Layer       Infrastructure Layer    API Layer
──────────────        ─────────────────       ────────────────────   ─────────
Entities              Commands/Queries        DbContext              Controllers
Value Objects         Handlers (MediatR)      Repositories           Middleware
Domain Events         Validators (Fluent)     MessageBus (RabbitMQ)  Program.cs
Aggregates            DTOs                    Outbox Processor
Enums                 Event Handlers          Kafka Producer/Consumer
```

### Shared Infrastructure (đã có)

| Component | File | Mục đích |
|-----------|------|---------|
| `AggregateRoot` | Shared/Core | Base class cho aggregate roots |
| `BaseEntity` | Shared/Core | Base class cho entities |
| `IDomainEvent` | Shared/Core | Interface cho domain events |
| `ValueObject` | Shared/Core | Base class cho value objects |
| `IMessageBus` | Shared/Messaging | Abstraction cho message broker |
| `OutboxMessage` | Shared/Messaging | Outbox pattern entity |
| `IntegrationEventBase` | Shared/Contracts | Base cho integration events |
| `IRepository<T>` | Shared/Infrastructure | Generic repository interface |
| `ICacheService` | Shared/Infrastructure | Redis + Memory cache |
| `PermissionConstants` | Shared/Authorization | Hằng số quyền hệ thống |
| `UserContext` | Shared/Authorization | Context user qua HTTP header |

---

## 2. Đánh Giá Mức Độ Hoàn Thiện

| Service | Domain | Application | Infrastructure | API | Event Consumer | Mức Độ |
|---------|--------|-------------|----------------|-----|----------------|--------|
| IdentityService | ✅ | ✅ | ✅ | ✅ | — | **95%** |
| SaleOrderService | ✅ | ⚠️ Thiếu nhiều command | ✅ | ⚠️ 1 controller | ✅ | **55%** |
| PurchaseOrderService | ✅ | ⚠️ Chỉ có pattern | ✅ | ⚠️ 2 controllers | ❌ | **45%** |
| WareHouseService | ✅ | ⚠️ | ✅ | ⚠️ 2 controllers | ✅ | **60%** |
| AccountingService | ✅ Entities only | ❌ Thiếu commands | ⚠️ Skeleton | ❌ 0 controllers | ❌ | **20%** |
| CRMService | ✅ Entities only | ❌ Thiếu commands | ⚠️ Skeleton | ❌ 0 controllers | ❌ | **20%** |
| HRMService | ✅ Entities only | ❌ Thiếu commands | ⚠️ Skeleton | ❌ 0 controllers | ❌ | **20%** |
| ProductService | ✅ | ⚠️ Features folder | ✅ | ✅ 5 controllers | ❌ | **65%** |
| PaymentService | ✅ | ✅ | ✅ | ✅ | ✅ | **80%** |
| ShippingService | ✅ | ✅ | ✅ | ✅ | ✅ | **80%** |
| POSService | ✅ | ✅ | ✅ | ✅ | ❌ | **75%** |
| PromotionService | ⚠️ Thiếu FlashSale | ✅ | ✅ | ✅ | ❌ | **70%** |
| NotificationService | ✅ | ✅ | ✅ | ✅ | ✅ | **85%** |
| ReportingService | ❌ Không có entity | ⚠️ Queries only | ✅ | ✅ | ❌ | **30%** |

**Trung bình toàn hệ thống: ~57%**

---

## 3. Phân Tích Từng Nghiệp Vụ

---

### 3.1 Identity Service — Xác Thực & Phân Quyền

**Mục đích:** Quản lý danh tính, xác thực JWT, phân quyền RBAC, multi-tenant.

#### ✅ Tính năng đã có

**Authentication**
- Đăng ký tài khoản (`POST /api/auth/register`)
- Đăng nhập username/password → JWT Access Token + Refresh Token
- Làm mới token (`POST /api/auth/refresh`)
- Đăng xuất (thu hồi refresh token)
- Xác nhận email qua token
- Quên mật khẩu / đặt lại mật khẩu
- Đăng nhập bên thứ ba (Google, Microsoft OAuth)

**Authorization (RBAC)**
- Quản lý Roles (CRUD)
- Gán Role cho User
- Quản lý Permissions (CRUD)
- Gán Permission trực tiếp cho User hoặc qua Role
- Middleware truyền UserContext qua HTTP header sang các service khác

**User Management**
- CRUD người dùng
- Kích hoạt / vô hiệu hóa tài khoản

**Multi-Tenant**
- Quản lý Tenants (CRUD)
- Gán User vào Tenant
- Lọc dữ liệu theo TenantId

**Published Events**
- `UserRegisteredIntegrationEvent` → Kafka

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Xác thực 2 bước (2FA/MFA) | Cao | TOTP, SMS OTP |
| Audit log đăng nhập | Cao | IP, device, thời gian, thành công/thất bại |
| Khóa tài khoản sau N lần sai mật khẩu | Cao | Brute-force protection |
| Session management | Trung | Xem & thu hồi sessions đang active |
| API Key management | Trung | Cho service-to-service calls |
| Password policy configuration | Thấp | Độ dài tối thiểu, ký tự đặc biệt |
| LDAP/Active Directory integration | Thấp | Cho doanh nghiệp lớn |
| Published: `UserUpdated`, `RoleAssigned`, `PermissionGranted` | Cao | Các service khác cần đồng bộ |

#### Luồng Vận Hành

```
Client → POST /auth/register → [validate] → User entity → PublishDomainEvent
                                                      → OutboxProcessor → Kafka
                                                      → NotificationService (welcome email)
                                                      → HRMService (if employee)
                                                      → CRMService (if customer)

Client → POST /auth/login → [verify password] → generate JWT + RefreshToken
                          → return {accessToken, refreshToken, expiresIn}

API Request → [JWT middleware] → extract claims → UserContextMiddleware
           → inject X-User-Id, X-User-Roles, X-Tenant-Id headers
           → forward to downstream service
```

---

### 3.2 Product Service — Quản Lý Sản Phẩm

**Mục đích:** Catalog sản phẩm, variants, danh mục, giá, ảnh. Upstream source — không consume events.

#### ✅ Tính năng đã có

**Product Catalog**
- CRUD sản phẩm (`ProductsController`)
- Quản lý danh mục phân cấp (`CategoriesController`)
- Quản lý variants (SKU, màu sắc, kích thước) (`VariantsController`)
- Upload & quản lý ảnh sản phẩm (`ImagesController`)
- Quản lý bảng giá (`PricingController`)

**Domain Events**
- `ProductCreatedDomainEvent` → Published khi tạo sản phẩm
- `ProductDiscontinuedDomainEvent` → Published khi ngừng kinh doanh
- `ProductPublishedDomainEvent` → Published khi đăng bán

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Tìm kiếm & lọc sản phẩm (full-text search) | Cao | Elasticsearch integration |
| Bulk import/export (Excel/CSV) | Cao | Nhập hàng nghìn sản phẩm |
| Trạng thái sản phẩm workflow | Cao | Draft → Review → Published → Discontinued |
| SEO fields (slug, meta title, meta description) | Trung | Cho storefront |
| Product bundles / combo | Trung | Bán kèm, bộ sản phẩm |
| Cross-sell / upsell relationships | Trung | "Khách hàng cũng mua..." |
| Stock quantity sync từ Warehouse | Cao | Real-time tồn kho trên storefront |
| `ProductUpdated` integration event | Cao | Thông báo SalesOrder, POS, Promotion |
| `ProductPriceChanged` integration event | Cao | Thông báo Promotion tính lại |
| Application layer: command handlers đầy đủ | Cao | Features folder chưa complete |
| Attribute system (custom attributes) | Thấp | Màu sắc, chất liệu, thương hiệu |
| Product reviews & ratings | Thấp | Khách hàng đánh giá |

#### Luồng Vận Hành

```
Admin → POST /api/products → CreateProductCommand → MediatR → Handler
     → Product.Create() → raises ProductCreatedDomainEvent
     → EF Core save → OutboxProcessor → Integration Event
     → SaleOrderService (cache sản phẩm)
     → POSService (sync local)
     → PromotionService (apply discount rules)
     → ReportingService (dimension table DimProducts)
```

---

### 3.3 Sale Order Service — Đơn Hàng Bán

**Mục đích:** Tạo và quản lý đơn hàng bán, orchestrate fulfillment qua Saga pattern.

#### ✅ Tính năng đã có

**Domain**
- `SalesOrder` aggregate với `OrderLineItem` value object
- `Address` value object (shipping/billing)
- Trạng thái: Draft, Confirmed, Approved, Shipped, Delivered, Cancelled
- Domain Events: Created, Approved, Cancelled, Shipped

**Commands/Queries**
- `CreateOrderCommand` + Handler
- `GetOrderQuery` + Handler

**Saga**
- `OrderFulfillmentSaga` — điều phối luồng: Order → Reserve Inventory → Process Payment → Create Shipment
- `OrderFulfillmentSagaState` — trạng thái saga
- `IOrderFulfillmentSagaRepository` — persistence saga state

**Infrastructure**
- `RabbitMqMessageBus` — publish integration events
- `SalesOrderRepository`

**Published Events**
- `SalesOrderCreatedIntegrationEvent`
- `SalesOrderApprovedIntegrationEvent`
- `SalesOrderShippedIntegrationEvent`
- `SalesOrderCancelledIntegrationEvent`

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `ApproveOrderCommand` | Cao | Duyệt đơn hàng (thủ công hoặc auto) |
| `CancelOrderCommand` | Cao | Hủy đơn với lý do |
| `UpdateOrderCommand` | Cao | Cập nhật địa chỉ, ghi chú trước khi xử lý |
| `ListOrdersQuery` với filter/sort/page | Cao | Xem danh sách đơn hàng |
| `GetOrdersByCustomerQuery` | Cao | Lịch sử đơn hàng của khách |
| Controller endpoints đầy đủ | Cao | Hiện chỉ có 1 controller rất hạn chế |
| Apply promotion/coupon khi tạo đơn | Cao | Gọi PromotionService |
| Order notes / internal comments | Trung | Ghi chú nội bộ cho team |
| Partial fulfillment | Trung | Xuất một phần đơn hàng |
| Order timeline / audit trail | Trung | Lịch sử thay đổi trạng thái |
| Consume `ProductPriceChanged` event | Cao | Cảnh báo giá thay đổi sau khi đặt |
| Return/Refund order flow | Cao | Khách trả hàng → hoàn tiền |
| `SalesOrderDelivered` event | Cao | Trigger loyalty points, CRM update |

#### Luồng Vận Hành

```
Customer → POST /api/orders → CreateOrderCommand
                            → validate (product exists, stock available)
                            → SalesOrder.Create() → SalesOrderCreatedEvent
                            → Saga bắt đầu: OrderFulfillmentSaga

OrderFulfillmentSaga:
  Step 1: Publish → WarehouseService.ReserveInventory
          ← InventoryReserved (thành công) → sang Step 2
          ← InventoryFailed → Cancel order

  Step 2: Publish → PaymentService.ProcessPayment
          ← PaymentProcessed → sang Step 3
          ← PaymentFailed → Release inventory → Cancel order

  Step 3: Publish → ShippingService.CreateShipment
          ← ShipmentCreated → Order status = Shipped
          → NotificationService (gửi email xác nhận)
          → AccountingService (tạo invoice)
          → CRMService (update customer history)
          → ReportingService (fact table)
```

---

### 3.4 Purchase Order Service — Đơn Hàng Mua

**Mục đích:** Quản lý quá trình mua hàng từ nhà cung cấp, từ yêu cầu mua đến nhận hàng.

#### ✅ Tính năng đã có

**Domain**
- `PurchaseOrder` aggregate
- `PurchaseOrderItem` entity
- `Supplier` entity
- `Address`, `Money` value objects
- Trạng thái: Draft, Pending, Approved, Received, Cancelled
- Domain Events: Created, Approved, Cancelled

**Infrastructure**
- Authorization patterns (4 policies)
- Caching patterns (4 cache decorators)
- `PurchasingDbContext`

**API Controllers**
- `PurchaseOrdersController` (partial)
- `SuppliersController`

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `CreatePurchaseOrderCommand` + Handler | Cao | Command chưa có handler |
| `ApprovePurchaseOrderCommand` | Cao | Workflow duyệt mua hàng |
| `ReceiveGoodsCommand` (GRN) | Cao | Nhận hàng → trigger Warehouse |
| `ListPurchaseOrdersQuery` | Cao | Xem danh sách PO |
| `GetPurchaseOrderQuery` | Cao | Chi tiết PO |
| `CancelPurchaseOrderCommand` | Trung | Hủy PO |
| Integration Event Consumers | Cao | Hiện không consume bất kỳ event nào |
| `PurchaseOrderReceived` integration event | Cao | Trigger WarehouseService nhập kho |
| Supplier evaluation / rating | Thấp | Đánh giá nhà cung cấp |
| Price comparison giữa các supplier | Trung | Báo giá nhiều nhà cung cấp |
| `ReceiptScheduled` event | Cao | Lên lịch nhận hàng |
| Three-way matching | Thấp | PO vs GRN vs Invoice |

#### Luồng Vận Hành

```
Staff → POST /api/purchase-orders → CreatePurchaseOrderCommand
     → PurchaseOrder.Create() → PurchaseOrderCreatedEvent
     → Notify AccountingService (budget check - chưa có)

Manager → POST /api/purchase-orders/{id}/approve → ApprovePurchaseOrderCommand
        → PurchaseOrder.Approve() → PurchaseOrderApprovedEvent
        → Notify Supplier (email - qua NotificationService)

Warehouse → POST /api/purchase-orders/{id}/receive → ReceiveGoodsCommand
          → PurchaseOrder.MarkReceived() → PurchaseOrderReceivedEvent
          → [THIẾU] WarehouseService.ReceiveStock()
          → AccountingService.CreateBill()
```

---

### 3.5 Warehouse Service — Kho Hàng

**Mục đích:** Quản lý tồn kho, di chuyển hàng, đặt chỗ tồn kho cho đơn hàng.

#### ✅ Tính năng đã có

**Domain (Aggregates)**
- `Inventory` aggregate (tồn kho theo sản phẩm + location)
- `Reservation` entity (giữ hàng cho đơn)
- `StockMovement` entity (lịch sử di chuyển)
- `WarehouseLocation` entity (vị trí trong kho)

**Domain Events**
- `InventoryReservedEvent`
- `InventoryUpdatedEvent`
- `StockMovementRecordedEvent`

**Integration Event Consumers**
- `SalesOrderCreatedEventHandler` → Reserve inventory
- `SalesOrderCancelledEventHandler` → Release reservation
- `SalesOrderShippedEventHandler` → Deduct actual stock
- `PurchaseOrderCreatedEventHandler` → Prepare receiving

**API Controllers**
- `InventoryController` (xem tồn kho)
- `StockMovementController` (lịch sử di chuyển)

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Nhập kho từ PO (`ReceiveStockCommand`) | Cao | Consume `PurchaseOrderReceived` event |
| Quản lý vị trí kho (Location CRUD API) | Cao | Rack, Shelf, Bin management |
| Kiểm kê vật lý (Stocktake / Physical Count) | Cao | So sánh thực tế với hệ thống |
| Transfer Order (chuyển kho) | Trung | Di chuyển hàng giữa các kho |
| Cảnh báo tồn kho tối thiểu (reorder point) | Cao | Tự động tạo PR khi stock < min |
| Lot/Batch tracking | Trung | Theo dõi lô hàng |
| Serial number tracking | Trung | Tracking thiết bị theo số serial |
| Expiry date management | Trung | Quản lý hạn sử dụng |
| Stock adjustment (điều chỉnh tồn kho) | Cao | Xử lý hàng vỡ, mất mát |
| Publish `ReceiptCompleted` event | Cao | Thông báo khi nhập kho xong |
| Publish `StockAlert` event | Trung | Cảnh báo tồn kho thấp |
| Inventory valuation (FIFO/LIFO/Average) | Thấp | Cho kế toán |

#### Luồng Vận Hành

```
[Event: SalesOrderCreated]
→ SalesOrderCreatedEventHandler
→ Inventory.Reserve(productId, qty) → validate stock >= qty
→ Reservation.Create() → InventoryReservedEvent → RabbitMQ
→ [Saga nhận InventoryReserved → tiếp tục Payment]

[Event: SalesOrderShipped]
→ SalesOrderShippedEventHandler
→ Inventory.Deduct(reservationId) → StockMovement.Record(OUT)
→ InventoryUpdatedEvent

[THIẾU - cần thêm]
[Event: PurchaseOrderReceived]
→ ReceiveStockHandler
→ Inventory.AddStock(productId, qty, locationId)
→ StockMovement.Record(IN) → InventoryUpdatedEvent
→ ProductService sync tồn kho (real-time)
```

---

### 3.6 Payment Service — Thanh Toán

**Mục đích:** Xử lý thanh toán, hoàn tiền, và quyết toán.

#### ✅ Tính năng đã có

**Domain**
- `Payment` aggregate: Id, OrderId, Amount, Currency, Method, Status, GatewayTransactionId
- `Refund` entity: Id, PaymentId, Amount, Reason, Status
- `Settlement` entity: Id, Period, TotalAmount, Status
- Trạng thái: Pending → Authorized → Captured → Failed/Cancelled/Refunded
- `PaymentEvents`: PaymentProcessed, PaymentRefunded, SettlementCreated, PaymentFailed

**Commands**
- `InitiatePaymentCommand`
- `AuthorizePaymentCommand`
- `CapturePaymentCommand`
- `RefundPaymentCommand`
- `ProcessSettlementCommand`

**Event Consumers**
- `SalesOrderCreatedEventHandler` → tự động initiate payment

**API**: `PaymentsController`

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Payment gateway integration thực | Cao | Stripe, VNPay, MoMo, ZaloPay |
| Webhook handler từ gateway | Cao | Nhận callback async từ gateway |
| `PaymentMethod` entity (saved cards) | Trung | Lưu thẻ đã dùng |
| Partial refund | Cao | Hoàn tiền một phần |
| Installment plan | Thấp | Trả góp |
| Multi-currency support | Trung | USD, VND, EUR |
| Consume `InvoiceCreated` từ Accounting | Trung | Thanh toán theo invoice |
| Payment reconciliation report | Trung | Đối soát với ngân hàng |
| Fraud detection integration | Thấp | Phát hiện giao dịch gian lận |
| `PaymentAuthorized` event | Cao | Phân biệt authorize vs capture |

#### Luồng Vận Hành

```
[Event: SalesOrderCreated]
→ SalesOrderCreatedEventHandler
→ Payment.Initiate(orderId, amount, method) → status: Pending

Customer → POST /api/payments/{id}/authorize
→ AuthorizePaymentCommand → [Gateway API call - CẦN IMPLEMENT]
→ Payment.Authorize(gatewayRef) → status: Authorized → PaymentAuthorizedEvent

→ POST /api/payments/{id}/capture
→ CapturePaymentCommand → [Gateway API call]
→ Payment.Capture() → status: Captured → PaymentProcessedEvent → RabbitMQ
→ [Saga nhận PaymentProcessed → tiếp tục Shipping]
→ AccountingService.CreateInvoice()
→ CRMService.UpdateCustomerHistory()
→ NotificationService.SendReceipt()
→ PromotionService.EarnLoyaltyPoints()

Customer → POST /api/payments/{id}/refund
→ RefundPaymentCommand → Payment.Refund() → PaymentRefundedEvent
→ AccountingService.CreateCreditNote()
→ NotificationService.SendRefundConfirmation()
```

---

### 3.7 Shipping Service — Vận Chuyển

**Mục đích:** Tạo lô giao hàng, theo dõi vận chuyển, xác nhận giao hàng.

#### ✅ Tính năng đã có

**Domain**
- `Shipment` aggregate: OrderId, CarrierId, TrackingNumber, Status
- `ShipmentItem` entity: ProductId, Quantity, Weight
- `TrackingEvent` entity: Status, Location, OccurredAt
- `Carrier` entity: Name, Code, IsActive, BaseRatePerKg
- Trạng thái: Pending → Picked → InTransit → OutForDelivery → Delivered/Failed/Returned
- `ShipmentEvents`: ShipmentCreated, TrackingUpdated, DeliveryConfirmed

**Commands**
- `CreateShipmentCommand`
- `AddTrackingEventCommand`
- `AssignCarrierCommand`
- `ConfirmDeliveryCommand`

**Event Consumers**
- `SalesOrderApprovedEventHandler` → tạo shipment

**API**: `ShipmentsController`

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Carrier API integration thực | Cao | GHN, GHTK, J&T, FedEx, DHL |
| Shipping rate calculation | Cao | Tính phí theo cân nặng/khoảng cách |
| Label printing | Trung | PDF shipping label |
| Failed delivery handling | Cao | Giao thất bại → retry/return |
| Return shipment flow | Cao | Khách trả hàng → RMA |
| Bulk tracking update | Trung | Cập nhật tracking nhiều đơn cùng lúc |
| `InventoryReserved` event consumer | Cao | Lên lịch pickup sau khi kho confirm |
| Proof of delivery (POD) | Trung | Ảnh chụp, chữ ký xác nhận |
| Estimated delivery date calculation | Trung | ETA dựa theo route |
| `ShipmentFailed` event | Cao | Giao thất bại notify các service khác |

#### Luồng Vận Hành

```
[Event: SalesOrderApproved]
→ SalesOrderApprovedEventHandler
→ CreateShipmentCommand → Shipment.Create(orderId, items)
→ [Tự động hoặc manual] AssignCarrier → [CẦN] carrier rate API
→ ShipmentCreatedEvent → NotificationService (tracking code)

Carrier webhook → POST /api/shipments/{id}/tracking
→ AddTrackingEventCommand → Shipment.AddTracking(status, location)
→ TrackingUpdatedEvent → NotificationService (SMS/email update)

→ ConfirmDeliveryCommand → Shipment.MarkDelivered()
→ DeliveryConfirmedEvent
→ SaleOrderService (update order status → Delivered)
→ PromotionService.EarnLoyaltyPoints()
→ AccountingService (finalize invoice)
→ NotificationService (delivery confirmation)
```

---

### 3.8 POS Service — Bán Lẻ Tại Quầy

**Mục đích:** Quản lý giao dịch bán lẻ tại điểm bán, ca làm việc, terminal.

#### ✅ Tính năng đã có

**Domain**
- `Transaction` aggregate: TerminalId, CashierId, Items, TotalAmount, DiscountAmount, TaxAmount
- `TransactionItem` entity: ProductId, Quantity, UnitPrice, TaxAmount
- `CashShift` entity: TerminalId, CashierId, OpeningBalance, ClosingBalance, TotalSales
- Trạng thái: Draft → Completed / Voided
- `POSEvents`: TransactionCreated, CashShiftClosed, RegisterBalanced

**Commands**
- `OpenTransactionCommand`
- `AddTransactionItemCommand`
- `ApplyDiscountCommand`
- `CompleteTransactionCommand`
- `VoidTransactionCommand`
- `OpenCashShiftCommand`
- `CloseCashShiftCommand`

**API**: `POSController`

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Terminal entity & management API | Cao | Quản lý máy POS, kích hoạt |
| Consume `ProductCreated` event | Cao | Sync sản phẩm mới xuống POS |
| Consume `PromotionCreated` event | Cao | Sync khuyến mãi xuống POS |
| Consume `PaymentProcessed` event | Cao | Xác nhận thanh toán từ gateway |
| Offline mode & sync queue | Cao | Giao dịch khi mất mạng |
| Barcode / QR scanner integration | Trung | Tìm sản phẩm nhanh |
| Cash denomination tracking | Trung | Đếm tiền từng mệnh giá |
| Receipt printing | Trung | In hóa đơn nhiệt |
| Daily Z-report | Cao | Báo cáo doanh thu cuối ngày |
| Shift summary | Cao | Tổng kết ca làm việc |
| Customer display integration | Thấp | Màn hình phụ cho khách |
| Split payment | Trung | Vừa tiền mặt vừa thẻ |
| Real-time inventory deduction | Cao | Giảm tồn kho ngay khi bán |

#### Luồng Vận Hành

```
Cashier → POST /pos/shifts/open → OpenCashShiftCommand → CashShift.Open()

Cashier → POST /pos/transactions/open → Transaction.Open(terminalId, cashierId)

Cashier → scan barcode → GET /products/{barcode}
       → POST /pos/transactions/{id}/items → AddItem() → recalculate total

Cashier → POST /pos/transactions/{id}/discount → ApplyDiscount(promotionId)
        → [CẦN] PromotionService.ValidatePromotion()

Cashier → POST /pos/transactions/{id}/complete → payment captured
        → Transaction.Complete() → TransactionCreatedEvent
        → [CẦN] WarehouseService.DeductStock()
        → AccountingService.PostRevenue()
        → PromotionService.EarnLoyaltyPoints()
        → ReportingService (fact table)

Cashier → POST /pos/shifts/close → CashShift.Close(closingBalance)
        → RegisterBalanced event → ReportingService
```

---

### 3.9 Promotion Service — Khuyến Mãi

**Mục đích:** Quản lý chương trình khuyến mãi, coupon, loyalty points, flash sale.

#### ✅ Tính năng đã có

**Domain**
- `Promotion` aggregate: Name, Type, DiscountValue, DiscountType (Percent/Fixed), StartDate, EndDate, MaxUsage
- `Coupon` entity: Code, IsUsed, UsedAt, UsedBy
- `LoyaltyAccount` entity: UserId, Points, TierLevel
- `PromotionEvents`: PromotionCreated, CouponRedeemed, LoyaltyPointsEarned, FlashSaleStarted

**Commands**
- `CreatePromotionCommand`
- `RedeemCouponCommand`
- `EarnLoyaltyPointsCommand`
- `RedeemLoyaltyPointsCommand`
- `StartFlashSaleCommand`

**API**: `PromotionsController`

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `FlashSale` entity (chưa có trong domain) | Cao | Tồn tại trong command nhưng thiếu entity |
| Consume `ProductCreated` event | Cao | Apply promotion rules cho sản phẩm mới |
| Consume `SalesOrderCreated` event | Cao | Earn loyalty sau khi đặt hàng |
| Promotion eligibility rules engine | Cao | Điều kiện áp dụng phức tạp |
| Bulk coupon code generation | Trung | Tạo hàng nghìn coupon cùng lúc |
| Promotion scheduling (auto start/end) | Trung | Background job tự động bật/tắt |
| Tier-based loyalty (Bronze/Silver/Gold) | Trung | Hạng thành viên |
| Combo promotions (Buy X Get Y) | Trung | Mua 2 tặng 1 |
| Category-level discount | Trung | Giảm giá cả danh mục |
| Promotion analytics | Thấp | Hiệu quả từng chương trình |
| `GetApplicablePromotions` API | Cao | SalesOrder/POS gọi để lấy KM áp dụng được |
| `ValidateCoupon` API | Cao | Kiểm tra coupon còn hiệu lực không |
| `GetLoyaltyBalance` API | Cao | Xem số điểm của khách |

#### Luồng Vận Hành

```
Admin → POST /promotions → CreatePromotionCommand → Promotion.Create()
     → PromotionCreatedEvent → POSService, SaleOrderService (cache rules)

Customer → POST /orders → [order creation]
         → SaleOrderService gọi PromotionService.GetApplicablePromotions(customerId, items)
         → trả về list promotions áp dụng được
         → tính giảm giá → apply vào OrderLineItem

Customer → POST /orders → nhập coupon code
         → PromotionService.ValidateCoupon(code, orderId)
         → Coupon.Use() → CouponRedeemedEvent → NotificationService

[Event: PaymentProcessed] hoặc [Event: TransactionCreated]
→ EarnLoyaltyPointsCommand → LoyaltyAccount.AddPoints()
→ LoyaltyPointsEarnedEvent → NotificationService (thông báo tích điểm)
```

---

### 3.10 CRM Service — Quản Lý Khách Hàng

**Mục đích:** Theo dõi contacts, leads, opportunities, cases hỗ trợ khách hàng.

#### ✅ Tính năng đã có

**Domain Entities (chỉ có entities, chưa có gì khác)**
- `Contact`: thông tin khách hàng/prospect
- `Lead`: khách hàng tiềm năng
- `Opportunity`: cơ hội bán hàng
- `Case`: vụ việc hỗ trợ khách hàng

#### ❌ Tính năng còn thiếu (hầu hết chưa có)

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `CreateContactCommand` + Handler | Cao | CRUD contact |
| `CreateLeadCommand` + Handler | Cao | Tạo lead mới |
| `QualifyLeadCommand` | Cao | Chuyển lead → opportunity |
| `CreateOpportunityCommand` | Cao | Pipeline bán hàng |
| `OpenCaseCommand` / `ResolveCaseCommand` | Cao | Hỗ trợ khách hàng |
| Consume `UserCreated` event | Cao | Tạo contact từ user mới |
| Consume `SalesOrderCreated` event | Cao | Link đơn hàng vào contact |
| Consume `PaymentReceived` event | Trung | Lịch sử thanh toán |
| Published events: `LeadCreated`, `LeadQualified`, `OpportunityWon`, `CaseOpened`, `CaseResolved` | Cao | Trigger notifications, reports |
| Activity logging (call, email, meeting) | Trung | Lịch sử tương tác |
| Contact tags & segments | Trung | Phân nhóm khách hàng |
| All Controllers (0 hiện có) | Cao | Chưa có bất kỳ endpoint nào |
| Email integration | Thấp | Gửi/nhận email trong CRM |
| Calendar / appointment | Thấp | Lịch hẹn |

#### Luồng Vận Hành (kế hoạch)

```
[Event: UserCreated]
→ CRM.CreateContact(userId, email, name)
→ Contact.Create() → assign to sales team (round-robin)

Sales → POST /crm/leads → CreateLeadCommand → Lead.Create()
     → assign to salesperson → notify via NotificationService

Sales → POST /crm/leads/{id}/qualify → QualifyLeadCommand
     → Lead.Qualify() → LeadQualifiedEvent
     → Opportunity.Create() → NotificationService (notify team)

[Event: SalesOrderCreated]
→ Opportunity.LinkOrder(orderId)
→ nếu opportunity.stage = Proposal → move to Won
→ OpportunityWonEvent → NotificationService, ReportingService

Customer → POST /crm/cases → OpenCaseCommand → Case.Open()
         → CaseOpenedEvent → assign to support team
→ POST /crm/cases/{id}/resolve → Case.Resolve()
→ CaseResolvedEvent → NotificationService (feedback request)
```

---

### 3.11 HRM Service — Nhân Sự

**Mục đích:** Quản lý nhân viên, phòng ban, chấm công, nghỉ phép, bảng lương.

#### ✅ Tính năng đã có

**Domain Entities (chỉ có entities)**
- `Employee`: thông tin nhân viên
- `Department`: phòng ban
- `LeaveRequest`: yêu cầu nghỉ phép
- `PaySlip`: phiếu lương

#### ❌ Tính năng còn thiếu (hầu hết chưa có)

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `HireEmployeeCommand` + Handler | Cao | Onboarding nhân viên mới |
| `TerminateEmployeeCommand` | Cao | Offboarding |
| `CreateDepartmentCommand` | Cao | Quản lý cơ cấu tổ chức |
| `SubmitLeaveRequestCommand` | Cao | Nhân viên nộp đơn xin nghỉ |
| `ApproveLeaveCommand` | Cao | Manager duyệt nghỉ phép |
| `ProcessPayrollCommand` | Cao | Tính lương hàng tháng |
| `GeneratePaySlipCommand` | Cao | Tạo phiếu lương |
| Attendance tracking | Cao | Chấm công vào/ra |
| Consume `UserCreated` event | Cao | Link user account với employee |
| Published events: `EmployeeHired`, `EmployeeTerminated`, `PaySlipGenerated`, `LeaveApproved` | Cao | Trigger notifications |
| Org chart | Trung | Sơ đồ tổ chức |
| Performance review | Thấp | Đánh giá nhân viên |
| All Controllers (0 hiện có) | Cao | Chưa có bất kỳ endpoint nào |
| Shift management | Trung | Ca làm việc |
| Overtime calculation | Trung | Tính lương OT |

#### Luồng Vận Hành (kế hoạch)

```
HR → POST /hrm/employees → HireEmployeeCommand → Employee.Hire()
   → EmployeeHiredEvent → NotificationService (welcome + onboarding email)
   → IdentityService (tạo user account tự động - CẦN)

[Event: UserCreated]
→ Employee.LinkUserAccount(userId)

Employee → POST /hrm/leave-requests → SubmitLeaveRequest
         → LeaveRequest.Submit() → notify Manager

Manager → POST /hrm/leave-requests/{id}/approve → ApproveLeave
        → LeaveRequest.Approve() → LeaveApprovedEvent
        → NotificationService (thông báo nhân viên)
        → update attendance calendar

HR → POST /hrm/payroll/process?month=2026-06
   → ProcessPayrollCommand
   → tính lương từng nhân viên (base + OT - khấu trừ)
   → GeneratePaySlip() → PaySlipGeneratedEvent
   → NotificationService (gửi phiếu lương email)
   → AccountingService (post lương vào GL)
```

---

### 3.12 Accounting Service — Kế Toán

**Mục đích:** Sổ cái tổng hợp, kế toán phải thu, phải trả, bút toán, báo cáo tài chính.

#### ✅ Tính năng đã có

**Domain Entities (chỉ có entities)**
- `Account`: tài khoản kế toán (Chart of Accounts)
- `Invoice`: hóa đơn bán hàng
- `Bill`: hóa đơn mua hàng (phải trả)
- `JournalEntry`: bút toán
- `FiscalPeriod`: kỳ kế toán

#### ❌ Tính năng còn thiếu (hầu hết chưa có)

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `CreateInvoiceCommand` | Cao | Khi PaymentProcessed |
| `ApproveBillCommand` | Cao | Khi PurchaseOrderReceived |
| `PostJournalEntryCommand` | Cao | Ghi bút toán vào GL |
| `CloseFiscalPeriodCommand` | Cao | Đóng kỳ kế toán |
| Consume `SalesOrderCreated` | Cao | Tạo hóa đơn AR |
| Consume `PurchaseOrderCreated` | Cao | Tạo bill AP |
| Consume `PaymentProcessed` | Cao | Ghi nhận tiền thu |
| Consume `WarehouseMovement` | Trung | Điều chỉnh giá trị tồn kho |
| Published: `InvoiceCreated`, `BillApproved`, `JournalEntryPosted`, `FiscalPeriodClosed` | Cao | Trigger reports |
| All Controllers (0 hiện có) | Cao | Chưa có bất kỳ endpoint nào |
| Chart of Accounts management | Cao | Danh mục tài khoản |
| AR Aging report | Cao | Công nợ phải thu |
| AP Aging report | Cao | Công nợ phải trả |
| Bank reconciliation | Trung | Đối soát ngân hàng |
| Tax management (VAT) | Cao | Khai báo thuế |
| Trial balance | Cao | Bảng cân đối thử |
| P&L Statement | Cao | Báo cáo lãi lỗ |
| Balance Sheet | Cao | Bảng cân đối kế toán |

#### Luồng Vận Hành (kế hoạch)

```
[Event: SalesOrderCreated]
→ AccountingService.CreateInvoice(orderId, amount)
→ Invoice.Create() → post bút toán: DR Phải Thu / CR Doanh Thu

[Event: PaymentProcessed]
→ AccountingService.RecordPayment(invoiceId, amount)
→ Invoice.MarkPaid() → post bút toán: DR Tiền / CR Phải Thu
→ InvoiceCreatedEvent → PaymentService (reconcile)

[Event: PurchaseOrderReceived]
→ AccountingService.CreateBill(poId, amount)
→ Bill.Create() → post bút toán: DR Hàng Tồn Kho / CR Phải Trả
→ BillApprovedEvent → ReportingService

Month End:
→ POST /accounting/fiscal-periods/{id}/close
→ FiscalPeriod.Close() → validate all journals balanced
→ generate P&L, Balance Sheet
→ FiscalPeriodClosedEvent → ReportingService
```

---

### 3.13 Notification Service — Thông Báo

**Mục đích:** Gửi thông báo đa kênh (Email, SMS, Push) dựa trên events từ toàn hệ thống.

#### ✅ Tính năng đã có

**Domain**
- `Notification` aggregate: RecipientId, Channel, TemplateId, Subject, Body, Status
- `NotificationTemplate` entity: Code, Subject, Body, Channel, Variables
- `NotificationPreference` entity: UserId, Channel, Enabled
- `NotificationLog` entity: lịch sử gửi
- `NotificationEvents`: NotificationSent, NotificationFailed

**Commands/Queries**
- `SendNotificationCommand`
- `NotificationQueries` (lịch sử)

**Event Consumers**
- `IntegrationEventHandlers` — consume events từ các service khác

**API**
- `NotificationsController`
- `PreferencesController`
- `TemplatesController`

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Email provider implementation | Cao | SendGrid / AWS SES / SMTP thực sự |
| SMS provider implementation | Cao | Twilio / VIETTEL / VNPT SMS |
| FCM/APNS push implementation | Cao | Firebase + Apple Push |
| Template variable substitution engine | Cao | Replace {{customerName}}, {{orderId}} |
| Retry mechanism (exponential backoff) | Cao | Khi gửi thất bại |
| Notification scheduling | Trung | Gửi theo lịch |
| Digest emails | Thấp | Tổng hợp nhiều thông báo vào 1 email |
| Unsubscribe management | Trung | Tuân thủ GDPR |
| Webhook delivery (outbound) | Thấp | Notify external systems |
| Bulk notification | Trung | Gửi campaign email marketing |

#### Luồng Vận Hành

```
[Bất kỳ service nào] → publish Integration Event → RabbitMQ
→ NotificationService.IntegrationEventHandlers consume
→ SendNotificationCommand(recipientId, templateCode, variables)
→ [check NotificationPreference] → nếu bị tắt thì skip
→ [load Template] → substitute variables
→ route theo Channel:
    Email → IEmailSender → [CẦN] SendGrid/SES
    SMS   → ISmsSender   → [CẦN] Twilio
    Push  → IPushSender  → [CẦN] FCM/APNS
→ Notification.MarkSent() / MarkFailed()
→ NotificationLog.Record()
→ [nếu thất bại] → retry queue (exponential backoff)
```

---

### 3.14 Reporting Service — Báo Cáo & Phân Tích

**Mục đích:** Kho dữ liệu OLAP read-only, cung cấp data cho dashboards và BI tools.

#### ✅ Tính năng đã có

**Application**
- `ReportingQueries`: GetSalesDashboard, GetInventoryReport, GetPaymentSummary, GetHRDashboard

**Infrastructure**
- Background jobs skeleton
- Message bus skeleton
- Persistence skeleton

**API**: `ReportsController`

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Kafka consumers cho TẤT CẢ events | Cao | Hiện không có event consumer nào |
| Fact tables schema & EF models | Cao | FactSalesOrders, FactPayments, FactInventory, FactPOS |
| Dimension tables schema & EF models | Cao | DimCustomers, DimProducts, DimTime, DimEmployees |
| ETL handlers (event → OLAP) | Cao | Transform và load vào fact/dim tables |
| Sales Dashboard data query | Cao | Revenue, orders, AOV theo thời gian |
| Inventory Report query | Cao | Stock levels, turnover rate |
| Payment Summary query | Cao | Revenue by payment method |
| HR Dashboard query | Cao | Headcount, attendance, leave |
| Pre-aggregated materialized views | Trung | Performance cho large dataset |
| Export to Excel/CSV | Trung | Download báo cáo |
| Real-time KPI calculation | Cao | Số liệu gần real-time |

#### Luồng Vận Hành (kế hoạch)

```
[Mọi service] → publish event → Kafka topic per service
→ ReportingService Kafka consumers (1 consumer per topic):

SalesOrderCreated → ETL → FactSalesOrders (insert)
PaymentProcessed  → ETL → FactPayments (insert)
InventoryUpdated  → ETL → FactInventoryMovements (insert)
TransactionCreated → ETL → FactPOSTransactions (insert)
EmployeeHired     → ETL → DimEmployees (upsert)
ProductCreated    → ETL → DimProducts (upsert)

Admin → GET /reports/sales?from=2026-01-01&to=2026-06-30
     → SELECT SUM(amount) FROM FactSalesOrders WHERE date BETWEEN...
     → return aggregated KPIs

BI Tool (Power BI / Tableau) → direct SQL query → ReportingDB (read-only)
```

---

## 4. Luồng Business End-to-End

### 4.1 Luồng Đặt Hàng E-Commerce

```
┌─────────────────────────────────────────────────────────────────────┐
│                    CUSTOMER JOURNEY (Storefront/Mobile)              │
└─────────────────────────────────────────────────────────────────────┘

1. BROWSE & SEARCH
   Customer → Storefront/Mobile App
   → ProductService: GET /products (filter, search)
   → PromotionService: GET /promotions/active (flash sales, banners)

2. ADD TO CART
   → LocalStorage/Zustand (client-side cart)
   → PromotionService: GET /promotions/applicable (real-time discount check)

3. CHECKOUT
   → IdentityService: Verify JWT token
   → SaleOrderService: POST /orders (CreateOrderCommand)
     ├── validate product prices (ProductService)
     ├── apply coupon (PromotionService.ValidateCoupon)
     └── → SalesOrderCreatedEvent → [SAGA bắt đầu]

4. INVENTORY RESERVATION (Saga Step 1)
   → WarehouseService: [Event: SalesOrderCreated]
   → Reserve stock → InventoryReservedEvent
   [Fail] → SalesOrderService: Cancel order

5. PAYMENT (Saga Step 2)
   → PaymentService: [Event: InventoryReserved]
   → Customer submits payment → POST /payments/{id}/capture
   → [Gateway: Stripe/VNPay] → PaymentProcessedEvent
   [Fail] → WarehouseService: Release reservation → Cancel order

6. SHIPPING (Saga Step 3)
   → ShippingService: [Event: SalesOrderApproved]
   → Shipment.Create() → [Carrier API: GHN/GHTK] → TrackingNumber
   → ShipmentCreatedEvent

7. NOTIFICATIONS (Throughout)
   → NotificationService consumes all events:
     ├── OrderConfirmation (email + push)
     ├── PaymentReceipt (email)
     ├── ShippingUpdate (SMS + push)
     └── DeliveryConfirmation (email + push)

8. POST-DELIVERY
   → PromotionService: EarnLoyaltyPoints
   → CRMService: Update customer purchase history
   → AccountingService: Finalize invoice
   → ReportingService: Update fact tables
```

---

### 4.2 Luồng Bán Lẻ Tại Quầy POS

```
┌─────────────────────────────────────────────────────────────────────┐
│                    CASHIER WORKFLOW (Staff Mobile App / POS Terminal) │
└─────────────────────────────────────────────────────────────────────┘

1. MỞ CA LÀM VIỆC
   Cashier → POST /pos/shifts/open (OpenCashShiftCommand)
   → count opening cash → CashShift.Open()
   → IdentityService: verify cashier permissions

2. BÁN HÀNG
   Cashier → scan barcode → GET /products/{barcode}
   → POST /pos/transactions/open → Transaction.Open()
   → POST /pos/transactions/{id}/items (AddItem per product)
   → apply promotion: GET /promotions/applicable
   → POST /pos/transactions/{id}/discount (ApplyDiscount)

3. THANH TOÁN
   → Customer pays (cash/card/ewallet)
   → POST /pos/transactions/{id}/complete (CompleteTransaction)
   → Transaction.Complete() → TransactionCreatedEvent
   → WarehouseService: DeductStock (real-time)
   → AccountingService: PostRevenue
   → PromotionService: EarnLoyaltyPoints
   → Print receipt

4. ĐÓNG CA
   → POST /pos/shifts/close (CloseCashShiftCommand)
   → count closing cash → CashShift.Close()
   → CashShiftClosedEvent → ReportingService (daily Z-report)
   → AccountingService: reconcile cash
```

---

### 4.3 Luồng Mua Hàng Procurement

```
┌─────────────────────────────────────────────────────────────────────┐
│                    PROCUREMENT CYCLE                                  │
└─────────────────────────────────────────────────────────────────────┘

1. PHÁT SINH NHU CẦU
   WarehouseService: [StockAlert] → stock < reorder point
   → Notify purchasing team via NotificationService

2. TẠO ĐƠN MUA
   Purchaser → POST /purchase-orders (CreatePurchaseOrderCommand)
   → select Supplier → set items, quantities, prices
   → PurchaseOrderCreatedEvent

3. DUYỆT ĐƠN MUA
   Manager → POST /purchase-orders/{id}/approve
   → PurchaseOrderApprovedEvent
   → NotificationService → email to Supplier

4. NHẬN HÀNG (GRN)
   Warehouse → POST /purchase-orders/{id}/receive
   → ReceiveGoodsCommand → PurchaseOrder.MarkReceived()
   → WarehouseService: ReceiveStock (AddStock)
   → InventoryUpdatedEvent → ProductService (sync stock)
   → AccountingService: CreateBill (phải trả nhà cung cấp)

5. THANH TOÁN NHÀ CUNG CẤP
   Accountant → POST /accounting/bills/{id}/pay
   → AccountingService: Post bút toán: DR Phải Trả / CR Tiền
   → NotificationService → receipt to Supplier
```

---

### 4.4 Luồng CRM → Bán Hàng

```
┌─────────────────────────────────────────────────────────────────────┐
│                    LEAD TO CASH CYCLE                                 │
└─────────────────────────────────────────────────────────────────────┘

1. LEAD GENERATION
   → Marketing campaign / web form / manual entry
   → CRMService: POST /crm/leads (CreateLeadCommand)
   → LeadCreatedEvent → NotificationService (assign to salesperson)

2. LEAD QUALIFICATION
   Salesperson → POST /crm/leads/{id}/qualify
   → Lead.Qualify() → LeadQualifiedEvent
   → Opportunity.Create() → pipeline stage: Prospecting

3. OPPORTUNITY MANAGEMENT
   → move stages: Prospecting → Qualification → Proposal → Negotiation → Won/Lost
   → POST /crm/opportunities/{id}/won
   → OpportunityWonEvent → NotificationService (congratulate team)

4. ORDER CREATION
   → [Event: OpportunityWon]
   → SaleOrderService: CreateOrder (linked to opportunity)
   → standard order fulfillment flow...

5. POST-SALE
   → [Event: SalesOrderCreated]
   → CRMService: Link order to Contact
   → [Event: PaymentProcessed]
   → CRMService: Update customer LTV (lifetime value)
```

---

### 4.5 Luồng Nhân Sự & Bảng Lương

```
┌─────────────────────────────────────────────────────────────────────┐
│                    HR CYCLE                                           │
└─────────────────────────────────────────────────────────────────────┘

1. TUYỂN DỤNG & ONBOARDING
   HR → POST /hrm/employees (HireEmployeeCommand)
   → Employee.Hire() → EmployeeHiredEvent
   → IdentityService: tạo user account tự động
   → NotificationService: welcome + onboarding email

2. CHẤM CÔNG HÀNG NGÀY
   Employee → check-in/check-out (mobile app)
   → HRMService: POST /hrm/attendance (RecordAttendance)

3. XIN NGHỈ PHÉP
   Employee → POST /hrm/leave-requests (SubmitLeaveRequest)
   → LeaveRequest.Submit() → notify Manager

   Manager → POST /hrm/leave-requests/{id}/approve
   → LeaveRequest.Approve() → LeaveApprovedEvent
   → NotificationService → thông báo nhân viên

4. TÍNH LƯƠNG HÀNG THÁNG
   HR → POST /hrm/payroll/process?month=2026-06
   → ProcessPayrollCommand
   → calculate: base + overtime - deductions - tax
   → GeneratePaySlip per employee → PaySlipGeneratedEvent
   → NotificationService: gửi email phiếu lương
   → AccountingService: JournalEntry (DR Chi Phí Lương / CR Phải Trả Lương)

5. CHI TRẢ LƯƠNG
   Accountant → POST /accounting/payroll/{id}/pay
   → DR Phải Trả Lương / CR Tiền
```

---

## 5. Ma Trận Sự Kiện Tích Hợp

### Events đã có (Published → Consumed)

| Event | Publisher | Consumers hiện có | Consumers còn thiếu |
|-------|-----------|-------------------|---------------------|
| `UserRegistered` | Identity | — | HRM, CRM, Notification |
| `SalesOrderCreated` | SaleOrder | Warehouse ✅, Payment ✅ | CRM, Accounting, Promotion, Reporting |
| `SalesOrderApproved` | SaleOrder | Shipping ✅ | Accounting, Notification |
| `SalesOrderCancelled` | SaleOrder | Warehouse ✅ | Accounting, Notification |
| `SalesOrderShipped` | SaleOrder | Warehouse ✅ | Notification, Reporting |
| `InventoryReserved` | Warehouse | — | Shipping, Notification |
| `InventoryUpdated` | Warehouse | — | ProductService (sync), Reporting |
| `ProductCreated` | Product | — | SaleOrder, POS, Promotion, Reporting |
| `PaymentProcessed` | Payment | — | Accounting, CRM, Promotion, Notification, Reporting |
| `PaymentFailed` | Payment | — | Notification, SaleOrder (saga compensation) |
| `ShipmentCreated` | Shipping | — | Notification, Reporting |
| `DeliveryConfirmed` | Shipping | — | SaleOrder, Promotion, Accounting |
| `TransactionCreated` | POS | — | Warehouse, Accounting, Reporting |
| `PromotionCreated` | Promotion | — | POS, SaleOrder |
| `CouponRedeemed` | Promotion | — | Reporting |
| `EmployeeHired` | HRM | — | Identity, Notification |
| `PaySlipGenerated` | HRM | — | Accounting, Notification |

### Events chưa có (cần tạo)

| Event cần tạo | Service | Trigger | Consumers |
|---------------|---------|---------|-----------|
| `PurchaseOrderReceived` | PurchaseOrder | Nhận hàng | Warehouse, Accounting |
| `StockAlert` | Warehouse | Tồn kho thấp | PurchaseOrder, Notification |
| `InvoiceCreated` | Accounting | Sau PaymentProcessed | Payment, Reporting |
| `LeadQualified` | CRM | Chuyển lead → opportunity | Notification |
| `OpportunityWon` | CRM | Đóng deal | SaleOrder, Reporting |
| `SalesOrderDelivered` | SaleOrder | Giao hàng thành công | Promotion, CRM, Accounting |
| `ProductPriceChanged` | Product | Thay đổi giá | SaleOrder, Promotion |
| `UserUpdated` | Identity | Cập nhật thông tin | CRM, HRM |

---

## 6. Gap Analysis Tổng Hợp

### Nhóm 1: Không có Controller (0 API endpoints)

| Service | Tính năng cốt lõi chưa có |
|---------|--------------------------|
| AccountingService | Tất cả endpoints: Chart of Accounts, Invoice, Bill, Journal Entry, Reports |
| CRMService | Tất cả endpoints: Contact, Lead, Opportunity, Case |
| HRMService | Tất cả endpoints: Employee, Department, Leave, Payroll |

**Ước tính:** 3 services × ~5 controllers × ~5 endpoints = **~75 endpoints cần thêm**

### Nhóm 2: Có domain nhưng thiếu Application layer

| Service | Command còn thiếu | Query còn thiếu |
|---------|-------------------|----------------|
| SaleOrderService | Approve, Cancel, Update, Ship, ListOrders | ListOrders, GetByCustomer |
| PurchaseOrderService | Create(handler), Approve, ReceiveGoods, Cancel | List, GetById |
| WareHouseService | ReceiveStock, AdjustStock, TransferStock, LocationCRUD | StocktakeQuery |

### Nhóm 3: Thiếu event consumers (không lắng nghe events)

| Service | Events chưa consume |
|---------|---------------------|
| AccountingService | SalesOrderCreated, PurchaseOrderReceived, PaymentProcessed, WarehouseMovement |
| CRMService | UserCreated, SalesOrderCreated, PaymentReceived |
| HRMService | UserCreated |
| POSService | ProductCreated, PromotionCreated, PaymentProcessed |
| PromotionService | ProductCreated, SalesOrderCreated |
| ReportingService | TẤT CẢ events từ 13 services |

### Nhóm 4: Thiếu external integrations

| Integration | Cần cho | Ưu tiên |
|------------|---------|---------|
| Payment Gateway (VNPay/Stripe/MoMo) | PaymentService | Cao |
| Email Provider (SendGrid/SES) | NotificationService | Cao |
| SMS Provider (Twilio/VIETTEL) | NotificationService | Cao |
| FCM/APNS Push | NotificationService | Cao |
| Carrier API (GHN/GHTK/FedEx) | ShippingService | Cao |
| Full-text Search (Elasticsearch) | ProductService | Trung |

---

## 7. Thứ Tự Ưu Tiên Bổ Sung

### Sprint 1 — Hoàn Thiện Core Flow (E-Commerce)

```
1. SaleOrderService: ApproveCommand, CancelCommand, ListOrdersQuery, Controller đầy đủ
2. PurchaseOrderService: Command handlers, ReceiveGoodsCommand, event consumers
3. WarehouseService: ReceiveStockCommand, LocationCRUD API, StockAdjust
4. PaymentService: Gateway integration (VNPay/Stripe webhook)
5. ShippingService: Carrier API (GHN/GHTK), rate calculation
```

### Sprint 2 — Business Logic Services

```
6. AccountingService: Tất cả commands, queries, controllers, event consumers
7. CRMService: Tất cả commands, queries, controllers, event consumers
8. HRMService: Tất cả commands, queries, controllers, event consumers
9. NotificationService: Email/SMS/Push provider implementation
10. PromotionService: FlashSale entity, event consumers, rules engine
```

### Sprint 3 — Analytics & Polish

```
11. ReportingService: Kafka consumers, fact/dim tables, ETL handlers
12. ProductService: Full-text search, bulk import, event publishing đầy đủ
13. POSService: Event consumers, Terminal management, offline sync
14. IdentityService: 2FA, audit log, session management
```

### Sprint 4 — Production Readiness

```
15. Integration tests cho tất cả end-to-end flows
16. Performance testing (load testing với k6/Artillery)
17. Docker Compose: health checks, restart policies
18. Kubernetes: HPA, resource limits, liveness/readiness probes
19. Observability: OpenTelemetry tracing, Grafana dashboards
20. Security: Rate limiting, input sanitization, OWASP hardening
```

---

*Tài liệu này phản ánh trạng thái hệ thống tại ngày 2026-06-28 và cần được cập nhật sau mỗi sprint.*
