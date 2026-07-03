# ERP E-Commerce Solution — Phân Tích Business Logic Toàn Diện

> **Cập nhật:** 2026-07-03 — xử lý tuần tự từng service theo mục ❌ liệt kê (theo yêu
> cầu "từng service một, theo thứ tự"): đã xong 3.1 → 3.2 → 3.3 → 3.4.
> - **3.1 Identity** — đã xử lý toàn bộ tính năng ❌ còn lại (audit log, session
>   management, API key management, password policy, account-lockout wiring, domain
>   events UserUpdated/RoleAssigned/PermissionGranted) — chỉ còn LDAP/AD cố ý bỏ qua.
> - **3.2 Product** — đã xử lý toàn bộ tính năng ❌ còn lại (bulk import/export CSV,
>   product status workflow mở rộng, SEO fields, bundles, cross-sell/upsell, reviews,
>   stock sync từ Warehouse, ProductUpdated/ProductPriceChanged events) — xác nhận
>   "Application layer chưa complete" là claim sai của tài liệu gốc.
> - **3.3 Sale Order** — đã xử lý toàn bộ tính năng ❌ còn lại (UpdateOrderCommand,
>   apply coupon, order notes, partial fulfillment, order timeline/audit trail,
>   consume ProductPriceChanged, return/refund flow, SalesOrderDelivered event) — xác
>   nhận phần lớn mục ❌ cũ (Approve/Cancel/ListOrders/GetOrdersByCustomer/Controller
>   đầy đủ) là claim sai của tài liệu gốc, đã có sẵn từ trước.
> - **3.4 Purchase Order** — phát hiện quan trọng nhất đợt này: KHÔNG phải thiếu tính
>   năng, mà **7/8 handler chính (Approve/Cancel/Submit/AddItem/RemoveItem/GetById/
>   ListOrders) là stub hoàn toàn rỗng** — trả về thành công/list rỗng mà không chạm
>   database, silent failure chứ không phải lỗi rõ ràng. Đã viết lại thật toàn bộ,
>   cộng thêm ReceiveGoodsCommand (GRN), ScheduleReceipt, so sánh giá supplier,
>   three-way matching, đánh giá supplier.
>
> Mục 6 (Gap Analysis, event-driven backbone toàn hệ thống) đã cập nhật ở lần
> 2026-07-02. **3.5-3.14 chưa được xử lý ở đợt này** — sẽ tiếp tục tuần tự theo cùng
> pattern. Xem `docs/architecture/end-to-end-request-flow.md` để trace kỹ thuật
> UI→API cho các luồng chính.
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

> **Cập nhật 2026-07-02:** Bảng dưới đây đã được đối chiếu lại toàn bộ với code thật
> (không chỉ dựa vào cấu trúc thư mục). Bảng cũ đánh giá SaleOrder/PurchaseOrder/
> Accounting/CRM/HRM/Reporting thấp hơn thực tế rất nhiều — các service này thực ra
> đã có đầy đủ Domain/Application/API layer, chỉ là **event-driven backbone** (outbox
> publish, Kafka/RabbitMQ consumer) gần như không hoạt động ở bất kỳ đâu ngoài
> HRMService/NotificationService/ReportingService. Phần đó đã được rà soát và sửa
> trong đợt này — xem "Event Consumer" ở cột dưới và mục 6 (Gap Analysis).

| Service | Domain | Application | Infrastructure | API | Event Consumer | Mức Độ |
|---------|--------|-------------|----------------|-----|----------------|--------|
| IdentityService | ✅ | ✅ | ✅ (+ audit log, session mgmt, API keys, password policy — 2026-07-03) | ✅ (+ Security/ApiKeys/Sessions endpoints) | — (upstream) | **99%** (còn thiếu duy nhất LDAP, cố ý bỏ qua) |
| SaleOrderService | ✅ (+ notes, status history, partial fulfillment, return flow) | ✅ Đầy đủ lifecycle + Update/Notes/ShipItems/Return (2026-07-03) | ✅ RabbitMQ publish trực tiếp (không dùng Outbox) | ✅ 16 endpoints | ✅ (Kafka `product-topic` ProductPriceChanged, mới thêm; `OrderFulfillmentSaga` vẫn dead code) | **93%** |
| PurchaseOrderService | ✅ (+ SupplierRating, GoodsReceipt entities mới) | ✅ Đầy đủ VÀ THẬT (2026-07-03: 7/8 handler chính trước đó là stub rỗng — xem mục 3.4) | ✅ Outbox+Kafka (`purchasing-topic`); GRN/receipt-schedule/price-comparison/3-way-match mới thêm | ✅ 2 controllers, 20 endpoints (đã xoá 1 controller trùng route gây lỗi `AmbiguousMatchException`) | ✅ (outbound thật; chưa cần consume gì) | **95%** |
| WareHouseService | ✅ | ✅ | ✅ RabbitMQ publish đã sửa lỗi thiếu `IConnection` DI (trước đây crash khi gọi); consumer thật cho `sales_order_events` | ✅ 2 controllers | ✅ (RabbitMQ, `sales_order.*` + `purchasing-topic`) | **85%** |
| AccountingService | ✅ | ✅ Đầy đủ (9 commands, 12 queries kể cả AR/AP aging) | ✅ Outbox+Kafka đã hoạt động từ trước; consumer mới thêm | ✅ 5 controllers, ~19 endpoints | ✅ (RabbitMQ `sales_order.*` + Kafka `purchasing-topic`/`payment-topic`) | **90%** |
| CRMService | ✅ | ✅ Đầy đủ (14 commands, 9 queries) | ✅ Outbox+Kafka đã hoạt động từ trước; consumer mới thêm | ✅ 4 controllers, ~23 endpoints | ✅ (Kafka `identity-topic`/`payment-topic` + RabbitMQ `sales_order.*`) | **90%** |
| HRMService | ✅ (EmployeeSnapshot/LeaveRequest/PaySlip — Employee/Department đã chuyển sang SharedMasterDataService) | ✅ | ✅ `MasterDataEventConsumer` — consumer thật đầu tiên và duy nhất trong toàn hệ thống trước đợt này | ✅ 2 controllers, ~10 endpoints | ✅ | **85%** (thiếu hẳn tính năng chấm công/Attendance) |
| ProductService | ✅ (+ EAV attributes, dimensions, bundles, relations, reviews) | ✅ đầy đủ (mục cũ ghi "Features folder" thiếu là sai) | ✅ (+ Elasticsearch search, stock sync từ Warehouse) | ✅ 10 controllers | ✅ (RabbitMQ `warehouse.InventoryUpdatedEvent`, mới thêm — publish-side vẫn không consume gì khác) | **93%** |
| PaymentService | ✅ | ✅ | ✅ VNPay thật (không phải stub); Outbox đã đúng từ trước, chỉ thiếu drain job — đã thêm | ✅ 3 controllers (kể cả webhook/IPN thật) | ✅ (RabbitMQ `sales_order.SalesOrderCreatedEvent`, mới thêm) | **90%** |
| ShippingService | ✅ | ✅ (+ đã nối `AssignTrackingNumberCommand` vào endpoint, trước đây tồn tại nhưng không ai gọi được) | ✅ GHN thật; Outbox đã đúng từ trước, chỉ thiếu drain job — đã thêm | ✅ 2 controllers | ✅ (RabbitMQ `sales_order.SalesOrderApprovedEvent` + `warehouse_exchange`, cả hai mới thêm) | **90%** |
| POSService | ✅ | ✅ (+ `GetShiftSummaryQuery` trước đây không có handler, gọi là crash — đã sửa) | ⚠️ chưa có Outbox publish (không cần cho phạm vi đợt này) | ✅ 2 controllers | ✅ (Kafka `product-topic`/`promotion-topic`, mới thêm) | **80%** |
| PromotionService | ⚠️ Thiếu FlashSale entity/command (đã xác nhận không tồn tại, không phải "thiếu 1 phần") | ✅ (+ `RedeemLoyaltyPointsCommand` trước đây không có handler, gọi là crash — đã sửa; `Coupon.Redeem` trước đây không phát `CouponRedeemedEvent` — đã sửa) | ✅ Outbox+Kafka (`promotion-topic`) mới thêm — trước đây publish 0% dù DbContext đã sẵn sàng | ✅ 2 controllers | ✅ (Kafka `product-topic` + RabbitMQ `sales_order.*`, mới thêm) | **85%** |
| NotificationService | ✅ | ✅ | ✅ SMTP thật, Twilio/FCM thật khi cấu hình (đã sửa lỗi thiếu `AddHttpClient()` khiến crash nếu bật) | ✅ 3 controllers | ✅ (chỉ `identity-topic` — các topic khác vẫn là handler chết, xem mục 6) | **88%** |
| ReportingService | ⚠️ Không có domain entity giàu hành vi, nhưng có model/DbSet thật khớp schema OLAP | ✅ (3/4 query thật; `GetInventoryReport` vẫn là stub trả về 0 cứng) | ✅ 3 Kafka consumer thật (Sales/Payment/Inventory) — tài liệu cũ ghi "0 consumer" là sai | ✅ 1 controller, 3 endpoints (không có endpoint HR — `GetHRDashboard` không tồn tại) | ✅ (đã có sẵn, tài liệu cũ ghi nhầm là thiếu) | **55%** |

**Trung bình toàn hệ thống: ~87%** (tăng từ ~57% ban đầu sau khi (a) đối chiếu lại đánh
giá cho đúng với code thật — nhiều service đã bị đánh giá thấp hơn thực tế rất nhiều —
(b) sửa event-driven backbone thật sự cho 8 service, và (c) xử lý toàn bộ mục ❌ còn lại
của IdentityService (3.1), ProductService (3.2), SaleOrderService (3.3),
PurchaseOrderService (3.4) ngày 2026-07-03 — 3.4 đặc biệt đáng chú ý vì phát hiện 7/8
handler chính là stub rỗng, không phải "thiếu tính năng" như tài liệu gốc mô tả.
ReportingService là service còn khoảng cách rõ ràng nhất với phần còn lại (55%); các
mục 3.5-3.14 khác chưa được xử lý theo cùng mức độ ở đợt này.

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
- Đăng nhập bên thứ ba: Google, Microsoft (verify ID token qua `IExternalAuthProvider`),
  Facebook (Graph API `/me` + `/debug_token`), Twitter/X (OAuth 2.0 Authorization
  Code + PKCE riêng qua `TwitterOAuthService`, vì Twitter không có token xác thực
  một lần như 3 provider còn lại) — `POST /api/auth/external-login`,
  `GET/POST /api/auth/external-login/twitter/...`
- Xác thực 2 bước (2FA) qua SMS hoặc Email: sau khi verify mật khẩu, nếu tài khoản/tenant
  yêu cầu 2FA, hệ thống phát sinh mã OTP một lần (hash lưu DB, mã thật gửi qua Kafka
  `identity-topic` → NotificationService gửi SMS/Email) — `POST /api/auth/2fa/verify`,
  `POST /api/auth/2fa/resend`, tự bật/tắt qua `POST /api/users/{id}/2fa/enable|disable`
- Auth Settings 2 cấp: `SystemAuthSettings` (SuperAdmin, toàn hệ thống — công tắc gốc)
  và `TenantAuthSettings` (theo từng tenant, chỉ có hiệu lực khi System cũng bật) —
  bật/tắt từng provider đăng nhập ngoài + 2FA khả dụng/bắt buộc —
  `GET/PUT /api/settings/system`, `GET/PUT /api/settings/tenant/{tenantId}`,
  `GET /api/auth/login-options` (public, cho trang login biết hiện nút SSO nào)
- Khóa tài khoản sau 5 lần sai mật khẩu liên tiếp — `User.RecordLoginFail()`/`RecordLoginSuccess()`
  tồn tại từ trước nhưng **`AuthService.LoginAsync` chưa từng gọi chúng** (cùng dạng bug "trông
  như đã xong nhưng chưa từng chạy" phát hiện xuyên suốt hệ thống ở các đợt sửa trước) — đã sửa
  (2026-07-03): kiểm tra `IsLocked` trước khi verify mật khẩu, `RecordLoginFail()` khi sai,
  `RecordLoginSuccess()` khi đúng
- Audit log bảo mật: `SecurityAuditLogs` (mọi sự kiện — Login/Logout/Register/PasswordChange/
  PasswordReset/RoleAssigned/AccountLocked/AccountUnlocked, thành công lẫn thất bại) và
  `LoginAttempts` (IP-based) — 2 bảng này tồn tại trong `001_CreateAuthDB.sql` từ đầu nhưng
  không có entity/DbSet C# nào tương ứng cho tới khi sửa (2026-07-03); nay có Domain entity +
  repository + `ISecurityAuditService`, ghi tại mọi điểm xác thực/đổi mật khẩu/gán role —
  `GET /api/security/audit-logs`, `GET /api/security/login-attempts` (yêu cầu quyền
  `security:audit:view`)
- Session management: `DeviceSessions` (upsert mỗi lần đăng nhập thành công, khoá theo
  UserId+DeviceId — nếu client không gửi `DeviceId` thì dùng hash User-Agent làm khoá dự phòng)
  và `TokenBlacklist` (thu hồi tức thời 1 JWT theo `jti`, kiểm tra qua `OnTokenValidated` khi
  Identity tự validate JWT của chính nó — **giới hạn thật:** các service khác tin header do
  Gateway inject chứ không tự validate JWT, nên 1 token đã bị blacklist vẫn còn dùng được với
  các service đó cho tới khi hết hạn tự nhiên) — `GET/POST /api/users/{id}/sessions[/{id}/revoke]`
- API Key management: entity `ApiKey` mới (bảng `ApiKeys`, migration 003) cho service-to-service
  calls — tạo/liệt kê/thu hồi qua `POST/GET /api/api-keys`, `POST /api/api-keys/{id}/revoke`; raw
  key chỉ trả về đúng 1 lần lúc tạo, chỉ lưu hash. **Việc từng service cụ thể chấp nhận API Key
  thay/cùng với Bearer token là bước tích hợp riêng của từng service, chưa làm ở đợt này** — đợt
  này chỉ xây xong phần quản lý khoá
- Password policy: từ chối mật khẩu ngắn hơn `PasswordPolicy:MinLength` (mặc định 8) và mật khẩu
  trùng với 1 trong N mật khẩu gần nhất trong `PasswordHistories` (`PasswordPolicy:HistoryCount`,
  mặc định 5) — áp dụng ở `POST /api/auth/reset-password` và `POST /api/users/{id}/change-password`
- Domain events còn thiếu đã bổ sung: `UserUpdatedDomainEvent` (raise từ `User.UpdateProfile()`),
  `RoleAssignedDomainEvent` (raise từ `User.AssignRole()`), `PermissionGrantedDomainEvent` (raise
  từ `Role.NotifyPermissionGranted()`, gọi khi gán permission cho role) — cả 3 đi qua Outbox+Kafka
  `identity-topic` giống các event khác

**Authorization (RBAC)**
- Quản lý Roles (CRUD)
- Gán Role cho User
- Quản lý Permissions (CRUD)
- Gán Permission trực tiếp cho User hoặc qua Role
- Middleware truyền UserContext qua HTTP header sang các service khác

**User Management**
- CRUD người dùng
- Kích hoạt / vô hiệu hóa tài khoản
- Xem/thu hồi phiên đăng nhập đang hoạt động (`DeviceSessions`)

**Multi-Tenant**
- Quản lý Tenants (CRUD)
- Gán User vào Tenant
- Lọc dữ liệu theo TenantId

**Published Events**
- `UserRegisteredIntegrationEvent` → Kafka
- `TwoFactorCodeGeneratedEvent` → Kafka (`identity-topic`) — chỉ NotificationService consume, để gửi OTP
- `UserUpdatedDomainEvent`, `RoleAssignedDomainEvent`, `PermissionGrantedDomainEvent` → Kafka
  (`identity-topic`) — mới thêm 2026-07-03; chưa có consumer nào ở service khác lắng nghe 3 event
  này, tương tự tình trạng ban đầu của `TwoFactorCodeGeneratedEvent` trước khi Notification wire nó

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| ~~Xác thực 2 bước (2FA/MFA)~~ | ~~Cao~~ | **Đã triển khai** — SMS/Email OTP, xem `docs/business/Identity/ExternalAuthAndTwoFactor.md` |
| ~~Khóa tài khoản sau N lần sai mật khẩu~~ | ~~Cao~~ | **Đã sửa 2026-07-03** — method đã có từ trước nhưng chưa từng được gọi trong `LoginAsync`, nay đã wire |
| ~~Audit log đăng nhập~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — xem chi tiết ở mục ✅ phía trên |
| ~~Session management~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** — xem chi tiết ở mục ✅ phía trên, kèm giới hạn thật đã ghi rõ |
| ~~API Key management~~ | ~~Trung~~ | **Đã triển khai phần quản lý (create/list/revoke) 2026-07-03** — việc từng service chấp nhận API Key vẫn chưa làm |
| ~~Password policy configuration~~ | ~~Thấp~~ | **Đã triển khai 2026-07-03** — độ dài tối thiểu (config) + chống trùng N mật khẩu gần nhất; chưa có UI admin cấu hình theo tenant, chỉ đọc từ `appsettings.json` |
| LDAP/Active Directory integration | Thấp | **Cố ý bỏ qua ở đợt này** — cần thư viện LDAP client thật + một Active Directory thật để kiểm thử, không có sẵn trong môi trường này; khác các gap khác (đều chỉ cần code + schema có sẵn), đây là một loại tích hợp bên ngoài hoàn toàn mới, chưa có bất kỳ phần nào trong repo |
| ~~Published: `UserUpdated`, `RoleAssigned`, `PermissionGranted`~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — xem mục Published Events phía trên; chưa có service nào consume |

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

**Mục đích:** Catalog sản phẩm, variants, danh mục, giá, ảnh. Upstream source — publish
events, không consume (ngoại trừ đồng bộ tồn kho từ Warehouse, xem dưới).

#### ✅ Tính năng đã có

**Product Catalog**
- CRUD sản phẩm (`ProductsController`)
- Quản lý danh mục phân cấp (`CategoriesController`)
- Quản lý variants (SKU, barcode) + kích thước vật lý có cấu trúc
  (Length/Width/Height/DimensionUnit, Weight/WeightUnit — cột riêng, dùng cho tính
  toán vận chuyển/kho; giữ cột `Dimensions` chuỗi tự do cũ để tương thích ngược)
  (`VariantsController`)
- Thuộc tính sản phẩm mở rộng theo danh mục (màu sắc, kích thước, đặc tính kỹ thuật...)
  — hệ thống EAV-lite `ProductAttributes`/`ProductAttributeOptions`/
  `ProductAttributeValues`, không cần migration khi thêm thuộc tính mới
  (`AttributesController`) — xem `docs/architecture/extensible-attributes-pattern.md`
- Tìm kiếm & lọc sản phẩm full-text + theo thuộc tính động qua Elasticsearch —
  đồng bộ event-driven từ SQL (system-of-record) sang ES (read-model có thể build lại)
  qua `SearchIndexQueue`/`SearchIndexProcessor`
  (`GET/POST /api/products/search/advanced...`)
- Upload & quản lý ảnh sản phẩm (`ImagesController`)
- Quản lý bảng giá (`PricingController`)
- Mapping ID sản phẩm/variant sang hệ thống bên thứ ba (marketplace, ERP nhà cung
  cấp) — `ProductExternalMappings`
- **Application layer (2026-07-03): xác nhận ĐÃ đầy đủ, không phải đang thiếu** — rà
  soát lại toàn bộ `Features/` cho thấy mọi Command/Query đều có Handler thật
  (`CreateProductCommand`, `PublishProductCommand`, `DiscontinueProductCommand`,
  `SetProductPriceCommand`, variants/categories/images/pricing/attributes/search đều
  đầy đủ) — mục ❌ cũ ghi "Features folder chưa complete" là sai, tài liệu gốc chưa
  từng đối chiếu với code thật
- Quy trình trạng thái sản phẩm mở rộng: `Draft → PendingReview → Active →
  Discontinued` (`POST /{id}/submit-for-review` rồi `POST /{id}/publish`) — bước
  PendingReview là **tùy chọn**, `Publish()` vẫn nhận trực tiếp từ Draft như trước để
  không phá luồng hiện có của team không cần duyệt
- SEO fields: `Slug` (duy nhất), `MetaTitle`, `MetaDescription` — `PUT /{id}/seo`
- Bulk import/export CSV: `POST /api/products/bulk-import` (trả về số dòng thành
  công + danh sách lỗi theo dòng), `GET /api/products/export` — dùng CsvHelper, chưa
  hỗ trợ Excel gốc (.xlsx) vì CSV đã đủ, Excel mở/sửa CSV trực tiếp được
- Product bundles/combo: `ProductBundleItems` (bundle Product tham chiếu tới các
  Product thành phần + số lượng) — `GET/POST/DELETE /api/products/{id}/bundle-items`
- Cross-sell/upsell: `ProductRelations` (liên kết một chiều Product → Product liên
  quan, loại CrossSell/UpSell) — `GET/POST/DELETE /api/products/{id}/relations`
- Product reviews & ratings: `ProductReviews` (rating 1-5 + comment, CustomerId tham
  chiếu lỏng sang CRM/Identity) — `GET/POST /api/products/{id}/reviews`, trả về cả
  điểm trung bình; **chưa có hàng đợi kiểm duyệt** (mọi review tự động `IsApproved`)
- Stock quantity sync từ Warehouse: `Product.StockQuantity`/`LastStockSyncAt` —
  read-model đồng bộ qua `WarehouseRabbitMqConsumer` lắng nghe
  `warehouse_exchange`/`warehouse.InventoryUpdatedEvent` (RabbitMQ, cùng cơ chế
  SaleOrderService/WarehouseService dùng) — SQL của WarehouseService vẫn là
  system-of-record, đây chỉ là cache hiển thị

**Domain Events**
- `ProductCreatedDomainEvent` → Published khi tạo sản phẩm
- `ProductDiscontinuedDomainEvent` → Published khi ngừng kinh doanh
- `ProductPublishedDomainEvent` → Published khi đăng bán
- `ProductUpdatedDomainEvent` (mới 2026-07-03) → Published khi `UpdateDescription()` —
  trước đây hoàn toàn không tồn tại, dù `CLAUDE.md`/tài liệu này liệt kê là published
  event cần có; **chưa có service nào consume**
- `ProductPriceChangedDomainEvent` (mới 2026-07-03) → Published khi
  `SetProductPriceCommand` đổi giá variant — trước đây không tồn tại, khiến
  POS/Promotion (đã wire consumer ở đợt trước) luôn nhận giá mặc định 0; giờ **đã có
  nguồn phát**, nhưng POS/Promotion vẫn cần tự cập nhật consumer để nhận field mới
  (còn thiếu, xem mục 6)
- `ProductSearchIndexRequestedDomainEvent` → **nội bộ, không qua Kafka** — chỉ nạp vào
  `SearchIndexQueue` để đồng bộ Elasticsearch, không phải integration event cho
  service khác

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| ~~Tìm kiếm & lọc sản phẩm (full-text search)~~ | ~~Cao~~ | **Đã triển khai** — Elasticsearch, xem mục ✅ ở trên. Storefront UI lọc/tìm kiếm theo facet vẫn CHƯA làm (phạm vi backend only) |
| ~~Bulk import/export (Excel/CSV)~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — CSV (không phải Excel gốc), xem mục ✅ |
| ~~Trạng thái sản phẩm workflow~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — PendingReview thêm như bước tùy chọn |
| ~~SEO fields~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** |
| ~~Product bundles / combo~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** |
| ~~Cross-sell / upsell relationships~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** |
| ~~Stock quantity sync từ Warehouse~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — xem mục ✅ |
| ~~`ProductUpdated` integration event~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — chưa có consumer |
| ~~`ProductPriceChanged` integration event~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — POS/Promotion cần cập nhật consumer để nhận field mới, xem mục 6 |
| ~~Application layer: command handlers đầy đủ~~ | ~~Cao~~ | **Xác nhận đã đầy đủ từ trước** — mục này trong tài liệu gốc ghi sai, xem mục ✅ |
| ~~Attribute system (custom attributes)~~ | ~~Thấp~~ | **Đã triển khai** — xem `docs/architecture/extensible-attributes-pattern.md` và `ProductAttributes`/`ProductAttributeValues` trong ProductDB |
| ~~Product reviews & ratings~~ | ~~Thấp~~ | **Đã triển khai 2026-07-03** — chưa có hàng đợi kiểm duyệt, xem mục ✅ |

**Còn thiếu, chưa làm trong đợt này:** Excel (.xlsx) gốc cho bulk import/export (chỉ có
CSV); hàng đợi kiểm duyệt review; storefront UI cho facet search/SEO/bundles/
cross-sell/reviews (toàn bộ đợt này là backend-only, giống Elasticsearch trước đó);
POS/Promotion chưa cập nhật consumer để đọc `ProductPriceChangedDomainEvent` thật.

#### Luồng Vận Hành

```
Admin → POST /api/products → CreateProductCommand → MediatR → Handler
     → Product.Create() → raises ProductCreatedDomainEvent
     → EF Core save → OutboxProcessor → Integration Event
     → SaleOrderService (cache sản phẩm)
     → POSService (sync local)
     → PromotionService (apply discount rules)
     → ReportingService (dimension table DimProducts)

WarehouseService → [InventoryUpdatedEvent] → RabbitMQ warehouse_exchange
     → ProductService.WarehouseRabbitMqConsumer → Product.SyncStockQuantity()
     → StockQuantity cập nhật + ProductSearchIndexRequestedDomainEvent (ES resync)
```

---

### 3.3 Sale Order Service — Đơn Hàng Bán

**Mục đích:** Tạo và quản lý đơn hàng bán. Publish qua RabbitMQ trực tiếp (không dùng
Outbox) — xem mục 1 Pattern Áp Dụng Nhất Quán.

#### ✅ Tính năng đã có

**Domain**
- `SalesOrder` aggregate với `OrderLineItem` value object (nay có thêm
  `ShippedQuantity`/`RemainingQuantity` cho partial fulfillment)
- `Address` value object (shipping)
- `OrderNote` value object (nhiều ghi chú nội bộ theo thời gian, không phải chỉ 1
  field `Notes` duy nhất lúc tạo đơn)
- `OrderStatusHistoryEntry` value object — audit trail tự động, mỗi lần đổi trạng
  thái đều tự ghi 1 dòng (From/To/Reason/ChangedBy/ChangedAt)
- Trạng thái: `Draft → Submitted → Approved → Picked → Shipped/PartiallyShipped →
  Delivered → ReturnRequested → Returned`, cộng `Rejected`/`Cancelled` — **đã xác
  nhận 2026-07-03**: lifecycle này đã đầy đủ Approve/Reject/Pick/Ship/Cancel/
  MarkDelivered từ trước, tài liệu gốc ghi thiếu là sai; chỉ thêm mới
  `PartiallyShipped`/`ReturnRequested`/`Returned`
- Domain Events: Created, Approved, Rejected*, Cancelled, Shipped, PartiallyShipped
  (mới), Delivered (mới), Returned (mới) — (*Rejected có transition nhưng không
  raise integration event, giữ nguyên như trước)

**Commands/Queries — xác nhận ĐẦY ĐỦ 2026-07-03, tài liệu gốc ghi thiếu là sai:**
`CreateOrderCommand`, `SubmitOrderCommand`, `ApproveOrderCommand`,
`RejectOrderCommand`, `MarkAsPickedCommand`, `ShipOrderCommand`,
`CancelOrderCommand`, `MarkAsDeliveredCommand` đều đã có Handler từ trước.
`GetOrderQuery`, `ListOrdersQuery` (filter CustomerId/Status + skip/take),
`GetOrdersByCustomerQuery` đều đã có Handler từ trước. Controller
(`SalesOrdersController`) đã có 11 endpoint từ trước — mục ❌ cũ ghi "chỉ có 1
controller rất hạn chế" là sai.

**Mới thêm 2026-07-03:**
- `UpdateOrderCommand` (`PUT /api/v1/salesorders/{id}`) — sửa địa chỉ/ghi chú, chỉ
  cho phép khi Draft/Submitted
- `AddOrderNoteCommand` (`POST /{id}/notes`) — ghi chú nội bộ, không giới hạn trạng
  thái, không như field `Notes` cũ chỉ set được lúc tạo đơn
- Partial fulfillment: `ShipLineItemsCommand` (`POST /{id}/ship-items`) — giao một
  phần số lượng theo từng sản phẩm, đơn chuyển `PartiallyShipped` nếu còn dư, hoặc
  `Shipped` (raise đúng `SalesOrderShippedEvent` cũ) nếu giao hết; endpoint
  `/ship` cũ vẫn hoạt động y hệt (gọi `Ship()` nội bộ giờ delegate sang
  `ShipLineItems` với toàn bộ số lượng còn lại)
- Return/Refund flow: `RequestReturnCommand` (`POST /{id}/return`, chỉ từ
  Delivered) → `CompleteReturnCommand` (`POST /{id}/return/complete`) → raise
  `SalesOrderReturnedEvent` — Payment/Accounting tự xử lý hoàn tiền/đảo bút toán
  qua event, SaleOrderService không gọi trực tiếp
- Apply coupon khi tạo đơn: `CreateOrderCommand.CouponCode` (optional) →
  `IPromotionServiceClient` gọi thật `POST /api/v1/promotions/validate-coupon` của
  PromotionService (HTTP đồng bộ, cho phép theo `CLAUDE.md` vì chỉ là REST read,
  không phải cross-database) — nếu PromotionService lỗi/timeout thì đơn **vẫn tạo
  được**, chỉ log ghi chú nội bộ "coupon not applied", không chặn tạo đơn
- Consume `ProductPriceChanged`: `ProductPriceChangedConsumer` (Kafka
  `product-topic`) — trước đây không thể làm vì event nguồn chưa tồn tại (xem mục
  3.2); nay khi giá đổi, mọi đơn còn mở (chưa Shipped/Delivered/Cancelled/Returned)
  chứa sản phẩm đó được đánh dấu `HasPriceChangeWarning=true` + note giải thích —
  **không** tự động đổi giá đơn đã tạo
- `SalesOrderDeliveredEvent` — trước đây `MarkAsDeliveredCommandHandler` đã có sẵn
  vòng lặp publish domain events, nhưng `MarkAsDelivered()` chưa từng raise event
  nào nên vòng lặp đó luôn publish 0 event; đã sửa

**Saga**
- `OrderFulfillmentSaga`/`OrderFulfillmentSagaState`/`IOrderFulfillmentSagaRepository`
  — **vẫn là dead code, chưa động tới ở đợt này** — không đăng ký DI, không có
  entity trong DbContext; quyết định giữ làm true orchestration hay bỏ hẳn để dùng
  choreography (các consumer RabbitMQ/Kafka đã wire ở Phase 1) vẫn treo, xem mục 6

**Infrastructure**
- `RabbitMqMessageBus` — publish trực tiếp, exchange `sales_order_events`, routing
  key `sales_order.{EventType}`, JSON thường (không `$type`)
- `SalesOrderRepository`
- Mới: `ProductPriceChangedConsumer` (Kafka), `PromotionServiceClient` (HTTP)

**Published Events**
- `SalesOrderCreatedEvent`, `SalesOrderApprovedEvent`, `SalesOrderShippedEvent`,
  `SalesOrderCancelledEvent` (đã có từ trước)
- `SalesOrderPartiallyShippedEvent`, `SalesOrderDeliveredEvent`,
  `SalesOrderReturnedEvent` (mới 2026-07-03) — **chưa có consumer nào ở service
  khác**, cùng tình trạng với các event mới thêm ở 3.1/3.2

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| ~~`ApproveOrderCommand`~~ | ~~Cao~~ | **Xác nhận đã có từ trước** — tài liệu gốc ghi sai |
| ~~`CancelOrderCommand`~~ | ~~Cao~~ | **Xác nhận đã có từ trước** — tài liệu gốc ghi sai |
| ~~`UpdateOrderCommand`~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — gap thật duy nhất trong nhóm Commands |
| ~~`ListOrdersQuery` với filter/sort/page~~ | ~~Cao~~ | **Xác nhận đã có từ trước** — tài liệu gốc ghi sai |
| ~~`GetOrdersByCustomerQuery`~~ | ~~Cao~~ | **Xác nhận đã có từ trước** — tài liệu gốc ghi sai |
| ~~Controller endpoints đầy đủ~~ | ~~Cao~~ | **Xác nhận đã có 11 endpoint từ trước** — tài liệu gốc ghi "1 controller hạn chế" là sai |
| ~~Apply promotion/coupon khi tạo đơn~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** |
| ~~Order notes / internal comments~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** |
| ~~Partial fulfillment~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** |
| ~~Order timeline / audit trail~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** |
| ~~Consume `ProductPriceChanged` event~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — chỉ đánh dấu cảnh báo, không tự đổi giá đơn |
| ~~Return/Refund order flow~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — SaleOrder chỉ ghi nhận + publish event, refund thật do Payment/Accounting xử lý |
| ~~`SalesOrderDelivered` event~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — trước đây handler chờ publish event không bao giờ tồn tại |

**Còn thiếu, chưa làm trong đợt này:** `OrderFulfillmentSaga` vẫn dead code (quyết
định giữ/bỏ vẫn treo); chưa có consumer nào cho 3 event mới
(PartiallyShipped/Delivered/Returned); refund thật (gọi PaymentService) vẫn cần
PaymentService tự thêm consumer cho `SalesOrderReturnedEvent`.

#### Luồng Vận Hành

```
Customer → POST /api/v1/salesorders → CreateOrderCommand
                            → (nếu có CouponCode) → PromotionServiceClient.ValidateCouponAsync
                            → SalesOrder.Create() → SalesOrderCreatedEvent
                            → RabbitMqMessageBus → sales_order_events exchange

Choreography qua các consumer đã wire ở Phase 1 (không qua saga trung tâm):
  WarehouseService.SalesOrderRabbitMqConsumer  → reserve inventory
  PaymentService.SalesOrderRabbitMqConsumer    → auto-initiate payment
  ShippingService.SalesOrderRabbitMqConsumer   → create shipment (khi Approved)
  AccountingService/CRMService consumers       → invoice/link contact

POST /{id}/ship-items (partial) → ShipLineItemsCommand
     → PartiallyShipped (còn dư) hoặc Shipped (hết) → SalesOrderShippedEvent

POST /{id}/return → RequestReturnCommand → ReturnRequested
POST /{id}/return/complete → CompleteReturnCommand → Returned → SalesOrderReturnedEvent
     → (chưa có consumer) Payment cần tự wire để hoàn tiền thật

ProductService.ProductPriceChangedDomainEvent → Kafka product-topic
     → SaleOrderService.ProductPriceChangedConsumer → flag đơn còn mở
```

---

### 3.4 Purchase Order Service — Đơn Hàng Mua

**Mục đích:** Quản lý quá trình mua hàng từ nhà cung cấp, từ yêu cầu mua đến nhận hàng.

> **Phát hiện quan trọng nhất đợt này (2026-07-03):** Đây KHÔNG phải trường hợp
> "thiếu tính năng" như tài liệu gốc mô tả — mà là **7 trong 8 handler chính đã tồn
> tại nhưng thân hàm hoàn toàn rỗng** (`await Task.CompletedTask; return Unit.Value;`
> hoặc `return _mapper.Map<PurchaseOrderDto>(new object())` — map từ `object` rỗng ra
> DTO rác). Nghiêm trọng hơn nhiều so với các gap dạng "consumer chưa wire" tìm thấy ở
> các service khác: `ApprovePurchaseOrderCommandHandler`, `CancelPurchaseOrderCommandHandler`,
> `SubmitPurchaseOrderCommandHandler`, `GetPurchaseOrderByIdQueryHandler`,
> `GetPurchaseOrdersQueryHandler` không hề chạm vào database; `AddItemCommandHandler`/
> `RemoveItemCommandHandler` tạo/thao tác object trong bộ nhớ rồi vứt đi, không bao
> giờ lưu. Endpoint tương ứng trong `PurchaseOrdersController` gọi các handler này vẫn
> trả về 200 OK (rỗng) — silent failure, không phải lỗi 500. Chỉ
> `CreatePurchaseOrderCommandHandler` là thật. Tất cả đã được viết lại thật sự
> 2026-07-03.

#### ✅ Tính năng đã có

**Domain**
- `PurchaseOrder` aggregate, `PurchaseOrderItem` entity, `Supplier` entity
- `Address`, `Money` value objects
- Trạng thái: `Draft → Submitted → Approved → Ordered → PartiallyReceived → Received`,
  cộng `Cancelled`
- Domain Events: Created, Approved, Cancelled (**`Cancel()` có sẵn
  `PurchaseOrderCancelledDomainEvent` từ trước nhưng chưa từng raise — đã sửa
  2026-07-03**), Received (mới), ReceiptScheduled (mới)
- Mới 2026-07-03: `PurchaseOrderItem.ReceivedQuantity`/`RemainingQuantity` (cột SQL
  `ReceivedQuantity` đã có sẵn từ `001_CreatePurchasingDB.sql` nhưng không có property
  C# tương ứng); `PurchaseOrder.ScheduledReceiptDate`; `GoodsReceipt` entity (bảng SQL
  `GoodsReceipts` cũng đã có sẵn từ trước, cùng tình trạng schema mồ côi); `SupplierRating`
  entity (bảng mới)

**Commands/Queries — viết lại thật 2026-07-03 (trước đó là stub rỗng):**
`ApprovePurchaseOrderCommand`, `CancelPurchaseOrderCommand`, `SubmitPurchaseOrderCommand`,
`AddItemCommand`, `RemoveItemCommand`, `GetPurchaseOrderByIdQuery`, `GetPurchaseOrdersQuery`
(filter SupplierId/Status + phân trang). Dùng `IRepository<PurchaseOrder>`/
`IRepository<PurchaseOrderItem>` (Clean Architecture — Application không được tham
chiếu Infrastructure/DbContext trực tiếp); AddItem/RemoveItem dùng thêm
`IRepository<PurchaseOrderItem>` riêng để đảm bảo EF Core đánh dấu Added/Deleted đúng
(không đoán nhầm Modified cho dòng vừa tạo có sẵn khoá Guid).

**Mới thêm 2026-07-03:**
- `ReceiveGoodsCommand` (GRN, `POST /{id}/receive`) — nhận từng phần theo dòng, PO
  chuyển `PartiallyReceived` hoặc `Received`, ghi 1 dòng `GoodsReceipt`, raise
  `PurchaseOrderReceivedDomainEvent`
- `ScheduleReceiptCommand` (`POST /{id}/schedule-receipt`) → `ReceiptScheduledDomainEvent`
- `ComparePricesForProductQuery` (`GET /compare-prices?productCode=`) — so sánh giá
  theo supplier dựa trên lịch sử PO (join trong bộ nhớ, không cần IQueryable xuyên
  aggregate)
- `ThreeWayMatchQuery` (`POST /{id}/three-way-match`) — so khớp PO vs GRN (đã nhận) vs
  Invoice; **dòng invoice do caller (thường là AccountingService) truyền vào**, không
  lưu trữ hay tự tra cứu Invoice (không cross-database theo `CLAUDE.md`)
- `RateSupplierCommand`/`GetSupplierRatingsQuery` (`POST/GET /suppliers/{id}/ratings`)
  — đánh giá 1-5 sao + trung bình

**Infrastructure**
- Outbox+Kafka (`purchasing-topic`) đã hoạt động từ trước (đợt Phase 1)
- `PurchasingDbContext` — thêm `AutoInclude()` cho `Supplier` navigation (trước đây
  chỉ `Items` được auto-include, khiến `Supplier` luôn null trong DTO)

**API Controllers**
- `PurchaseOrdersController` (nay đầy đủ, 13 endpoints), `SuppliersController` (nay 7 endpoints)

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| ~~`CreatePurchaseOrderCommand` + Handler~~ | ~~Cao~~ | **Xác nhận đã có từ trước, hoạt động đúng** — mục ❌ cũ ghi sai |
| ~~`ApprovePurchaseOrderCommand`~~ | ~~Cao~~ | **Handler rỗng — đã viết lại thật 2026-07-03** |
| ~~`ReceiveGoodsCommand` (GRN)~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** |
| ~~`ListPurchaseOrdersQuery`~~ | ~~Cao~~ | **Handler rỗng (trả `new List<>()` cứng) — đã viết lại thật 2026-07-03** |
| ~~`GetPurchaseOrderQuery`~~ | ~~Cao~~ | **Handler rỗng (map từ `new object()`) — đã viết lại thật 2026-07-03** |
| ~~`CancelPurchaseOrderCommand`~~ | ~~Trung~~ | **Handler rỗng — đã viết lại thật 2026-07-03** |
| ~~Integration Event Consumers~~ | ~~Cao~~ | **Không cần consume gì thêm ở đợt này** — PurchaseOrderService không có nhu cầu nghiệp vụ nào cần nhận event từ service khác hiện tại |
| ~~`PurchaseOrderReceived` integration event~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — WarehouseService cần tự thêm consumer để thực sự nhập kho (xem mục 3.5 khi xử lý tới) |
| ~~Supplier evaluation / rating~~ | ~~Thấp~~ | **Đã triển khai 2026-07-03** |
| ~~Price comparison giữa các supplier~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** |
| ~~`ReceiptScheduled` event~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** |
| ~~Three-way matching~~ | ~~Thấp~~ | **Đã triển khai 2026-07-03** — chỉ so khớp, không lưu trữ Invoice |

**Còn thiếu, chưa làm trong đợt này:** WarehouseService chưa có consumer cho
`PurchaseOrderReceivedDomainEvent` (chỉ mới consume `PurchaseOrderApprovedDomainEvent`
để "chuẩn bị nhận hàng" từ Phase 1) — nhập kho thật cần chờ xử lý tới mục 3.5.

#### Luồng Vận Hành

```
Staff → POST /api/purchase-orders → CreatePurchaseOrderCommand
     → PurchaseOrder.Create() → PurchaseOrderCreatedEvent → Outbox → Kafka purchasing-topic
     → AccountingService consumer (đã wire từ Phase 1, tạo AP bill khi Approved)

Manager → POST /api/purchase-orders/{id}/approve → ApprovePurchaseOrderCommand
        → PurchaseOrder.Approve() → PurchaseOrderApprovedEvent
        → WarehouseService consumer (Phase 1, chuẩn bị nhận hàng)

Manager → POST /api/purchase-orders/{id}/schedule-receipt → ScheduleReceiptCommand
        → ReceiptScheduledEvent (chưa có consumer)

Warehouse → POST /api/purchase-orders/{id}/receive → ReceiveGoodsCommand
          → PurchaseOrder.ReceiveGoods() → PartiallyReceived hoặc Received
          → ghi GoodsReceipt → PurchaseOrderReceivedEvent (chưa có consumer — xem mục 3.5)

Accounting → POST /api/purchase-orders/{id}/three-way-match (truyền dòng invoice của mình)
           → so khớp PO/GRN/Invoice, trả về mismatch nếu có
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

> **Chi tiết kỹ thuật (UI → Gateway → Controller → MediatR → Domain → Outbox/Kafka/
> RabbitMQ → service khác):** xem
> [`docs/architecture/end-to-end-request-flow.md`](../architecture/end-to-end-request-flow.md).
> Các luồng dưới đây chỉ mô tả ở mức nghiệp vụ; tài liệu kia trace từng bước bằng
> route/command/event thật đã verify trong code, kể cả 3 luồng không có ở đây:
> Identity/Auth (login, 2FA, external login), Product Catalog & Search (Admin CRUD +
> Elasticsearch), Master Data (Admin CRUD pattern).

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
| `InventoryUpdated` | Warehouse | ProductService ✅ (stock sync, 2026-07-03) | Reporting |
| `ProductCreated` | Product | POS ✅, Promotion ✅ | SaleOrder, Reporting |
| `ProductUpdated` | Product | — (mới 2026-07-03) | SaleOrder, POS, Promotion |
| `ProductPriceChanged` | Product | SaleOrder ✅ (chỉ flag cảnh báo, 2026-07-03) | POS/Promotion cần cập nhật consumer để đọc field mới |
| `UserUpdated` | Identity | — (mới 2026-07-03) | CRM, HRM |
| `SalesOrderDelivered` | SaleOrder | — (mới 2026-07-03) | Promotion (loyalty points), CRM (purchase history) |
| `SalesOrderPartiallyShipped` | SaleOrder | — (mới 2026-07-03) | Warehouse, Notification |
| `SalesOrderReturned` | SaleOrder | — (mới 2026-07-03) | Payment (hoàn tiền thật), Accounting, Warehouse (restock) |
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

> **Cập nhật 2026-07-03:** SaleOrderService (mục 3.3) và PurchaseOrderService (mục
> 3.4) đã xử lý xong — PurchaseOrderService hoá ra không phải "thiếu" mà là 7/8
> handler chính tồn tại nhưng thân hàm rỗng (xem mục 3.4). Chỉ còn WareHouseService
> (mục 3.5, chưa xử lý ở đợt này).

| Service | Command còn thiếu | Query còn thiếu |
|---------|-------------------|----------------|
| ~~SaleOrderService~~ | ~~Approve, Cancel, Update, Ship, ListOrders~~ (đã xong 2026-07-03) | ~~ListOrders, GetByCustomer~~ (đã xong) |
| ~~PurchaseOrderService~~ | ~~Create(handler), Approve, ReceiveGoods, Cancel~~ (đã xong 2026-07-03 — thực chất là sửa stub rỗng) | ~~List, GetById~~ (đã xong) |
| WareHouseService | ReceiveStock, AdjustStock, TransferStock, LocationCRUD | StocktakeQuery |

### Nhóm 3: Event consumers — ĐÃ SỬA (2026-07-02)

> Tài liệu cũ liệt kê nhóm này là "chưa consume events". Rà soát lại code thật cho
> thấy vấn đề còn sâu hơn: hầu hết các class handler này **đã tồn tại từ trước**
> (implement `INotificationHandler<T>`/`IEventHandler<T>` đầy đủ) nhưng **không có
> consumer thật nào gọi `Publish`/`HandleAsync`** — tức là code "trông như đã xong"
> nhưng chưa từng chạy. Việc này đã được sửa: mỗi service dưới đây giờ có 1 Kafka
> consumer thật (mirror `HRMService.MasterDataEventConsumer`) và/hoặc 1 RabbitMQ
> consumer thật (mirror `WareHouseService.SalesOrderRabbitMqConsumer`, cho các event
> từ SaleOrderService — service duy nhất publish qua RabbitMQ thay vì Outbox+Kafka).

| Service | Events đã wire (2026-07-02) | Cơ chế |
|---------|------------------------------|--------|
| WareHouseService | `sales_order.SalesOrderCreated/Cancelled/Shipped`, `purchasing-topic` PO Created | RabbitMQ + Kafka |
| PaymentService | `sales_order.SalesOrderCreatedEvent` | RabbitMQ |
| ShippingService | `sales_order.SalesOrderApprovedEvent`, `warehouse_exchange` InventoryReserved | RabbitMQ (cả hai) |
| AccountingService | `sales_order.SalesOrderCreatedEvent`, `purchasing-topic`, `payment-topic` | RabbitMQ + Kafka |
| CRMService | `identity-topic` UserRegistered, `payment-topic`, `sales_order.SalesOrderCreatedEvent` | Kafka + RabbitMQ |
| POSService | `product-topic` ProductCreated, `promotion-topic` PromotionCreated | Kafka |
| PromotionService | `product-topic` ProductCreated, `sales_order.SalesOrderCreatedEvent` | Kafka + RabbitMQ |
| HRMService | `mst-topic` (đã có sẵn từ trước, không đổi) | Kafka |
| NotificationService | `identity-topic` (đã có sẵn từ trước, không đổi) | Kafka |
| ReportingService | Sales/Payment/Inventory topics (đã có sẵn từ trước — tài liệu cũ ghi nhầm là thiếu) | Kafka |

**Còn thiếu, chưa làm trong đợt này:** ReportingService chưa consume POS/HR events
(chưa có `FactPOSTransactions`/`DimEmployees` model dù bảng SQL đã có); ProductService
**đã publish** `ProductUpdated`/`ProductPriceChanged` từ 2026-07-03 (mục 3.2), nhưng
POS/Promotion's consumer vẫn đọc theo field cũ (giá mặc định 0) — cần cập nhật 2
consumer đó để đọc field giá thật từ `ProductPriceChangedDomainEvent`, chưa làm ở đợt
này; `OrderFulfillmentSaga` (SaleOrderService) vẫn là dead code, chưa quyết định giữ
lại làm true orchestration hay bỏ hẳn để dùng choreography qua các consumer mới.

### Nhóm 4: Thiếu external integrations

| Integration | Cần cho | Ưu tiên |
|------------|---------|---------|
| Payment Gateway (VNPay/Stripe/MoMo) | PaymentService | Cao |
| Email Provider (SendGrid/SES) | NotificationService | Cao |
| SMS Provider (Twilio/VIETTEL) | NotificationService | Cao |
| FCM/APNS Push | NotificationService | Cao |
| Carrier API (GHN/GHTK/FedEx) | ShippingService | Cao |
| ~~Full-text Search (Elasticsearch)~~ | ~~ProductService~~ | **Đã triển khai** (backend) |

---

## 7. Thứ Tự Ưu Tiên Bổ Sung

### Sprint 1 — Hoàn Thiện Core Flow (E-Commerce)

```
1. ~~SaleOrderService: ApproveCommand, CancelCommand, ListOrdersQuery, Controller đầy đủ~~
   (xác nhận đã có từ trước 2026-07-03); ~~UpdateOrderCommand, coupon, partial
   fulfillment, return flow, audit trail~~ (đã xong 2026-07-03) — xem mục 3.3
2. ~~PurchaseOrderService: Command handlers~~ (đã xong 2026-07-03 — 7/8 handler
   chính là stub rỗng, đã viết lại thật); ~~ReceiveGoodsCommand~~ (đã xong) — xem
   mục 3.4. Event consumers: không cần thêm ở PurchaseOrderService, nhưng
   WarehouseService cần tự thêm consumer cho `PurchaseOrderReceivedEvent` (mục 3.5)
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
12. ProductService: ~~Full-text search~~ (đã xong), ~~bulk import~~ (đã xong
    2026-07-03, CSV), ~~event publishing đầy đủ~~ (đã xong 2026-07-03 —
    ProductUpdated/ProductPriceChanged) — xem mục 3.2
13. POSService: Event consumers, Terminal management, offline sync
14. IdentityService: ~~2FA~~ (đã xong), ~~audit log~~ (đã xong 2026-07-03),
    ~~session management~~ (đã xong 2026-07-03) — xem mục 3.1
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
