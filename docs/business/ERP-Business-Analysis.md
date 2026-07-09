# ERP E-Commerce Solution — Phân Tích Business Logic Toàn Diện

> **Cập nhật:** 2026-07-08 — xem block "Cập nhật 2026-07-08" bên dưới cho đợt mới
> nhất (Tenant Isolation & Permission Model + Security Hardening 3 tầng cho
> IdentityService). Lịch sử trước đó (2026-07-05 trở về trước): xử lý tuần tự từng
> service theo mục ❌ liệt kê (theo yêu cầu "từng service một, theo thứ tự"): đã xong
> 3.1 → 3.2 → 3.3 → 3.4 → 3.5 → 3.6 → 3.7 → 3.8 → 3.9 (Promotion) → 3.10 (CRM) → 3.11
> (HRM) → 3.12 (Accounting) → 3.13 (Notification) → 3.14 (Reporting).
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
> - **3.5 Warehouse** — phát hiện một **crash-risk bug** độc lập với mục ❌: aggregate
>   `Reservation.ReservedItems` (`List<ReservedItem>`) chưa từng được cấu hình trong
>   `WarehouseDbContext` — EF Core không thể tự suy luận owned collection này, lẽ ra sẽ
>   ném lỗi ngay lần đầu đọc/ghi Reservation; đã sửa bằng `OwnsMany`. Ngoài ra
>   constructor của `Reservation` có tham số `items` không khớp tên property
>   `ReservedItems` nên EF không bind được — thêm constructor rỗng private để EF dùng
>   property-setter thay vì constructor-binding. Toàn bộ 12 mục ❌ đã xử lý thật: 
>   `ReceiveStockCommand` + Kafka consumer thật cho `PurchaseOrderReceivedDomainEvent`
>   (khớp SKU, không tự tạo Inventory ảo nếu không khớp), Location CRUD, Stocktake
>   (kiểm kê vật lý, tự động ghi nhận chênh lệch thành StockMovement), Transfer Order,
>   Lot/Batch + hạn sử dụng (`InventoryLot`, FIFO/LIFO depletion khi bán), Serial
>   number tracking (`InventorySerial`), `AdjustStockCommand` riêng biệt với reason
>   code, publish `ReceiptCompleted`/`StockAlert` events, và
>   `GetInventoryValuationQuery` (FIFO/LIFO/Average dựa trên cost layers thật).
> - **3.6 Payment** — phát hiện nghiêm trọng nhất đợt này (khác dạng so với 3.4/3.5):
>   webhook VNPay **verify chữ ký HMAC-SHA512 thật** nhưng sau khi verify xong **không
>   hề gọi mediator** — `PaymentAuthorizedEvent` (mục ❌ gốc tưởng là thiếu, thực ra đã
>   có từ trước) không bao giờ được raise trong thực tế vì không có gì kích hoạt
>   `Authorize()`; một giao dịch VNPay hợp lệ, đã xác thực đúng, không hề cập nhật gì
>   trong DB. Cũng phát hiện `Payment.Fail()`/`Refund.MarkAsCompleted`/`MarkAsFailed`
>   là dead code (không ai gọi) nên refund luôn kẹt ở `Pending`. Đã sửa: thêm
>   `TransactionRef` + `ProcessGatewayCallbackCommand` idempotent nối webhook → domain
>   thật; `IPaymentGateway.RefundAsync` (VNPay refund API thật) + sửa luôn lỗi domain
>   logic liên quan (Status set lạc quan trước khi gateway xác nhận). Ngoài ra sửa 1
>   vi phạm Clean Architecture độc lập với ❌ gốc: `IPaymentGateway` từng định nghĩa
>   trong Infrastructure dù Application cần dùng nó — đã chuyển sang Application làm
>   port đúng chuẩn. Bổ sung SavedPaymentMethod, InstallmentPlan, reconciliation
>   report (dựa trên `SettlementId` gán thật lên từng payment thay vì chỉ đối chiếu
>   theo khoảng ngày), InvoiceCreated consumer, fraud flag theo ngưỡng giao dịch lớn.
> - **3.7 Shipping** — GHN xác nhận là tích hợp thật (không phải claim sai), nhưng
>   `CreateShipmentCommandHandler` **chưa từng gọi carrier client** — Shipment tạo
>   xong không có tracking number thật cho tới khi gọi tay
>   `AssignTrackingNumberCommand` riêng; `Carrier.CalculateRate` cũng chưa từng được
>   gọi (endpoint fee đi thẳng qua GHN, bỏ qua domain). Phát hiện thêm 1 crash-risk
>   độc lập với ❌ gốc: `GhnCarrierClient` cần `IHttpClientFactory` client "GHN" nhưng
>   `AddHttpClient` chưa từng được gọi trong service — DI sẽ crash đúng lúc GHN được
>   cấu hình thật. Đã sửa cả ba + cấu trúc lại địa chỉ giao hàng thành field có cấu
>   trúc (Ward/District/Province, khớp SQL gốc và khớp field GHN cần), thêm RMA
>   (`ReturnShipment` — aggregate riêng cho reverse logistics), POD, PDF label thật
>   (PdfSharp), bulk tracking, retry delivery, và sửa vi phạm Clean Architecture độc
>   lập (`ICarrierClient` chuyển từ Infrastructure sang Application).
> - **3.8 POS** — 2/13 mục ❌ gốc hoá ra đã có sẵn và hoạt động thật (Consume
>   ProductCreated/PromotionCreated). Phát hiện nghiêm trọng nhất, độc lập với ❌ gốc:
>   service này **hoàn toàn không có `OutboxProcessor`** — domain event ghi đúng vào
>   `OutboxMessages` nhưng không job nào từng đẩy lên Kafka, nên "real-time inventory
>   deduction" (mục ❌ #13) không thể hoạt động dù có sửa gì phía consume vì sự kiện
>   không bao giờ rời khỏi DB của POS. Đã thêm Outbox+Kafka (`pos-topic`) — Warehouse
>   vẫn cần tự thêm consumer khi rà soát tới, giống pattern 3.4→3.5. Cũng nối
>   `ProductPriceChangedForPosHandler` (đã viết thật nhưng chưa từng đăng ký, vì
>   ProductService lúc đó chưa có event tương ứng — nay đã có từ mục 3.2). Sửa 2 lỗi
>   schema thật (không chỉ thiếu cột): `TransactionItem.LineTotal`/`TaxAmount` là
>   computed property (EF bỏ qua khi ghi) trong khi SQL là cột NOT NULL thật, và
>   `CashierId` sai kiểu (NVARCHAR vs Guid). Bổ sung Terminal entity (bảng SQL có sẵn
>   từ đầu, không C# nào dùng), split payment, đếm tiền theo mệnh giá, Z-report,
>   customer display, receipt text, offline sync batch.
>
> Mục 6 (Gap Analysis, event-driven backbone toàn hệ thống) đã cập nhật ở lần
> 2026-07-02. **3.14 (Reporting) đã xử lý 2026-07-05** — xem chi tiết bên dưới.
> - **3.14 Reporting** — 7/10 mục ❌ là claim sai (3 Kafka consumers, tất cả fact
>   tables, DimDate, ETL inline, sales/payment queries đã tồn tại và hoạt động thật).
>   Thực hiện: (a) sửa bug `TotalProcessed` — filter `"Captured"` → `"Processed"`;
>   (b) sửa topic names sai — "payment-processed"/"inventory-updated" →
>   "payment-topic"/"warehouse-topic"; (c) thay `GetInventoryReportAsync` stub cứng
>   bằng aggregation thật từ `FactInventoryMovements` với join `DimProduct` cho tên sản
>   phẩm; (d) thêm `FactPOSTransaction` + `FactHRPayslip` + `DimProduct` domain models;
>   (e) thêm `POSEventConsumer` (pos-topic) + `HRMEventConsumer` (hrm-topic) +
>   `ProductEventConsumer` (product-topic) — tất cả dùng `$type` substring dispatch
>   với `JDoc` helper để parse outbox JSON an toàn; (f) `HRDashboardDto` /
>   `POSDashboardDto` + queries/handlers; (g) GET /reports/hr, GET /reports/pos,
>   GET /reports/sales/export (CSV), GET /reports/payments/export (CSV).
> - **3.13 Notification** — 8/10 mục ❌ là claim sai (SMTP/Twilio/FCM/template
>   substitution/scheduling/unsubscribe đã có sẵn và hoạt động thật). Thực hiện: (a)
>   sửa retry exponential backoff — `ShouldRetryNow()` thêm vào domain entity, 
>   `NotificationDispatchJob` nay gọi `GetFailedForRetryAsync()` với backoff 30s/60s;
>   (b) Bulk notification — `BulkSendNotificationCommand` + handler + `POST /bulk`;
>   (c) Webhook delivery outbound — `WebhookEndpoint` entity, `WebhookSender`
>   (HMAC-SHA256), `WebhooksController` 6 endpoints, tự động fire sau mỗi lần gửi;
>   (d) Digest emails — bỏ qua (LOW priority, phức tạp aggregation window).
> Xem `docs/architecture/end-to-end-request-flow.md` để trace kỹ thuật
> UI→API cho các luồng chính.
>
> **Cập nhật 2026-07-08 — IdentityService, 2 đợt riêng biệt (bảo mật dữ liệu user):**
> - **Đợt 1 (Tenant Isolation & Permission Model):** phát hiện lỗ hổng IDOR sống —
>   `UsersController`/`RolesController` (`GetById`, `Update`, `Lock/Unlock`, `Delete`,
>   `AssignPermission`, `List`) không kiểm tra tenant, cho phép user thuộc Tenant A
>   xem/sửa/khoá user hoặc role thuộc Tenant B chỉ bằng cách đổi GUID/tham số. Đã sửa
>   toàn bộ theo mô hình 404 (không phải 403) khi tài nguyên thuộc tenant khác — tránh
>   lộ thông tin "tồn tại hay không". Phát hiện thêm chuỗi bug dữ liệu độc lập:
>   `RoleIds.FromCode()` trả về GUID cứng không khớp `Roles.Id` thật (mọi lần gán role,
>   kể cả tự đăng ký, đều vi phạm FK) — đã xoá hẳn, dùng `role.Id` thật;
>   `RolePermission.TenantId` là cột ma (EF map nhưng SQL không có) gây lỗi 500 sống
>   mỗi lần gán/gỡ permission cho role — đã xoá property; `UserRole`'s khoá composite
>   EF sai (thiếu TenantId) so với PK SQL thật; `UserService.RemoveRoleAsync` là no-op
>   (tìm thấy row nhưng không xoá) — đã sửa cả 4. `AuthService.RegisterAsync` trước
>   đây chỉ kiểm tra email trùng trong phạm vi tenant hiện tại, không phát hiện email
>   đã tồn tại ở tenant khác (vi phạm unique index toàn cục, crash SQL thô thay vì lỗi
>   sạch) — đã sửa dùng kiểm tra global + thêm `POST /api/users/invite` (gated
>   `identity:users:manage`) để admin gắn 1 tài khoản đã tồn tại vào tenant mới, theo
>   đúng mô hình "1 User dùng chung nhiều tenant qua `AuthUserTenant`" đã xác nhận với
>   người dùng. Đổi toàn bộ hằng số permission từ `snake_case` phẳng
>   (`manage_users`...) sang dạng phân cấp `identity:{resource}:{action}` khớp quy ước
>   `PermissionConstants.cs` dùng ở 13 service còn lại. Xây 2 trang admin frontend mới:
>   Users (gán/gỡ role, khoá/mở khoá, mời user vào tenant, chọn tenant nếu có
>   `identity:tenants:view`) và Roles & Permissions (CRUD role, gán quyền theo
>   checklist, danh mục quyền chỉ đọc).
> - **Đợt 2 (Security Hardening 3 tầng, sau khi người dùng hỏi "đã đảm bảo bảo mật dữ
>   liệu user chưa"):** audit phát hiện lỗ hổng nghiêm trọng nhất hệ thống — **2FA có
>   thể bị bỏ qua hoàn toàn** qua đường external-login (Google/Microsoft/Facebook) và
>   Twitter callback, vì 2 luồng này cấp token thẳng mà không hề kiểm tra
>   `TwoFactorEnabled` như luồng `/login` thường — đã sửa bằng cách gộp logic kiểm tra
>   2FA của `LoginAsync` thành 1 helper dùng chung (`IssueTokensOrChallengeAsync`) cho
>   cả 3 luồng. **Tier 1 (IdentityService, tự chứa):** JWT signing secret hardcode
>   trong git → chuyển sang biến môi trường; refresh-token hết hạn sau 30 ngày dù cấu
>   hình ghi 7 ngày (code bỏ qua config) — đã sửa đọc từ config; token reset-password
>   lưu plaintext trong DB → hash SHA-256 trước khi lưu; thêm FluentValidation (theo
>   đúng pattern `AddFluentValidationAutoValidation` đã dùng ở PurchaseOrderService,
>   không phải MediatR pipeline vì Identity không dùng MediatR); thêm CORS allow-list +
>   HTTPS redirection/HSTS + security headers (trước đây hoàn toàn không có); thêm
>   chống brute-force đăng nhập (nối vào 1 hàm đếm login fail đã viết sẵn nhưng chưa ai
>   gọi); API key `Scopes` được lưu nhưng chưa từng kiểm tra khi dùng key — đã thêm
>   enforcement; password policy thêm yêu cầu độ phức tạp (hoa/thường/số/ký tự đặc
>   biệt) và áp dụng cả lúc đăng ký (trước đây chỉ áp dụng lúc đổi/reset mật khẩu).
>   **Tier 2 (vệ sinh secrets toàn hạ tầng):** mật khẩu SQL Server/RabbitMQ/JWT key
>   hardcode plaintext trong `docker-compose.yml` (~30 chỗ) và `deployment/k8s/
>   secrets.yaml` — chuyển sang biến môi trường (`.env.example` mẫu, `.env` thật
>   gitignore); file k8s đổi thành `secrets.yaml.example` với placeholder.
>   **Tier 3 (lỗ hổng kiến trúc nghiêm trọng nhất, xuyên suốt toàn hệ thống):** Gateway
>   xác thực chữ ký JWT đúng cách, nhưng **13 service còn lại (tất cả trừ Identity)
>   không hề tự validate JWT** — chỉ tin các header `X-User-Id`/`X-Tenant-Id`/
>   `X-User-Roles`/`X-User-Permissions` do Gateway gắn vào, không kiểm tra chữ ký nào
>   cả. Bất kỳ ai truy cập trực tiếp vào 1 service (bỏ qua Gateway, ví dụ trong cùng
>   mạng docker) có thể giả mạo header và mạo danh bất kỳ user/tenant/role/permission
>   nào. Đã xây `AddErpJwtAuthentication` + `JwtClaimsUserContextService` dùng chung
>   (`src/Shared/Authorization/`) và áp dụng cho **toàn bộ 13 service** (SaleOrderService
>   và AccountingService còn có middleware nội bộ tự viết trùng lặp, đọc header trực
>   tiếp — đã viết lại để đọc từ `HttpContext.User` đã xác thực thay vì header). Giới
>   hạn còn lại đã ghi nhận rõ, chưa xử lý: việc thu hồi token (`TokenBlacklist`) vẫn
>   chỉ Identity biết, service khác xác thực chữ ký/hạn dùng nhưng không biết token đã
>   bị logout — cần một kho thu hồi dùng chung (Redis) hoặc event logout qua
>   Kafka/RabbitMQ ở đợt sau. Ngoài phạm vi bảo mật, phát hiện thêm 1 lỗi build tồn tại
>   từ trước (không liên quan bảo mật): 10 service pin `Confluent.Kafka` phiên bản cũ
>   (2.3.0) xung đột với `ERP.Shared`'s yêu cầu mới hơn (2.13.1) — đã nâng cấp đồng bộ
>   để tất cả 16 project (Gateway + Identity + 14 service) build sạch 0 lỗi.
>
> **Cập nhật 2026-07-05:** xử lý tuần tự từng service theo mục ❌ liệt kê (theo yêu
> cầu "từng service một, theo thứ tự"): đã xong 3.1 → 3.2 → 3.3 → 3.4 → 3.5 → 3.6 →
> 3.7 → 3.8 → 3.9 (Promotion) → 3.10 (CRM) → 3.11 (HRM) → 3.12 (Accounting) → 3.13
> (Notification) → 3.14 (Reporting).

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
| IdentityService | ✅ (+ AuthUserTenant invite flow — 2026-07-08) | ✅ | ✅ (+ audit log, session mgmt, API keys, password policy — 2026-07-03; + tenant-scoped repos, hashed reset tokens, brute-force guard, hierarchical permission codes — 2026-07-08) | ✅ (+ Security/ApiKeys/Sessions endpoints; + tenant IDOR closed trên Users/Roles controllers, FluentValidation, CORS/HSTS/security headers — 2026-07-08) | — (upstream) | **99%** (LDAP/AD vẫn cố ý bỏ qua; đã đóng lỗ hổng IDOR + 2FA-bypass + JWT trust boundary xuyên suốt 13 service khác — xem mục 3.1 và ghi chú 2026-07-08 ở đầu tài liệu) |
| SaleOrderService | ✅ (+ notes, status history, partial fulfillment, return flow) | ✅ Đầy đủ lifecycle + Update/Notes/ShipItems/Return (2026-07-03) | ✅ RabbitMQ publish trực tiếp (không dùng Outbox) | ✅ 16 endpoints | ✅ (Kafka `product-topic` ProductPriceChanged, mới thêm; `OrderFulfillmentSaga` vẫn dead code) | **93%** |
| PurchaseOrderService | ✅ (+ SupplierRating, GoodsReceipt entities mới) | ✅ Đầy đủ VÀ THẬT (2026-07-03: 7/8 handler chính trước đó là stub rỗng — xem mục 3.4) | ✅ Outbox+Kafka (`purchasing-topic`); GRN/receipt-schedule/price-comparison/3-way-match mới thêm | ✅ 2 controllers, 20 endpoints (đã xoá 1 controller trùng route gây lỗi `AmbiguousMatchException`) | ✅ (outbound thật; chưa cần consume gì) | **95%** |
| WareHouseService | ✅ (+ InventoryLot, InventorySerial, TransferOrder, Stocktake, sửa crash-risk EF mapping thiếu của `Reservation.ReservedItems` — 2026-07-03) | ✅ Đầy đủ (2026-07-03: ReceiveStock, AdjustStock riêng biệt, Location CRUD, Stocktake, Transfer Order, Lot/Serial/Expiry, Valuation FIFO/LIFO/Average — xem mục 3.5) | ✅ RabbitMQ publish đã sửa lỗi thiếu `IConnection` DI (trước đây crash khi gọi); consumer thật cho `sales_order_events` + `purchasing-topic` (PO received, mới 2026-07-03) | ✅ 7 controllers, ~30 endpoints | ✅ (RabbitMQ `sales_order.*` + Kafka `purchasing-topic` PurchaseOrderReceived, mới 2026-07-03) | **97%** |
| AccountingService | ✅ (+JournalLine, TaxRate, BankAccount, BankStatement) | ✅ 15 commands, 21 queries kể cả TrialBalance/P&L/BalanceSheet/Tax/Bank | ✅ Outbox+Kafka; warehouse-topic mới thêm | ✅ 8 controllers, ~30 endpoints | ✅ (RabbitMQ `sales_order.*` + Kafka `purchasing/payment/warehouse-topic`) | **97%** |
| CRMService | ✅ (+ Activity aggregate: Call/Email/Meeting/Note/Task; ContactTag với unique index; ActivityCreatedEvent/ActivityCompletedEvent — 2026-07-04) | ✅ Đầy đủ (18 commands, 14 queries — thêm Activity + Tag commands/queries + handlers; SalesOrderCreatedEventHandler nay tạo Activity thật thay vì chỉ log — 2026-07-04) | ✅ Outbox+Kafka; ActivityRepository + ContactTagRepository mới; ContactRepository.GetByIdsPagedAsync mới | ✅ 5 controllers, ~30 endpoints (ActivitiesController mới; ContactsController + 5 tag endpoints mới — 2026-07-04) | ✅ (Kafka `identity-topic`/`payment-topic` + RabbitMQ `sales_order.*` — SalesOrderCreated nay ghi Activity thật) | **97%** |
| HRMService | ✅ (+ AttendanceRecord với CheckIn/CheckOut/OT, WorkShift — 2026-07-04; EmployeeSnapshot + Email/EmployeeNumber; PaySlip + OvertimeHours/OvertimePay) | ✅ Đầy đủ (+ CheckIn/CheckOut/MarkAbsent + 3 work shift commands + 2 employee snapshot queries + attendance queries; ProcessPayroll nay tích hợp OT từ AttendanceRecord thật — 2026-07-04) | ✅ AttendanceRepository + WorkShiftRepository mới; EmployeeSnapshotRepository.GetPagedAsync; EF unique index (EmployeeId, Date) — 2026-07-04 | ✅ 5 controllers, ~22 endpoints (AttendanceController, WorkShiftsController, EmployeesController mới — 2026-07-04) | ✅ (Kafka `mst-topic` — `MasterDataEventConsumer` nay sync Email+EmployeeNumber vào snapshot) | **96%** |
| ProductService | ✅ (+ EAV attributes, dimensions, bundles, relations, reviews) | ✅ đầy đủ (mục cũ ghi "Features folder" thiếu là sai) | ✅ (+ Elasticsearch search, stock sync từ Warehouse) | ✅ 10 controllers | ✅ (RabbitMQ `warehouse.InventoryUpdatedEvent`, mới thêm — publish-side vẫn không consume gì khác) | **93%** |
| PaymentService | ✅ (+ TransactionRef, InvoiceId, SettlementId, SavedPaymentMethod, InstallmentPlan — 2026-07-03) | ✅ Đầy đủ VÀ THẬT (2026-07-03: webhook verify chữ ký thật nhưng trước đây không gọi mediator — Authorize/Capture/Fail không bao giờ chạy; đã nối `ProcessGatewayCallbackCommand` thật, idempotent) | ✅ VNPay thật (+ `RefundAsync` mới); `IPaymentGateway` port chuyển sang Application (sửa vi phạm Clean Architecture); Outbox đã đúng từ trước | ✅ 5 controllers (kể cả webhook/IPN nay thực sự cập nhật Payment) | ✅ (RabbitMQ `sales_order.SalesOrderCreatedEvent`; Kafka `accounting-topic` InvoiceCreated, mới 2026-07-03) | **97%** |
| ShippingService | ✅ (+ địa chỉ có cấu trúc, ReturnShipment/RMA, POD fields, DeliveryAttempts — 2026-07-03) | ✅ Đầy đủ VÀ THẬT (2026-07-03: CreateShipment nay gọi GHN thật để lấy tracking; MarkDeliveryFailed/RetryDelivery/bulk-tracking/label PDF nối thật — trước đây là domain method chết) | ✅ GHN thật (+ sửa crash-risk thiếu `AddHttpClient("GHN")`); `ICarrierClient` port chuyển sang Application; Outbox đã đúng từ trước | ✅ 3 controllers (+ ReturnShipments, label PDF, carrier quote) | ✅ (RabbitMQ `sales_order.SalesOrderApprovedEvent` + `warehouse_exchange` InventoryReserved nay có logic thật thay vì chỉ log) | **96%** |
| POSService | ✅ (+ Terminal, TransactionTender, CashDenominationLine — 2026-07-03) | ✅ Đầy đủ (2026-07-03: split payment, denomination counting, Z-report, customer display, receipt text, offline sync batch; `GetShiftSummaryQuery` xác nhận đã thật từ trước, không phải crash) | ✅ Outbox+Kafka (`pos-topic`) **mới thêm hoàn toàn** — trước đây không tồn tại dù DbContext đã ghi outbox đúng | ✅ 5 controllers | ✅ (Kafka `product-topic`/`promotion-topic` xác nhận thật từ trước; `payment-topic` PaymentProcessed + ProductPriceChanged, mới nối 2026-07-03) | **95%** |
| PromotionService | ✅ FlashSale entity + FlashSaleItem (2026-07-04), PromotionScope (Order/Product/Category), BuyXGetY, GenerateBulkCoupons, GetApplicablePromotions rules engine, LoyaltyTransaction history, PromotionSchedulerJob | ✅ | ✅ Outbox+Kafka (`promotion-topic`) | ✅ 3 controllers (Promotions/Loyalty/FlashSales) | ✅ (Kafka `product-topic` + RabbitMQ `sales_order.*`) | **98%** |
| NotificationService | ✅ (+ WebhookEndpoint — 2026-07-04) | ✅ (+ BulkSendNotificationCommand, WebhookCommands/Handlers, ShouldRetryNow() domain logic — 2026-07-04) | ✅ SMTP thật, Twilio/FCM thật; + WebhookSender HMAC-SHA256; NotificationDispatchJob nay thật sự retry với exponential backoff — 2026-07-04 | ✅ 4 controllers (+ WebhooksController 6 endpoints, POST /notifications/bulk — 2026-07-04) | ✅ (chỉ `identity-topic` live — các topic khác vẫn là handler chết cho tới khi Kafka consumer tương ứng được thêm, xem mục 6) | **95%** |
| ReportingService | ⚠️ Không có domain entity giàu hành vi, nhưng có model/DbSet thật khớp schema OLAP (+ `FactPOSTransaction`, `FactHRPayslip`, `DimProduct` mới thêm — 2026-07-05) | ✅ 5 queries thật (+ GetHRDashboard, GetPOSDashboard — 2026-07-05; InventoryReport nay là aggregation thật thay vì stub) | ✅ 6 Kafka consumers (+ POSEventConsumer, HRMEventConsumer, ProductEventConsumer — 2026-07-05); sửa topic names sai ("payment-processed"→"payment-topic", "inventory-updated"→"warehouse-topic") | ✅ 1 controller, 7 endpoints (+ GET /hr, GET /pos, GET /sales/export, GET /payments/export — 2026-07-05) | ✅ | **93%** |

**Trung bình toàn hệ thống: ~95%** (tăng từ ~57% ban đầu sau khi (a) đối chiếu lại đánh
giá cho đúng với code thật — nhiều service đã bị đánh giá thấp hơn thực tế rất nhiều —
(b) sửa event-driven backbone thật sự cho các service, và (c) xử lý toàn bộ mục ❌ còn
lại của 14/14 service (3.1→3.14). Các phát hiện nổi bật: 3.4 — 7/8 handler chính là
stub rỗng; 3.5 — crash-risk EF mapping bug; 3.6 — webhook verify chữ ký thật nhưng
không hề cập nhật database; 3.7 — carrier API (GHN) tồn tại nhưng không trên đường đi
chính; 3.8 — không có `OutboxProcessor` nên domain event không bao giờ rời DB; 3.10
(CRM)/3.11 (HRM)/3.12 (Accounting)/3.13 (Notification) — phần lớn mục ❌ là claim sai
của tài liệu gốc, thực tế đã có sẵn. 3.14 (Reporting) — bug TotalProcessed luôn = 0,
topic names sai, stub InventoryReport; nay đã sửa + thêm HR/POS dashboard + 3 Kafka
consumers mới + CSV export.

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
  `POST /api/auth/2fa/resend`, tự bật/tắt qua `POST /api/users/{id}/2fa/enable|disable`.
  **Lỗ hổng đã sửa (2026-07-08):** đăng nhập qua external-login (Google/Microsoft/
  Facebook) và Twitter callback trước đây cấp token thẳng, không hề kiểm tra
  `TwoFactorEnabled` như luồng `/login` thường — 1 user đã bật 2FA có thể bỏ qua hoàn
  toàn bằng cách đăng nhập qua provider ngoài. Đã gộp logic kiểm tra 2FA của cả 3
  luồng vào 1 helper dùng chung (`IssueTokensOrChallengeAsync`).
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
  Identity tự validate JWT của chính nó) — `GET/POST /api/users/{id}/sessions[/{id}/revoke]`.
  **Cập nhật 2026-07-08:** trước đây các service khác chỉ tin header do Gateway inject,
  không tự validate JWT — nay cả 13 service đã tự validate chữ ký/hạn dùng JWT (xem
  "Cross-Service JWT Trust Boundary" bên dưới). **Giới hạn còn lại:** việc thu hồi
  token (`TokenBlacklist`) vẫn chỉ Identity biết — 1 token đã logout nhưng chưa hết
  hạn tự nhiên vẫn được các service khác chấp nhận cho tới khi hết hạn, vì họ chỉ
  kiểm tra chữ ký/hạn dùng chứ không tra `TokenBlacklist` của Identity. Cần kho thu
  hồi dùng chung (Redis) hoặc event logout qua Kafka/RabbitMQ để đóng nốt giới hạn này.
- API Key management: entity `ApiKey` mới (bảng `ApiKeys`, migration 003) cho service-to-service
  calls — tạo/liệt kê/thu hồi qua `POST/GET /api/api-keys`, `POST /api/api-keys/{id}/revoke`; raw
  key chỉ trả về đúng 1 lần lúc tạo, chỉ lưu hash. **Việc từng service cụ thể chấp nhận API Key
  thay/cùng với Bearer token là bước tích hợp riêng của từng service, chưa làm ở đợt này** — đợt
  này chỉ xây xong phần quản lý khoá
- Password policy: từ chối mật khẩu ngắn hơn `PasswordPolicy:MinLength` (mặc định 8) và mật khẩu
  trùng với 1 trong N mật khẩu gần nhất trong `PasswordHistories` (`PasswordPolicy:HistoryCount`,
  mặc định 5) — áp dụng ở `POST /api/auth/reset-password` và `POST /api/users/{id}/change-password`.
  **Cập nhật 2026-07-08:** thêm yêu cầu độ phức tạp (chữ hoa/chữ thường/số/ký tự đặc
  biệt, mỗi loại bật/tắt riêng qua config) và áp dụng cả lúc **đăng ký**
  (`POST /api/auth/register`) — trước đây `RegisterAsync` hoàn toàn không gọi
  `ValidateNewPasswordAsync`, chỉ 2 luồng đổi/reset mật khẩu mới kiểm tra policy
- Domain events còn thiếu đã bổ sung: `UserUpdatedDomainEvent` (raise từ `User.UpdateProfile()`),
  `RoleAssignedDomainEvent` (raise từ `User.AssignRole()`), `PermissionGrantedDomainEvent` (raise
  từ `Role.NotifyPermissionGranted()`, gọi khi gán permission cho role) — cả 3 đi qua Outbox+Kafka
  `identity-topic` giống các event khác

**Authorization (RBAC)**
- Quản lý Roles (CRUD)
- Gán Role cho User
- Quản lý Permissions (CRUD)
- Gán Permission trực tiếp cho User hoặc qua Role
- Middleware truyền UserContext qua HTTP header sang các service khác (nay đã có xác
  thực chữ ký JWT thật đứng sau — xem "Cross-Service JWT Trust Boundary" bên dưới)
- Hằng số permission đổi từ `snake_case` phẳng (`manage_users`) sang dạng phân cấp
  `identity:{resource}:{action}` (2026-07-08), khớp quy ước `PermissionConstants.cs`
  dùng ở 13 service còn lại — giảm rủi ro trùng tên khi số lượng tenant/permission tăng
- `[Authorize(Policy = "Permission")]` được thêm vào **toàn bộ 35 chỗ dùng
  `[RequirePermission(...)]`** trên 7 controller (2026-07-08) — phát hiện quan trọng:
  trước đó chỉ có `[Authorize]` trần (policy mặc định = "đã đăng nhập"), nên
  `PermissionAuthorizationHandler` tự viết sẵn **chưa từng được gọi** — bất kỳ user đã
  đăng nhập nào, bất kể có permission thật hay không, đều qua được mọi endpoint có gắn
  `[RequirePermission]`

**User Management**
- CRUD người dùng
- Kích hoạt / vô hiệu hóa tài khoản
- Xem/thu hồi phiên đăng nhập đang hoạt động (`DeviceSessions`)
- **Tenant isolation (IDOR) đã đóng (2026-07-08):** `GetById`/`UpdateProfile`/
  `Lock`/`Unlock`/`Delete`/`AssignRole`/`RemoveRole` trên `UsersController`, và
  tương tự trên `RolesController`, trước đây không kiểm tra tenant — 1 user thuộc
  Tenant A có thể xem/sửa/khoá user hoặc role thuộc Tenant B chỉ bằng cách đổi
  GUID trên URL. Nay trả về 404 (không phải 403) khi tài nguyên thuộc tenant
  khác, tránh lộ "tồn tại hay không" cho kẻ dò quét. `List` mặc định về tenant của
  người gọi; muốn xem tenant khác phải có thêm quyền `identity:tenants:view`,
  kiểm tra lại từ DB chứ không tin claim JWT

**Multi-Tenant**
- Quản lý Tenants (CRUD)
- Gán User vào Tenant
- Lọc dữ liệu theo TenantId
- **Invite-to-tenant flow (2026-07-08):** `POST /api/users/invite` (gated
  `identity:users:manage`) gắn 1 tài khoản đã tồn tại (theo email) vào tenant của
  người gọi — idempotent (`alreadyMember: true` nếu đã là thành viên), tuỳ chọn
  gán role ngay lúc mời. Đây là con đường **duy nhất** hợp lệ để 1 user tham gia
  tenant thứ 2 — tự đăng ký (`/auth/register`) với email đã tồn tại ở tenant khác
  bị từ chối sạch (409) thay vì tự động gắn vào (tránh vấn đề tiếp nhận tài khoản
  không có sự đồng ý)

**Published Events**
- `UserRegisteredIntegrationEvent` → Kafka
- `TwoFactorCodeGeneratedEvent` → Kafka (`identity-topic`) — chỉ NotificationService consume, để gửi OTP
- `UserUpdatedDomainEvent`, `RoleAssignedDomainEvent`, `PermissionGrantedDomainEvent` → Kafka
  (`identity-topic`) — mới thêm 2026-07-03; chưa có consumer nào ở service khác lắng nghe 3 event
  này, tương tự tình trạng ban đầu của `TwoFactorCodeGeneratedEvent` trước khi Notification wire nó

**Bảo mật dữ liệu user (đợt rà soát 2026-07-08, sau câu hỏi trực tiếp của người dùng
"IdentityService đã đảm bảo bảo mật dữ liệu user chưa")**
- Token reset-password (`PasswordResetTokens.Token`) trước đây lưu **plaintext** trong
  DB — nay hash SHA-256 trước khi lưu; raw token vẫn trả về/gửi email đúng 1 lần như cũ
- FluentValidation thêm cho `RegisterRequest`/`LoginRequest`/`ChangePasswordRequest`/
  `ForgotPasswordRequest`/`ResetPasswordRequest` — trước đây không có validation nào
  ngoài vài check thủ công rải rác trong domain
- CORS allow-list (đọc từ config, không phải `AllowAnyOrigin`), HTTPS redirection/HSTS,
  security headers (`X-Content-Type-Options`, `X-Frame-Options`, CSP) — trước đây
  IdentityService không có bất kỳ thứ nào trong 3 thứ này
- Chống brute-force đăng nhập: khoá tạm theo cặp (IP, email) sau N lần sai trong 1
  khoảng thời gian cấu hình được (`PasswordPolicy:MaxFailedAttemptsPerWindow`/
  `FailedAttemptsWindowMinutes`) — nối vào 1 hàm đếm login fail đã viết sẵn từ trước
  (`CountRecentFailedAsync`) nhưng chưa từng được gọi ở đâu
- API key `Scopes` (lưu từ trước) nay được kiểm tra thật khi validate key — trước đây
  bất kỳ key hợp lệ nào cũng được cấp toàn quyền bất kể scope đã cấp là gì
- JWT signing secret chuyển từ hardcode trong `appsettings.json`/`docker-compose.yml`
  sang biến môi trường (`${JWT_SIGNING_KEY}`, mẫu ở `.env.example`) — xem
  `docs/architecture/deployment-guide.md`
- **Cross-Service JWT Trust Boundary (thay đổi kiến trúc lớn nhất đợt này):** trước
  đây chỉ Gateway xác thực chữ ký JWT; 13 service còn lại chỉ tin header
  `X-User-Id`/`X-Tenant-Id`/`X-User-Roles`/`X-User-Permissions` do Gateway gắn vào —
  ai truy cập trực tiếp vào 1 service (bỏ qua Gateway) có thể giả mạo header và mạo
  danh bất kỳ ai. Đã thêm `AddErpJwtAuthentication`/`JwtClaimsUserContextService` dùng
  chung (`src/Shared/Authorization/`) và áp dụng cho toàn bộ 13 service — mỗi service
  giờ tự xác thực chữ ký/hạn dùng JWT độc lập, không còn phụ thuộc header do Gateway
  gắn vào là nguồn tin cậy duy nhất. Giới hạn còn lại: thu hồi token (logout) vẫn chỉ
  Identity biết — xem ghi chú "Session management" ở mục ✅ phía trên

#### ❌ Tính năng còn thiếu

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| ~~Xác thực 2 bước (2FA/MFA)~~ | ~~Cao~~ | **Đã triển khai** — SMS/Email OTP, xem `docs/business/Identity/ExternalAuthAndTwoFactor.md` |
| ~~Khóa tài khoản sau N lần sai mật khẩu~~ | ~~Cao~~ | **Đã sửa 2026-07-03** — method đã có từ trước nhưng chưa từng được gọi trong `LoginAsync`, nay đã wire |
| ~~Audit log đăng nhập~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — xem chi tiết ở mục ✅ phía trên |
| ~~Session management~~ | ~~Trung~~ | **Đã triển khai 2026-07-03**, giới hạn thu hồi token thật đã đóng một phần 2026-07-08 (13 service tự validate JWT) — xem ghi chú "Session management" ở mục ✅ |
| ~~API Key management~~ | ~~Trung~~ | **Đã triển khai phần quản lý (create/list/revoke) 2026-07-03; scope enforcement thêm 2026-07-08** — việc từng service chấp nhận API Key thay Bearer token vẫn chưa làm |
| ~~Password policy configuration~~ | ~~Thấp~~ | **Đã triển khai 2026-07-03; thêm yêu cầu độ phức tạp + áp dụng lúc đăng ký 2026-07-08** — chưa có UI admin cấu hình theo tenant, chỉ đọc từ `appsettings.json` |
| ~~Tenant isolation trên Users/Roles (IDOR)~~ | ~~Cao~~ | **Đã sửa 2026-07-08** — xem "Tenant isolation (IDOR) đã đóng" ở mục ✅ phía trên |
| ~~2FA có thể bị bỏ qua qua external-login~~ | ~~Cao~~ | **Đã sửa 2026-07-08** — xem ghi chú trong mục 2FA ở mục ✅ phía trên |
| LDAP/Active Directory integration | Thấp | **Cố ý bỏ qua ở đợt này** — cần thư viện LDAP client thật + một Active Directory thật để kiểm thử, không có sẵn trong môi trường này; khác các gap khác (đều chỉ cần code + schema có sẵn), đây là một loại tích hợp bên ngoài hoàn toàn mới, chưa có bất kỳ phần nào trong repo |
| Thu hồi token dùng chung giữa các service (shared token blacklist) | Trung | **Chưa làm** — mỗi service tự validate chữ ký/hạn dùng JWT (2026-07-08) nhưng không service nào (ngoài Identity) biết token đã bị logout/thu hồi trước hạn; cần kho Redis dùng chung hoặc event logout qua Kafka/RabbitMQ |
| `PermissionsController.Create/Update/Delete` không lưu (không gọi `SaveChangesAsync`) | Trung | **Biết nhưng cố ý chưa sửa 2026-07-08** — admin frontend chỉ hiển thị danh mục quyền dạng chỉ đọc, không xây CRUD trên endpoint đang lỗi này |
| Gán/thu hồi permission trực tiếp cho từng User (không qua Role) | Thấp | **Chưa có service method** — hiện chỉ hỗ trợ gán permission qua Role; `UserPermission` override trực tiếp chưa có tầng service |
| Gateway/PurchaseOrderService's `RoleDefinitions` cứng, không theo tenant | Thấp | **Cố ý ngoài phạm vi 2026-07-08** — 2 nơi này có bảng permission tính lại độc lập, không dùng RBAC theo tenant thật của Identity; là sáng kiến lớn hơn, riêng biệt |
| ~~Published: `UserUpdated`, `RoleAssigned`, `PermissionGranted`~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — xem mục Published Events phía trên; chưa có service nào consume |

#### Luồng Vận Hành

```
Client → POST /auth/register → [validate] → User entity → PublishDomainEvent
                                                      → OutboxProcessor → Kafka
                                                      → NotificationService (welcome email)
                                                      → HRMService (if employee)
                                                      → CRMService (if customer)

Client → POST /auth/login → [verify password] → [check 2FA required?]
                                              → yes: TwoFactorChallenge → 2fa/verify → tokens
                                              → no:  generate JWT + RefreshToken
                          → return {accessToken, refreshToken, expiresIn}
        (external-login/Twitter callback đi qua CÙNG bước [check 2FA required?] này
        từ 2026-07-08 — trước đó 2 luồng này bỏ qua 2FA hoàn toàn)

API Request → Gateway: [validate chữ ký JWT] → inject X-User-Id/X-Tenant-Id/
              X-User-Roles/X-User-Permissions headers (tiện cho code cũ còn đọc header)
           → forward to downstream service
           → downstream service (từ 2026-07-08): [tự validate chữ ký/hạn dùng JWT
             qua AddErpJwtAuthentication] → JwtClaimsUserContextService đọc
             HttpContext.User đã xác thực, KHÔNG còn tin header không chữ ký làm
             nguồn duy nhất — đóng lỗ hổng giả mạo header khi truy cập thẳng vào
             service, bỏ qua Gateway
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
| ~~`PurchaseOrderReceived` integration event~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** — WarehouseService nay có consumer thật (`PurchaseOrderReceivedConsumer`, xem mục 3.5, đã xử lý 2026-07-03) |
| ~~Supplier evaluation / rating~~ | ~~Thấp~~ | **Đã triển khai 2026-07-03** |
| ~~Price comparison giữa các supplier~~ | ~~Trung~~ | **Đã triển khai 2026-07-03** |
| ~~`ReceiptScheduled` event~~ | ~~Cao~~ | **Đã triển khai 2026-07-03** |
| ~~Three-way matching~~ | ~~Thấp~~ | **Đã triển khai 2026-07-03** — chỉ so khớp, không lưu trữ Invoice |

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
          → ghi GoodsReceipt → PurchaseOrderReceivedEvent
          → WarehouseService.PurchaseOrderReceivedConsumer (mục 3.5, đã xử lý 2026-07-03)

Accounting → POST /api/purchase-orders/{id}/three-way-match (truyền dòng invoice của mình)
           → so khớp PO/GRN/Invoice, trả về mismatch nếu có
```

---

### 3.5 Warehouse Service — Kho Hàng

**Mục đích:** Quản lý tồn kho, di chuyển hàng, đặt chỗ tồn kho cho đơn hàng.

> **Cập nhật 2026-07-03:** Ground-truth audit đọc toàn bộ 34 file thật (rất nhỏ so với
> các service khác) phát hiện một **crash-risk bug độc lập với danh sách ❌ gốc**:
> `Reservation.ReservedItems` (`List<ReservedItem>`) chưa từng được cấu hình trong
> `WarehouseDbContext.OnModelCreating` — EF Core không có convention nào để tự suy luận
> một owned collection trần như vậy, nên lần đầu tiên bất kỳ handler nào đọc/ghi một
> `Reservation` (tức là ngay `SalesOrderCreatedEventHandler`/`ReserveInventoryCommand` —
> luồng lõi nhất của cả service) sẽ ném lỗi ở thời điểm build model EF. Đã sửa bằng
> `OwnsMany` + shadow FK. Đồng thời, constructor `Reservation(orderId, items, holdUntil)`
> có tham số `items` không khớp tên property `ReservedItems` nên EF không thể tự bind
> bằng constructor injection — thêm constructor rỗng `private Reservation()` để EF dùng
> property-setter thay vì constructor-binding (tương tự sửa `InventoryLot` vì tham số
> `quantity` không khớp `ReceivedQuantity`/`RemainingQuantity`). Cả hai lỗi này không hề
> được liệt kê trong danh sách ❌ gốc — chỉ lộ ra khi đọc kỹ `WarehouseDbContext` cùng
> với domain entity thật.
>
> Toàn bộ 12 mục ❌ đã xử lý thật (không phải stub): `ReceiveStockCommand` (nhập kho thủ
> công + tự tạo `Inventory` nếu sản phẩm chưa có) và một Kafka consumer thật
> (`PurchaseOrderReceivedConsumer`, topic `purchasing-topic`) cho
> `PurchaseOrderReceivedDomainEvent` từ PurchaseOrderService (mục 3.4) — khớp theo
> `Inventory.ProductSku` vì event chỉ mang `ProductCode` (string), không mang
> `ProductId` (Guid) của WarehouseService; dòng nào không khớp SKU sẽ log cảnh báo và bỏ
> qua thay vì tự tạo `Inventory` với `ProductId` ngẫu nhiên không khớp với ProductService
> thật. Location CRUD (`WarehouseLocation` trước đây có entity + DbSet nhưng không một
> command/controller nào dùng tới). Stocktake/kiểm kê vật lý (snapshot số lượng hệ
> thống → nhập số đếm thực tế → hoàn tất tự động ghi `StockMovement` cho mọi dòng lệch).
> Transfer Order (chuyển kho — không đổi `TotalQuantity` toàn công ty vì `Inventory` ở
> đây là 1 pool chung theo sản phẩm chứ không theo từng location, chỉ cập nhật
> `LocationId` "nhà" và ghi `StockMovement` loại `Transferred`, trước đây định nghĩa
> nhưng chưa từng được dùng). Lot/Batch + hạn sử dụng (`InventoryLot`, FIFO/LIFO
> depletion khi bán qua `RecordStockMovementCommandHandler`). Serial number tracking
> (`InventorySerial`, trạng thái InStock/Reserved/Sold/Damaged). `AdjustStockCommand`
> riêng biệt với reason code (Damaged/Lost/Correction/Other) tách khỏi
> `RecordStockMovementCommand` chung chung trước đây. Publish `ReceiptCompletedEvent`
> và `StockAlertEvent` (dựa trên `Inventory.IsLowStock()` đã có sẵn nhưng trước đây
> không publish gì). `GetInventoryValuationQuery` (FIFO/LIFO dựa trên cost layer còn lại
> sau khi bán; Average dùng running weighted-average cost đã có sẵn trên
> `Inventory.Cost`). Xem `docs/business/FIXS-Business/ERP-Business-Analysis-Fix.md` cho
> danh sách gốc đã đối chiếu.
>
> **Giới hạn đã biết, cố ý không xử lý ở đợt này:** (1) service này chưa có
> multi-tenancy/JWT auth nào cả (không `IUserContextService`, không `[Authorize]`) —
> khác với PurchaseOrderService/ProductService; SQL schema đã có sẵn cột `TenantId` ở
> mọi bảng nhưng C# hoàn toàn không dùng tới — đây là gap kiến trúc riêng, không nằm
> trong danh sách ❌ business feature nên không tự ý thêm nửa vời. (2) bảng
> `OutboxMessages` trong SQL vẫn là schema mồ côi — service publish qua RabbitMQ trực
> tiếp (khớp với cách SaleOrderService publish, vì Warehouse consume từ SaleOrderService
> qua RabbitMQ), không dùng Outbox+Kafka nên bảng này không cần thiết cho cơ chế hiện
> tại. (3) WarehouseLocation/Inventory tổng tồn kho vẫn là 1 pool chung theo sản phẩm
> (không theo từng location riêng) — Transfer Order chỉ cập nhật vị trí "nhà" và ghi
> lịch sử, không tách kho theo địa điểm; nếu cần tồn kho tách theo location thật sự cần
> đổi khoá `Inventory` sang composite (ProductId, LocationId), ngoài phạm vi đợt này.

#### ✅ Tính năng đã có

**Domain (Aggregates)**
- `Inventory` aggregate (tồn kho theo sản phẩm; + `ProductSku`, `LocationId`,
  `ValuationMethod` — mới 2026-07-03)
- `Reservation` entity (giữ hàng cho đơn; EF owned-collection mapping đã sửa)
- `StockMovement` entity (lịch sử di chuyển)
- `WarehouseLocation` entity (vị trí trong kho; + `Name`, `Zone`, `CurrentOccupancy` —
  mới 2026-07-03, cột đã có sẵn trong SQL nhưng C# chưa dùng)
- `InventoryLot` (lot/batch + hạn sử dụng — mới 2026-07-03)
- `InventorySerial` (serial number tracking — mới 2026-07-03)
- `TransferOrder` + `TransferOrderLine` (chuyển kho — mới 2026-07-03)
- `Stocktake` + `StocktakeLine` (kiểm kê vật lý — mới 2026-07-03)

**Domain Events**
- `InventoryReservedEvent`
- `InventoryUpdatedEvent`
- `StockMovementRecordedEvent` (+ `Reference` field — mới 2026-07-03)
- `StockAlertEvent` (mới 2026-07-03, publish khi `Inventory.IsLowStock()`)
- `ReceiptCompletedEvent` (mới 2026-07-03)
- `TransferOrderCompletedEvent` (mới 2026-07-03)
- `StocktakeCompletedEvent` (mới 2026-07-03)

**Integration Event Consumers**
- `SalesOrderCreatedEventHandler` → Reserve inventory (RabbitMQ, có sẵn)
- `SalesOrderCancelledEventHandler` → Release reservation (RabbitMQ, có sẵn)
- `SalesOrderShippedEventHandler` → Deduct actual stock (RabbitMQ, có sẵn)
- `PurchaseOrderReceivedConsumer` → nhập kho thật qua `ReceiveStockCommand` (Kafka
  `purchasing-topic`, mới 2026-07-03 — thay thế `PurchaseOrderCreatedEventHandler` cũ,
  vốn là dead code không consumer nào gọi tới và ghi vào `IReceiptRepository` no-op)

**API Controllers**
- `InventoryController` (xem tồn kho, reserve/release; + `receive`, `{id}/adjust`,
  `{id}/valuation` — mới 2026-07-03)
- `StockMovementController` (lịch sử di chuyển)
- `LocationsController` (Location CRUD — mới 2026-07-03)
- `LotsController` (lot theo sản phẩm, lot sắp hết hạn — mới 2026-07-03)
- `SerialsController` (serial theo sản phẩm, đổi trạng thái — mới 2026-07-03)
- `TransferOrdersController` (create/ship/complete/cancel — mới 2026-07-03)
- `StocktakesController` (start/record-count/complete/cancel — mới 2026-07-03)

#### ❌ Tính năng còn thiếu

*(Toàn bộ 12 mục ❌ gốc đã xử lý — xem changelog phía trên. Không còn mục nào.)*

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
  → nếu ValuationMethod != Average: deplete InventoryLot (FIFO/LIFO)
→ InventoryUpdatedEvent (+ StockAlertEvent nếu IsLowStock())

[Event: PurchaseOrderReceived — mục 3.4, mới 2026-07-03]
→ PurchaseOrderReceivedConsumer (Kafka purchasing-topic)
→ khớp từng dòng theo Inventory.ProductSku == ProductCode (event không mang ProductId)
→ ReceiveStockCommand → Inventory.ReceiveStock(qty, unitCost)
→ StockMovement.Record(IN) + InventoryLot (nếu có LotNumber) + InventorySerial (nếu có)
→ InventoryUpdatedEvent + ReceiptCompletedEvent (+ StockAlertEvent nếu vẫn thấp)

[Stocktake — mới 2026-07-03]
StartStocktakeCommand (snapshot TotalQuantity hiện tại)
→ RecordStocktakeCountCommand (nhập số đếm thực tế, nhiều lần)
→ CompleteStocktakeCommand → mọi dòng lệch (Variance != 0)
  → Inventory.AdjustStock(variance) → StockMovement.Record(Adjusted, "Stocktake variance")
  → StocktakeCompletedEvent

[Transfer Order — mới 2026-07-03]
CreateTransferOrderCommand (Draft) → ShipTransferOrderCommand (InTransit)
→ CompleteTransferOrderCommand
  → StockMovement.Record(Transferred) cho mỗi dòng (không đổi TotalQuantity)
  → Inventory.AssignLocation(toLocationId) nếu đây là location "nhà" hiện tại
  → TransferOrderCompletedEvent
```

---

### 3.6 Payment Service — Thanh Toán

**Mục đích:** Xử lý thanh toán, hoàn tiền, và quyết toán.

> **Cập nhật 2026-07-03:** Ground-truth audit đọc toàn bộ 21 file thật phát hiện
> **item cuối trong danh sách ❌ gốc là claim sai** (`PaymentAuthorizedEvent` đã tồn
> tại và được raise thật trong `Payment.Authorize()` từ trước, phân biệt rõ với
> `PaymentProcessedEvent` của `Payment.Capture()`) — nhưng phát hiện quan trọng hơn,
> **không nằm trong danh sách ❌ gốc**: webhook VNPay (`PaymentGatewayController`) đã
> **verify HMAC-SHA512 signature thật** (không phải giả) nhưng sau khi verify xong
> **không hề gọi bất kỳ command nào** — `IMediator _mediator` được inject nhưng chưa
> từng dùng trong file. Nghĩa là một giao dịch VNPay thành công, đã được xác thực chữ
> ký đúng, **không hề cập nhật `Payment.Status`, không set `AuthorizedAt`/`CapturedAt`,
> không raise domain event nào** — verify thật nhưng kết quả biến mất, đây là dạng
> "trông như đã xong" nghiêm trọng hơn cả stub handler vì phần bảo mật (chữ ký) là
> thật, chỉ riêng phần nghiệp vụ bị thiếu. Cũng phát hiện `Payment.Fail()` và
> `Refund.MarkAsCompleted()`/`MarkAsFailed()` là các domain method có sẵn nhưng
> **chưa từng được gọi ở bất kỳ đâu** (dead code) — refund trước đây luôn ở trạng
> thái `Pending` vĩnh viễn vì không ai xác nhận với gateway.
>
> Đã sửa: thêm `Payment.TransactionRef` (được gán khi tạo payment URL, để webhook có
> thể tra cứu lại đúng Payment — trước đây hoàn toàn không lưu, không có cách nào map
> callback về đúng payment một cách đáng tin cậy) và `ProcessGatewayCallbackCommand`
> làm điểm xử lý tập trung, idempotent (bỏ qua nếu payment đã ở trạng thái cuối) cho
> cả callback lẫn IPN, gọi `Authorize()` → `Capture()` khi thành công hoặc `Fail()`
> khi thất bại. Thêm `IPaymentGateway.RefundAsync` (VNPay refund API, HMAC-signed,
> `HttpClient` POST) và gọi `Refund.MarkAsCompleted`/`MarkAsFailed` thật sau khi gọi
> gateway — đồng thời sửa một lỗi domain logic liên quan: `Payment.AddRefund` set
> `Status = Refunded` lạc quan *trước khi* biết gateway có thành công hay không; thêm
> `Payment.ReconcileStatusWithConfirmedRefunds()` để trạng thái chỉ phản ánh refund đã
> được gateway xác nhận.
>
> Cũng phát hiện (không nằm trong ❌ gốc) `IPaymentGateway` được định nghĩa trong
> `ERP.PAY.Infrastructure` — vi phạm Clean Architecture vì Application cần dùng
> interface này (ví dụ `RefundPaymentCommandHandler`) nhưng Application không được
> phép reference Infrastructure. Đã chuyển interface + các record `GatewayCreateResult`/
> `GatewayVerifyResult`/`GatewayRefundResult` sang `ERP.PAY.Application.Interfaces`
> (đúng vị trí port theo Clean Architecture), Infrastructure vẫn giữ implementation
> (`VnPayGateway`/`NullPaymentGateway`).
>
> Orphaned schema: cột `Payments.GatewayRef` trong SQL gốc không khớp property C#
> `GatewayTransactionId` — nghĩa là DB tạo từ script gốc sẽ thiếu cột EF thực sự dùng.
> Đã sửa bằng `sp_rename` trong migration 002.

#### ✅ Tính năng đã có

**Domain**
- `Payment` aggregate: OrderId (unique), InvoiceId, TransactionRef, Amount, Currency,
  Method, Status, GatewayTransactionId, SettlementId, IsFlaggedForReview — các field
  in nghiêng là mới 2026-07-03
- `Refund` entity: Id, PaymentId, Amount, Reason, Status, GatewayRefundId (nay thực sự
  được set qua gateway thật, không còn `Pending` vĩnh viễn)
- `Settlement` entity: Id, Period, TotalAmount, Status
- `SavedPaymentMethod` (thẻ đã lưu — mới 2026-07-03, chỉ lưu token từ gateway/widget
  tokenize phía client, không bao giờ nhận/lưu số thẻ thật — giảm phạm vi PCI-DSS)
- `InstallmentPlan` + `InstallmentLine` (trả góp — mới 2026-07-03, theo dõi lịch trả
  góp; VNPay không có API trả góp tích hợp sẵn trong codebase này nên đây là tracking
  lịch trả, không phải một luồng thanh toán trả góp qua gateway)
- Trạng thái: Pending → Authorized → Captured → Failed/Cancelled/Refunded/PartiallyRefunded
- `PaymentEvents`: PaymentInitiated, PaymentAuthorized (đã có sẵn từ trước — mục ❌ gốc
  ghi thiếu là sai), PaymentProcessed, PaymentRefunded, PaymentFailed

**Commands**
- `InitiatePaymentCommand` (nay idempotent theo OrderId — tránh crash khi event
  `SalesOrderCreated` bị redeliver; + fraud check + `InvoiceId` optional)
- `AuthorizePaymentCommand`, `CapturePaymentCommand`, `CancelPaymentCommand`
- `AssignTransactionRefCommand` (mới 2026-07-03)
- `ProcessGatewayCallbackCommand` (mới 2026-07-03 — điểm xử lý webhook thật, idempotent)
- `LinkInvoiceCommand` (mới 2026-07-03)
- `RefundPaymentCommand` (nay gọi gateway thật + `ReconcileStatusWithConfirmedRefunds`)
- `ProcessSettlementCommand` (nay gán `SettlementId` lên từng payment — trước đây chỉ
  tính tổng, không lưu payment nào thuộc settlement nào)
- `SavePaymentMethodCommand`/`SetDefaultPaymentMethodCommand`/`RemovePaymentMethodCommand`
- `CreateInstallmentPlanCommand`/`RecordInstallmentPaymentCommand`

**Queries**
- `GetPaymentQuery`, `GetPaymentByOrderQuery`, `ListSettlementsQuery`
- `GetReconciliationReportQuery` (mới 2026-07-03 — so khớp Captured payments trong kỳ
  với `SettlementId` đã gán, trả về danh sách chưa được settle)
- `GetPaymentMethodsByCustomerQuery`, `GetInstallmentPlanByPaymentQuery`

**Event Consumers**
- `SalesOrderCreatedEventHandler` → tự động initiate payment (RabbitMQ, có sẵn)
- `InvoiceCreatedConsumer` (mới 2026-07-03, Kafka `accounting-topic`) — payment theo
  invoice (net-terms/B2B); nếu order đã có payment (từ luồng checkout) thì chỉ link
  invoice vào payment đó thay vì tạo payment thứ hai (vì `Payment.OrderId` là unique)

**Infrastructure**
- `VnPayGateway`: tạo payment URL + verify callback (HMAC-SHA512, đã có sẵn, xác nhận
  thật) + `RefundAsync` (mới 2026-07-03, HMAC-signed POST tới VNPay refund API) + guard
  currency ≠ VND trả lỗi rõ ràng thay vì âm thầm gắn nhãn sai (VNPay chỉ settle VND)
- `IPaymentGateway` port chuyển từ Infrastructure sang Application (sửa vi phạm Clean
  Architecture — mới 2026-07-03)
- `FraudDetectionService` (mới 2026-07-03) — chỉ có amount làm tín hiệu cục bộ khả dụng
  (Payment không lưu CustomerId, không cross-service call), flag giao dịch lớn để review
- Outbox+Kafka (`payment-topic`) đã hoạt động từ trước, xác nhận thật

**API Controllers**
- `PaymentsController`, `SettlementsController` (+ `GET /reconciliation` — mới 2026-07-03)
- `PaymentGatewayController` (webhook nay thực sự cập nhật Payment — mới 2026-07-03)
- `PaymentMethodsController` (mới 2026-07-03)
- `InstallmentsController` (mới 2026-07-03)

#### ❌ Tính năng còn thiếu

*(Toàn bộ 10 mục ❌ gốc đã xử lý — xem changelog phía trên. Ghi chú riêng: MoMo/
ZaloPay/Stripe vẫn chưa có implementation, chỉ VNPay — nếu cần đa gateway thật sự thì
đây là việc bổ sung tương lai, không phải "hoàn thiện tính năng còn thiếu" vì VNPay đã
là một gateway thật hoàn chỉnh. Multi-currency: domain model hỗ trợ currency tuỳ ý,
nhưng gateway duy nhất hiện có (VNPay) chỉ xử lý được VND — đây là giới hạn thật của
gateway, không phải thiếu sót có thể "sửa" mà không thêm gateway khác.)*

#### Luồng Vận Hành

```
[Event: SalesOrderCreated]
→ SalesOrderCreatedEventHandler
→ InitiatePaymentCommand (idempotent theo OrderId)
→ Payment.Initiate(orderId, amount, method) → status: Pending → PaymentInitiatedEvent

Customer → POST /api/payment-gateway/{id}/create-url
→ VnPayGateway.CreatePaymentUrlAsync() → AssignTransactionRefCommand (lưu TxnRef)
→ redirect khách tới VNPay checkout

VNPay → GET /callback hoặc POST /ipn (sau khi khách thanh toán)
→ VerifyCallbackAsync (HMAC-SHA512 — xác thực thật)
→ ProcessGatewayCallbackCommand (tra Payment theo TransactionRef, idempotent)
  → nếu IsPaid: Payment.Authorize() → PaymentAuthorizedEvent
              → Payment.Capture() → PaymentProcessedEvent → Outbox → Kafka payment-topic
  → nếu !IsPaid: Payment.Fail(reason) → PaymentFailedEvent

[Event: InvoiceCreated — AccountingService, mới 2026-07-03]
→ InvoiceCreatedConsumer (Kafka accounting-topic)
→ nếu order đã có payment: LinkInvoiceCommand
→ nếu chưa: InitiatePaymentCommand (Method=BankTransfer, InvoiceId gán sẵn)

Customer → POST /api/payments/{id}/refund
→ RefundPaymentCommand → Payment.AddRefund() (đặt Status lạc quan)
→ VnPayGateway.RefundAsync() (gọi thật)
→ Refund.MarkAsCompleted()/MarkAsFailed() → Payment.ReconcileStatusWithConfirmedRefunds()
→ PaymentRefundedEvent

Accountant → POST /api/settlements (ProcessSettlementCommand)
→ gán SettlementId lên từng Captured payment trong kỳ
→ GET /api/settlements/reconciliation?periodStart=...&periodEnd=...
  → so khớp Captured payments vs SettlementId đã gán → liệt kê payment chưa settle
```

---

### 3.7 Shipping Service — Vận Chuyển

**Mục đích:** Tạo lô giao hàng, theo dõi vận chuyển, xác nhận giao hàng.

> **Cập nhật 2026-07-03:** Ground-truth audit đọc toàn bộ 20 file thật phát hiện GHN
> (`GhnCarrierClient`) là tích hợp thật (HTTP request/response thật với API GHN thật:
> create order, track order, calculate fee) — xác nhận claim trước đó. Nhưng phát
> hiện **`CreateShipmentCommandHandler` chưa từng gọi carrier client** — Shipment được
> tạo xong vẫn không có tracking number thật cho tới khi ai đó gọi
> `AssignTrackingNumberCommand` thủ công riêng biệt; "carrier API integration thực" tồn
> tại nhưng không nằm trên đường đi chính. Cũng phát hiện `Carrier.CalculateRate`
> (công thức theo cân nặng) **chưa từng được gọi ở đâu** — endpoint `calculate-fee`
> thật ra gọi thẳng GHN API, bỏ qua hoàn toàn logic trong domain. Và một **crash-risk
> bug độc lập với ❌ gốc**: `GhnCarrierClient` cần `IHttpClientFactory.CreateClient
> ("GHN")` nhưng `AddHttpClient` chưa từng được gọi ở bất kỳ đâu trong service —
> nghĩa là DI sẽ crash ngay khi GHN thực sự được cấu hình (đúng môi trường duy nhất
> mà client này có ý nghĩa). Domain method `MarkDeliveryFailed`/`MarkInTransit`/
> `MarkOutForDelivery` tồn tại nhưng không handler nào gọi tới — hàng giao thất bại
> không có cách nào ghi nhận qua API.
>
> Đã sửa: nối `CreateShipmentCommandHandler` → `ICarrierClient.CreateOrderAsync` thật
> (đặt đơn với GHN ngay khi tạo shipment, lấy tracking number thật); thêm
> `AddHttpClient("GHN")` (sửa crash-risk); cấu trúc lại địa chỉ từ 1 chuỗi
> `ShippingAddress` thành các field có cấu trúc (Ward/District/Province) khớp với cột
> SQL gốc và khớp với field GHN thực sự cần; `Carrier.CalculateRate`/mới thêm
> `EstimateDeliveryDate` tính theo mô hình nội tỉnh/liên tỉnh (không có geocoding thật
> trong service này nên đây là xấp xỉ theo tỉnh — đúng cách các carrier VN tự làm cho
> bước báo giá đầu tiên) và đã nối vào endpoint `GET /carriers/{id}/quote` thật.
>
> Cũng sửa 1 vi phạm Clean Architecture độc lập với ❌ gốc: `ICarrierClient` từng định
> nghĩa trong Infrastructure dù Application cần dùng (để gọi carrier khi tạo shipment)
> — đã chuyển sang `ERP.SHP.Application.Interfaces` làm port đúng chuẩn.
>
> Orphaned schema: `ShipmentTrackingEvents.Status` là `INT` trong SQL gốc nhưng C#/EF
> luôn dùng `string` — khác với các trường hợp khác trong đợt này (chỉ thiếu cột),
> đây là sai KIỂU DỮ LIỆU thật sự, sẽ lỗi ngay khi đọc/ghi nếu DB tạo từ script gốc.
> Đã sửa bằng đổi kiểu cột trong migration 002.

#### ✅ Tính năng đã có

**Domain**
- `Shipment` aggregate: OrderId, CarrierId, TrackingNumber, Status, + địa chỉ có cấu
  trúc (RecipientName/Phone, AddressStreet/Ward/District/Province/PostalCode/Country
  — mới 2026-07-03, thay cho 1 chuỗi `ShippingAddress` cũ), CancelledAt/CancelReason,
  DeliveryAttempts, ReceivedByName/SignatureUrl/PhotoUrl (POD)
- `ShipmentItem` entity: ProductId, Quantity, Weight
- `TrackingEvent` entity: Status, Location, OccurredAt
- `Carrier` entity: Name, Code, IsActive, BaseRatePerKg, + InterProvinceSurcharge,
  OriginProvince (mới 2026-07-03)
- `ReturnShipment` + trạng thái riêng (RMA — mới 2026-07-03, reverse logistics là
  aggregate riêng, không tái dùng state machine của Shipment thuận)
- Trạng thái Shipment: Pending → **ReadyForPickup** (mới) → PickedUp → InTransit →
  OutForDelivery → Delivered/DeliveryFailed (nay có `RetryDelivery()`)/Returned/Cancelled
- `ShipmentEvents`: ShipmentCreated, TrackingUpdated, DeliveryConfirmed,
  `ShipmentFailedEvent` (mới 2026-07-03), `ReturnInitiatedEvent`/`ReturnReceivedEvent` (mới)

**Commands**
- `CreateShipmentCommand` (nay gọi `ICarrierClient.CreateOrderAsync` thật để lấy
  tracking number — trước đây phải gọi `AssignTrackingNumberCommand` thủ công riêng)
- `AddTrackingEventCommand`, `AssignTrackingNumberCommand` (xác nhận đã nối endpoint
  từ trước — claim "chưa có endpoint" của note cũ là sai)
- `ConfirmDeliveryCommand` (+ POD fields — mới 2026-07-03)
- `MarkDeliveryFailedCommand`/`RetryDeliveryCommand` (mới 2026-07-03 — nối
  `Shipment.MarkDeliveryFailed`/`RetryDelivery` trước đây tồn tại nhưng không handler
  nào gọi tới)
- `BulkUpdateTrackingCommand` (mới 2026-07-03 — best-effort theo từng dòng, 1 lỗi
  không làm hỏng cả batch)
- `InitiateReturnCommand`/`AssignReturnTrackingNumberCommand`/`MarkReturnPickedUpCommand`/
  `MarkReturnReceivedCommand`/`CancelReturnCommand` (RMA — mới 2026-07-03)

**Queries**
- `GetShipmentQuery`, `TrackShipmentQuery`, `ListShipmentsByOrderQuery`, `ListCarriersQuery`
- `GetCarrierQuoteQuery` (mới 2026-07-03 — nối `Carrier.CalculateRate`/
  `EstimateDeliveryDate` trước đây tồn tại nhưng không ai gọi)
- `GenerateShippingLabelQuery` (mới 2026-07-03 — PDF thật qua PdfSharp, không phải ảnh
  giả hay text thuần)
- `GetReturnShipmentByIdQuery`/`ListReturnsByShipmentQuery`

**Event Consumers**
- `SalesOrderApprovedEventHandler` → tạo shipment (RabbitMQ, có sẵn; vẫn dùng địa chỉ
  placeholder vì `SalesOrderApprovedEvent` gốc không mang address — giới hạn từ phía
  publisher, không sửa được ở đây)
- `InventoryReservedEventHandler` (mới 2026-07-03: trước đây chỉ log, nay gọi
  `Shipment.MarkReadyForPickup()` thật nếu Shipment đã tồn tại cho order đó — dùng
  `GetTrackedByOrderIdAsync` thay vì bản `AsNoTracking()` để tránh rủi ro EF đoán sai
  Added/Modified cho `TrackingEvent` mới thêm vào)

**Infrastructure**
- `GhnCarrierClient` thật (create order/track/calculate fee — xác nhận đúng claim);
  `IHttpClientFactory` cho client "GHN" trước đây chưa từng đăng ký — đã sửa (crash-risk)
- `ICarrierClient` port chuyển từ Infrastructure sang Application (sửa vi phạm Clean
  Architecture — mới 2026-07-03)
- Outbox+Kafka (`shipping-topic`) đã hoạt động từ trước, xác nhận thật

**API Controllers**
- `ShipmentsController` (+ delivery-failed, retry-delivery, bulk-tracking, label — mới
  2026-07-03)
- `ReturnShipmentsController` (mới 2026-07-03)
- `CarriersController` (+ `{id}/quote` — mới 2026-07-03)

#### ❌ Tính năng còn thiếu

*(Toàn bộ 10 mục ❌ gốc đã xử lý — xem changelog phía trên. Ghi chú riêng: chỉ GHN có
implementation thật; GHTK/J&T/FedEx/DHL vẫn chưa có — nếu cần đa carrier thật sự thì
đây là việc bổ sung tương lai. Rate/ETA theo tỉnh là xấp xỉ, không phải geocoding
khoảng cách thật — cần tích hợp bản đồ nếu muốn chính xác hơn.)*

#### Luồng Vận Hành

```
[Event: SalesOrderApproved]
→ SalesOrderApprovedEventHandler
→ CreateShipmentCommand → Shipment.Create(orderId, recipient, items)
→ ICarrierClient.CreateOrderAsync (GHN thật) → AssignTrackingNumber nếu thành công
→ ShipmentCreatedEvent → Outbox → Kafka shipping-topic

[Event: InventoryReserved — WarehouseService, mới 2026-07-03]
→ InventoryReservedEventHandler
→ nếu Shipment đã tồn tại cho order: Shipment.MarkReadyForPickup()
→ nếu chưa: chỉ log (chưa có gì để lên lịch)

Carrier → POST /api/v1/shipments/{id}/tracking (hoặc bulk-tracking cho nhiều đơn)
→ AddTrackingEventCommand/BulkUpdateTrackingCommand → TrackingUpdatedEvent

→ ConfirmDeliveryCommand (kèm ReceivedByName/SignatureUrl/PhotoUrl — POD)
→ Shipment.MarkAsDelivered() → DeliveryConfirmedEvent
  [Thất bại] → MarkDeliveryFailedCommand → ShipmentFailedEvent
             → RetryDeliveryCommand (thử giao lại) hoặc InitiateReturnCommand (RMA)

Khách trả hàng → POST /api/v1/shipments/{id}/returns (InitiateReturnCommand)
→ ReturnInitiatedEvent → AssignReturnTrackingNumberCommand → MarkReturnPickedUpCommand
→ MarkReturnReceivedCommand → ReturnReceivedEvent (Warehouse restock — chưa có consumer)

GET /api/v1/shipments/{id}/label → PDF thật (PdfSharp): tracking number, địa chỉ, cân nặng
```

---

### 3.8 POS Service — Bán Lẻ Tại Quầy

**Mục đích:** Quản lý giao dịch bán lẻ tại điểm bán, ca làm việc, terminal.

> **Cập nhật 2026-07-03:** Ground-truth audit xác nhận `Consume ProductCreated` và
> `Consume PromotionCreated` (2 trong 13 mục ❌ gốc) **đã có sẵn và hoạt động thật**
> (`PosKafkaConsumer` thật, đăng ký `IHostedService`, cập nhật cache thật) — claim
> "thiếu" trong tài liệu gốc là sai cho 2 mục này. `Shift summary` cũng đã xác nhận có
> sẵn và hoạt động thật từ trước (`GetShiftSummaryQueryHandler` tính toán thật từ DB,
> không phải stub — một ghi chú kế hoạch trước đó nói handler này thiếu đã lỗi thời).
>
> Phát hiện nghiêm trọng nhất, **không nằm trong danh sách ❌ gốc**: service này
> **hoàn toàn không có `OutboxProcessor`** — `POSDbContext.SaveChangesAsync` ghi đúng
> `TransactionCreatedEvent`/`CashShiftClosedEvent` vào bảng `OutboxMessages` (Outbox
> pattern viết đúng), nhưng không có job nào từng tồn tại để đẩy các message này lên
> Kafka. Đây là nguyên nhân gốc rễ khiến "Real-time inventory deduction" (mục ❌ #13)
> không thể hoạt động — sự kiện bán hàng không bao giờ rời khỏi database của chính
> POS, nên WarehouseService không có gì để consume. Đã thêm `OutboxProcessor` +
> `KafkaMessageBus` (topic `pos-topic`) theo đúng pattern các service khác.
>
> Cũng phát hiện `ProductPriceChangedForPosHandler` là handler thật (cập nhật cache
> giá đúng) nhưng chưa từng được đăng ký DI/dispatch — comment cũ trong code giải
> thích lý do (ProductService chưa có domain event tương ứng), nhưng ProductService
> đã thêm `ProductPriceChangedDomainEvent` từ mục 3.2 (2026-07-03) nên giờ đây có thể
> nối thật. Orphaned schema: `TransactionItem.LineTotal`/`TaxAmount` là computed
> property (không có setter) trong khi SQL định nghĩa là cột `NOT NULL` thật — EF sẽ
> bỏ qua các property này khi ghi, gây lỗi ràng buộc NOT NULL nếu DB tạo từ script gốc;
> đã chuyển sang stored property tính lại mỗi lần Recalculate(). `CashierId` trong SQL
> là `NVARCHAR(100)` nhưng C# luôn dùng `Guid` — sai kiểu dữ liệu thật, đã sửa bằng
> đổi kiểu cột trong migration 002.

#### ✅ Tính năng đã có

**Domain**
- `Transaction` aggregate: TerminalId, CashierId, Items, TotalAmount, DiscountAmount,
  TaxAmount, + PromotionId, VoidedAt, ExternalPaymentId (mới 2026-07-03, cột đã có sẵn
  trong SQL nhưng C# chưa dùng), Tenders (split payment — mới)
- `TransactionItem` entity: ProductId, Quantity, UnitPrice, TaxAmount (nay là stored
  property thay vì computed-only — sửa lỗi NOT NULL constraint tiềm ẩn), DiscountAmount
- `TransactionTender` (mới 2026-07-03 — split payment, nhiều phương thức/1 giao dịch)
- `CashShift` entity: TerminalId, CashierId, OpeningBalance, ClosingBalance, TotalSales,
  + Denominations (đếm tiền từng mệnh giá — mới 2026-07-03)
- `Terminal` (mới 2026-07-03 — bảng SQL đã có sẵn từ đầu nhưng hoàn toàn không có C#
  entity/repository/controller nào dùng tới)
- Trạng thái: Open → Completed / Voided / Suspended (giá trị `Suspended` vẫn chưa dùng
  — giữ nguyên như đã có, không phải mục ❌ gốc)
- `POSEvents`: TransactionCreated, CashShiftClosed

**Commands**
- `OpenTransactionCommand`, `AddTransactionItemCommand`, `VoidTransactionCommand`
- `ApplyDiscountCommand` (+ `PromotionId` — mới 2026-07-03)
- `CompleteTransactionCommand` (+ `Tenders` list cho split payment — mới 2026-07-03,
  tương thích ngược với single-method cũ)
- `OpenCashShiftCommand`/`CloseCashShiftCommand` (+ đếm tiền theo mệnh giá, validate
  tổng khớp với balance — mới 2026-07-03)
- `SyncOfflineTransactionsCommand` (mới 2026-07-03 — batch, idempotent theo
  ClientTransactionId do terminal tự sinh; việc lưu trữ offline thật sự nằm ở phía
  client/terminal app, ngoài phạm vi backend này — đây là nửa "sync" của offline mode)
- `CreateTerminalCommand`/`SetTerminalActiveCommand`/`RenameTerminalCommand` (mới)

**Queries**
- `GetTransactionQuery`, `GetActiveShiftQuery`, `GetShiftSummaryQuery` (+ breakdown
  theo phương thức thanh toán — mới 2026-07-03; xác nhận handler này đã thật từ trước)
- `GetProductByBarcodeQuery` (mới 2026-07-03 — nối `IPosProductCacheRepository.
  GetBySkuAsync` đã có sẵn nhưng chưa từng được gọi từ đâu)
- `GetDailyZReportQuery` (mới 2026-07-03 — tổng hợp mọi ca trong ngày, theo terminal
  hoặc toàn hệ thống)
- `GetCustomerDisplayQuery` (mới 2026-07-03 — poll giao dịch đang mở của 1 terminal
  cho màn hình phụ; không có tích hợp phần cứng thật, đây là nguồn dữ liệu để client
  màn hình phụ tự poll/subscribe)
- `GetReceiptQuery` (mới 2026-07-03 — text thuần định dạng 42 cột cho máy in nhiệt,
  không có driver ESC/POS thật trong service này)
- `ListTerminalsQuery`

**Event Consumers**
- `ProductCreatedForPosHandler` → cache sản phẩm (Kafka `product-topic`, xác nhận có
  sẵn và hoạt động thật)
- `ProductPriceChangedForPosHandler` → cập nhật giá cache (mới nối 2026-07-03 — handler
  có sẵn nhưng chưa từng đăng ký/dispatch)
- `PromotionCreatedForPosHandler` → cache khuyến mãi (Kafka `promotion-topic`, xác
  nhận có sẵn và hoạt động thật)
- `PaymentProcessedForPosHandler` (mới 2026-07-03, Kafka `payment-topic`) — khớp theo
  Transaction.Id == PaymentProcessedEvent.OrderId cho kịch bản click-and-collect/
  thanh toán online rồi nhận tại quầy; không khớp là bình thường (đa số online order
  không đi qua POS terminal nào)

**Infrastructure**
- Outbox+Kafka (`pos-topic`) — **mới 2026-07-03**, trước đây hoàn toàn không tồn tại
  dù `POSDbContext` đã ghi outbox đúng từ trước

**API Controllers**
- `TransactionsController` (+ receipt, sync-offline — mới 2026-07-03)
- `CashShiftsController`
- `PosProductsController` (barcode lookup — mới), `TerminalsController` (mới, +
  customer-display), `ReportsController` (Z-report — mới)

#### ❌ Tính năng còn thiếu

*(Toàn bộ 13 mục ❌ gốc đã xử lý — xem changelog phía trên; 2 mục hoá ra đã có sẵn từ
trước (ProductCreated/PromotionCreated consumers), phần còn lại triển khai thật mới.
Ghi chú riêng: WarehouseService vẫn cần tự thêm consumer cho `pos-topic` để thực sự
trừ tồn kho — tương tự pattern PurchaseOrderService (3.4) → WarehouseService (3.5),
việc publish giờ đã thật nhưng phía consume là việc của WarehouseService khi rà soát
tới, ngoài phạm vi đợt này.)*

#### Luồng Vận Hành

```
Cashier → POST /pos/shifts/open (kèm đếm tiền theo mệnh giá) → CashShift.Open()

Cashier → POST /pos/transactions/open → Transaction.Open(terminalId, cashierId)

Cashier → quét mã vạch → GET /pos/products/lookup/{barcode} → GetProductByBarcodeQuery
       → POST /pos/transactions/{id}/items → AddItem() → recalculate total

Cashier → POST /pos/transactions/{id}/discount (kèm PromotionId nếu có)

Cashier → POST /pos/transactions/{id}/complete (1 hoặc nhiều Tenders — split payment)
        → Transaction.CompleteWithTenders() → TransactionCreatedEvent
        → Outbox → Kafka pos-topic (mới 2026-07-03 — trước đây không đến đâu cả)
        → [WarehouseService cần tự thêm consumer để trừ tồn kho — chưa xử lý ở đợt này]

[Event: PaymentProcessed — PaymentService, mới 2026-07-03]
→ PaymentProcessedForPosHandler → khớp theo Transaction.Id == OrderId nếu có

Cashier → GET /pos/transactions/{id}/receipt → text thuần cho máy in nhiệt

[Mất mạng] → terminal tự lưu cục bộ → có mạng lại
→ POST /pos/transactions/sync-offline (batch, idempotent theo ClientTransactionId)

Cashier → POST /pos/shifts/close (kèm đếm tiền theo mệnh giá) → CashShift.Close()
Manager → GET /pos/reports/z-report?date=... → tổng hợp mọi ca trong ngày
```

---

### 3.9 Promotion Service — Khuyến Mãi

**Mục đích:** Quản lý chương trình khuyến mãi, coupon, loyalty points, flash sale.

**Cập nhật:** 2026-07-04 — toàn bộ tính năng còn thiếu đã được triển khai thật.

#### ✅ Tính năng đã có (đầy đủ sau đợt này)

**Domain**
- `Promotion` aggregate: Name, DiscountType (Percentage/FixedAmount/BuyXGetY/FreeShipping), PromotionScope (Order/Product/Category), DiscountValue, MaxDiscountAmount, MinOrderAmount, BuyQuantity/GetQuantity, MaxUsageTotal, MaxUsagePerUser, ApplicableProductIds (List\<Guid\>), ApplicableCategoryIds (List\<string\>), StartDate, EndDate, Status (Draft/Active/Paused/Expired/Cancelled)
- `Coupon` entity: Code (unique), IsUsed, UsedAt, UsedBy — owned by Promotion aggregate (EF OwnsMany)
- `LoyaltyAccount` aggregate: UserId, TotalPoints, AvailablePoints, Tier (Bronze/Silver/Gold/Platinum) + tier thresholds (1k/5k/10k), EarnPoints/RedeemPoints domain methods
- `LoyaltyTransaction` entity: AccountId, UserId, Points, Reason, TransactionType (Earn/Redeem) — full history
- `FlashSale` aggregate: Name, StartTime, EndTime, Status (Scheduled/Active/Completed/Cancelled), TryReserve() method
- `FlashSaleItem` entity: ProductId, ProductName, OriginalPrice, SalePrice, AllocatedQuantity, SoldQuantity, RemainingQuantity, DiscountPercentage (computed)
- Domain events: `PromotionCreatedEvent`, `CouponRedeemedEvent`, `LoyaltyPointsEarnedEvent`, `FlashSaleStartedEvent`

**Commands**
- `CreatePromotionCommand` — hỗ trợ đầy đủ scope/targeting/BuyXGetY
- `ActivatePromotionCommand`, `PausePromotionCommand`, `CancelPromotionCommand`
- `GenerateBulkCouponsCommand` — tạo tối đa 10.000 coupon/lần với prefix tùy chỉnh
- `RedeemCouponCommand`
- `EarnLoyaltyPointsCommand`, `RedeemLoyaltyPointsCommand`
- `CreateFlashSaleCommand` — tạo flash sale + items
- `StartFlashSaleCommand`, `CancelFlashSaleCommand`
- `ReserveFlashSaleItemCommand` — đặt chỗ flash sale stock

**Queries**
- `GetActivePromotionsQuery`, `GetAllPromotionsQuery`, `GetPromotionByIdQuery`
- `GetApplicablePromotionsQuery` — nhận OrderAmount + list items (ProductId/Category/Qty/Price), trả về danh sách KM áp dụng được, sắp xếp theo discount cao nhất
- `ValidateCouponQuery` — kiểm tra coupon còn hiệu lực + tính discount
- `GetLoyaltyBalanceQuery` — trả về points, tier, và điểm cần để lên hạng tiếp theo
- `GetFlashSalesQuery`, `GetFlashSaleByIdQuery`

**Promotion Eligibility Rules Engine**
- `Promotion.IsApplicableToItems()` — scope=Order (áp dụng mọi đơn), scope=Product (filter theo ApplicableProductIds), scope=Category (filter theo ApplicableCategoryIds)
- `Promotion.CalculateDiscount()` — tính giảm giá cho từng DiscountType, áp dụng MaxDiscountAmount ceiling
- BuyXGetY: tính số sets = totalQty / (buyQty+getQty), tặng free các sản phẩm rẻ nhất

**Loyalty Tier Benefits**
- Bronze (0-999 pts): 1 pt/1 đơn vị tiền tệ
- Silver (1000-4999 pts): 2 pts/đơn vị
- Gold (5000-9999 pts): 3 pts/đơn vị
- Platinum (≥10000 pts): 5 pts/đơn vị
- Response DTO trả về PointsToNextTier + NextTier

**Infrastructure**
- `PromotionDbContext`: FlashSales, FlashSaleItems, LoyaltyTransactions tables mới; ApplicableProductIds/ApplicableCategoryIds lưu JSON; index (Status, StartDate/EndDate)
- `FlashSaleRepository`, `LoyaltyAccountRepository` (với Transactions Include)
- `PromotionSchedulerJob` (background, 1 phút/lần): auto-activate Draft→Active khi StartDate tới, auto-expire Active/Paused→Expired khi EndDate qua, auto-start/complete FlashSale
- `OutboxProcessor` + `KafkaMessageBus`: publish promotion-topic events
- `PromotionKafkaConsumer`: consume product-topic → cache ProductCreated
- `SalesOrderRabbitMqConsumer`: consume sales_order_events → earn loyalty points

**API**
- `PromotionsController`: GET /promotions, GET /promotions/all, GET /promotions/{id}, POST /promotions, POST /{id}/activate, POST /{id}/pause, POST /{id}/cancel, POST /{id}/generate-coupons, POST /applicable, POST /validate-coupon, POST /redeem-coupon
- `LoyaltyController`: GET /loyalty/{userId}, POST /loyalty/{userId}/earn, POST /loyalty/{userId}/redeem
- `FlashSalesController`: GET /flash-sales, GET /flash-sales/{id}, POST /flash-sales, POST /{id}/start, POST /{id}/cancel, POST /{id}/reserve

#### ⚠️ Còn thiếu (thấp ưu tiên)

| Tính năng | Ghi chú |
|-----------|---------|
| Promotion analytics | Hiệu quả từng chương trình — cần Reporting Service |
| Points expiry | Điểm hết hạn sau N ngày không dùng |
| Stackable coupons | Kết hợp nhiều coupon cùng lúc |
| Per-user usage enforcement | MaxUsagePerUser đã có trong domain nhưng chưa check khi redeem |

#### Luồng Vận Hành

```
Admin → POST /promotions → CreatePromotionCommand → Promotion.Create()
     → PromotionCreatedEvent (Outbox → Kafka promotion-topic)
     → PromotionSchedulerJob tự động Activate khi đến StartDate

Customer → POST /promotions/applicable {OrderAmount, Items[]}
         → GetApplicablePromotionsQuery → kiểm tra scope/eligibility → tính discount
         → trả về list ApplicablePromotionDto (sắp xếp theo DiscountAmount cao nhất)

Customer → POST /promotions/validate-coupon {Code, OrderAmount}
         → ValidateCouponQuery → kiểm tra IsUsed, IsActive, tính discount
Customer → POST /promotions/redeem-coupon {Code, UserId, OrderAmount}
         → RedeemCouponCommand → Coupon.Redeem() → CouponRedeemedEvent

[Event: SalesOrderCreated] (RabbitMQ sales_order_events)
→ SalesOrderRabbitMqConsumer → SalesOrderCreatedForPromotionHandler
→ EarnLoyaltyPointsCommand → LoyaltyAccount.EarnPoints() → LoyaltyTransaction
→ LoyaltyPointsEarnedEvent → Outbox → Kafka

Admin → POST /flash-sales → CreateFlashSaleCommand → FlashSale.Create() + AddItem()
Admin → POST /flash-sales/{id}/start → StartFlashSaleCommand → FlashSale.Start()
     → FlashSaleStartedEvent → Outbox → Kafka
Customer → POST /flash-sales/{id}/reserve → ReserveFlashSaleItemCommand → FlashSale.TryReserve()
PromotionSchedulerJob → auto-start/complete FlashSale theo StartTime/EndTime
```

---

### 3.10 CRM Service — Quản Lý Khách Hàng

**Mục đích:** Theo dõi contacts, leads, opportunities, cases hỗ trợ khách hàng, lịch sử tương tác, và phân nhóm khách hàng qua tags.

#### ✅ Tính năng đã có

**Domain Entities**
- `Contact`: thông tin khách hàng/prospect (UserId linking, IsDeleted soft-delete)
- `Lead`: khách hàng tiềm năng (LeadSource enum, EstimatedValue, Status, QualifiedAt)
- `Opportunity`: cơ hội bán hàng (Stage pipeline, Value, ExpectedCloseDate, WonAt/LostAt)
- `Case`: vụ việc hỗ trợ (CasePriority, CaseStatus, AssignedToUserId, Resolution)
- `Activity`: lịch sử tương tác (ActivityType: Call/Email/Meeting/Note/Task; Status: Scheduled/Completed/Cancelled; ContactId, OpportunityId, CaseId FK; Source field)
- `ContactTag`: phân nhóm khách hàng (Tag normalized to lowercase, unique per contact)

**Domain Events**
- `LeadCreatedEvent`, `LeadQualifiedEvent` (published via Outbox → Kafka)
- `OpportunityWonEvent`, `OpportunityLostEvent`
- `CaseOpenedEvent`, `CaseResolvedEvent`, `CaseClosedEvent`, `CaseAssignedEvent`
- `ActivityCreatedEvent(ActivityId, ContactId, ActivityType, Subject)`
- `ActivityCompletedEvent(ActivityId, ContactId)`

**CQRS — Commands (14)**
- Contact: `CreateContactCommand`, `UpdateContactCommand`
- Lead: `CreateLeadCommand`, `QualifyLeadCommand`, `UpdateLeadStatusCommand`
- Opportunity: `CreateOpportunityCommand`, `UpdateOpportunityStageCommand`, `CloseOpportunityWonCommand`, `CloseOpportunityLostCommand`
- Case: `OpenCaseCommand`, `AssignCaseCommand`, `ResolveCaseCommand`, `CloseCaseCommand`
- Activity: `CreateActivityCommand`, `CompleteActivityCommand`, `CancelActivityCommand`, `UpdateActivityNotesCommand`
- Tag: `AddContactTagCommand`, `RemoveContactTagCommand`

**CQRS — Queries (9)**
- `GetContactQuery`, `ListContactsQuery`
- `GetLeadQuery`, `ListLeadsQuery`
- `GetOpportunityQuery`, `ListOpportunitiesQuery`, `GetPipelineSummaryQuery`
- `GetCaseQuery`, `ListCasesQuery`
- `GetActivityQuery`, `ListActivitiesQuery`
- `GetContactTagsQuery`, `ListContactsByTagQuery`, `GetAllTagsQuery`

**Validators (FluentValidation)**
- `CreateContactCommandValidator`, `UpdateContactCommandValidator`
- `CreateLeadCommandValidator`, `CreateOpportunityCommandValidator`
- `OpenCaseCommandValidator`
- `CreateActivityCommandValidator`, `AddContactTagCommandValidator`

**Integration — Consumed Events**
- Kafka `identity-topic` → `UserCreatedEventHandler`: auto-creates `Contact` record linked to UserId
- Kafka `payment-topic` → `PaymentProcessedEventHandler`: logs payment correlation
- RabbitMQ `sales_order_events` exchange → `SalesOrderCreatedEventHandler`: creates `Activity` note (type=Note) on the linked Contact with order details

**API Controllers — 5 controllers, ~30 endpoints**
- `ContactsController` (`/api/v1/crm/contacts`): List, Get, Create, Update; + tag sub-resources: GET/POST `/tags`, DELETE `/tags/{tag}`, GET `/by-tag/{tag}`, GET `/tags/all`
- `LeadsController` (`/api/v1/crm/leads`): List, Get, Create, Qualify, UpdateStatus
- `OpportunitiesController` (`/api/v1/crm/opportunities`): List, Get, Create, UpdateStage, Won, Lost; + GET `/pipeline-summary`
- `CasesController` (`/api/v1/crm/cases`): List, Get, Open, Assign, Resolve, Close
- `ActivitiesController` (`/api/v1/crm/activities`): List, Get, Create, Complete, Cancel, UpdateNotes

**Repositories (Infrastructure)**
- `ContactRepository`: GetByIdAsync, GetByUserIdAsync, AddAsync, UpdateAsync, GetAllAsync, GetPagedAsync, GetByIdsPagedAsync
- `LeadRepository`, `OpportunityRepository`, `CaseRepository` (full CRUD + paged queries)
- `ActivityRepository`: GetByIdAsync, AddAsync, UpdateAsync, GetPagedAsync (filtered by contactId/opportunityId/activityType)
- `ContactTagRepository`: GetByContactIdAsync, GetContactIdsByTagAsync, AddAsync, DeleteAsync, GetAllTagsAsync

**EF Core — CrmDbContext**
- Tables: Contacts, Leads, Opportunities, Cases, Activities, ContactTags, OutboxMessages
- Activity: indexes on (ContactId), (ContactId, Status), (OpportunityId), (CreatedAt)
- ContactTag: unique index on (ContactId, Tag); index on Tag; Tag normalized to lowercase

#### ⚠️ Tính năng chưa triển khai (low priority)

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| Email integration | Thấp | Gửi/nhận email trực tiếp trong CRM |
| Calendar / appointment | Thấp | Lịch hẹn với reminder |
| Bulk lead import | Thấp | Import CSV |

#### Luồng Vận Hành

```
[Event: UserCreated from identity-topic]
→ UserCreatedEventHandler → Contact.Create(userId, email, name)
→ Contact lưu vào CrmDB

Sales → POST /api/v1/crm/leads → CreateLeadCommand → Lead.Create()
     → LeadCreatedEvent → Outbox → Kafka (crm-topic)
     → NotificationService assigns salesperson

Sales → POST /api/v1/crm/leads/{id}/qualify → QualifyLeadCommand
     → Lead.Qualify() → LeadQualifiedEvent
     → Opportunity.Create(contactId, value, expectedCloseDate)

[Event: SalesOrderCreated from sales_order_events RabbitMQ]
→ SalesOrderCreatedEventHandler → find Contact by UserId
→ Activity.Create(contactId, ActivityType.Note, "Sales order placed — Order #...")
→ Activity stores order reference in Source field

Sales → POST /api/v1/crm/activities → CreateActivityCommand
      → Activity.Create(contactId, ActivityType.Call, subject, notes, scheduledAt)
      → ActivityCreatedEvent → Outbox

POST /api/v1/crm/activities/{id}/complete
→ Activity.Complete(notes) → ActivityCompletedEvent

POST /api/v1/crm/contacts/{id}/tags → AddContactTagCommand
→ ContactTag.Create(contactId, tag) → tag normalized to lowercase

GET /api/v1/crm/contacts/by-tag/{tag}
→ ListContactsByTagQuery → ContactTagRepository.GetContactIdsByTagAsync()
→ ContactRepository.GetByIdsPagedAsync() → paged contacts

Customer → POST /api/v1/crm/cases → OpenCaseCommand → Case.Open()
         → CaseOpenedEvent → assign to support team

POST /api/v1/crm/cases/{id}/resolve → ResolveCaseCommand
→ Case.Resolve(resolution) → CaseResolvedEvent

GET /api/v1/crm/opportunities/pipeline-summary
→ GetPipelineSummaryQuery → count by stage, total/won pipeline value
```

---

### 3.11 HRM Service — Nhân Sự

**Mục đích:** Chấm công, nghỉ phép, bảng lương, ca làm việc. Employee/Department master data thuộc SharedMasterDataService; HRM giữ `EmployeeSnapshot` read-only projection.

#### ✅ Tính năng đã có

**Domain Entities**
- `EmployeeSnapshot`: projection read-only từ MST (EmployeeNumber, FullName, Email, EmploymentStatus, DepartmentId) — sync qua Kafka `mst-topic`
- `LeaveRequest`: yêu cầu nghỉ phép (LeaveType enum, DaysRequested, Approve/Reject/Cancel lifecycle)
- `PaySlip`: phiếu lương (GrossSalary, Deductions, **OvertimeHours**, **OvertimePay**, NetSalary, PaymentStatus)
- `AttendanceRecord`: chấm công ngày (CheckIn/CheckOut, TotalHours, **OvertimeHours**, AttendanceStatus: Present/Absent/Late/HalfDay/OnLeave/Holiday) — unique index (EmployeeId, Date)
- `WorkShift`: ca làm việc (Name, StartTime, EndTime TimeSpan, ShiftHours, IsActive)

**Domain Events**
- `LeaveApprovedEvent(LeaveRequestId, EmployeeId, StartDate, EndDate)` — Outbox → Kafka
- `LeaveRejectedEvent(LeaveRequestId, EmployeeId, Reason)` — Outbox → Kafka
- `PaySlipGeneratedEvent(PaySlipId, EmployeeId, NetPay, PayPeriod)` — Outbox → Kafka
- `AttendanceCheckedInEvent(AttendanceRecordId, EmployeeId, Date, CheckInTime)`
- `AttendanceCheckedOutEvent(AttendanceRecordId, EmployeeId, CheckOutTime, TotalHours, OvertimeHours)`

**CQRS — Commands (13)**
- Snapshot sync: `UpsertEmployeeSnapshotCommand` (syncs Email+EmployeeNumber too), `MarkEmployeeSnapshotTerminatedCommand`, `UpdateEmployeeSnapshotDepartmentCommand`
- Leave: `SubmitLeaveRequestCommand`, `ApproveLeaveCommand`, `RejectLeaveCommand`, `CancelLeaveCommand`
- Payroll: `ProcessPayrollCommand` (integrates OT from AttendanceRecord), `GeneratePaySlipCommand` (OvertimeHours + OvertimePay params)
- Attendance: `CheckInCommand`, `CheckOutCommand`, `MarkAbsentCommand`
- Work Shift: `CreateWorkShiftCommand`, `UpdateWorkShiftCommand`, `DeactivateWorkShiftCommand`

**CQRS — Queries (11)**
- Employee: `GetEmployeeSnapshotQuery`, `ListEmployeeSnapshotsQuery` (search/dept/status filters)
- Leave: `GetLeaveRequestQuery`, `ListLeaveRequestsQuery`
- PaySlip: `GetPaySlipQuery`, `ListPaySlipsQuery`
- Attendance: `GetAttendanceQuery`, `GetAttendanceByDateQuery`, `ListAttendanceQuery`, `GetAttendanceSummaryQuery` (present/absent/late days, total hours, OT hours)
- Work Shift: `GetWorkShiftQuery`, `ListWorkShiftsQuery`

**Validators (FluentValidation)**
- `SubmitLeaveRequestCommandValidator`, `RejectLeaveCommandValidator`
- `GeneratePaySlipCommandValidator`, `ProcessPayrollCommandValidator`
- `CheckInCommandValidator`, `CheckOutCommandValidator`
- `CreateWorkShiftCommandValidator`

**Integration — Consumed Events**
- Kafka `mst-topic` → `MasterDataEventConsumer` (IHostedService) → `EmployeeMasterDataEventHandlers`:
  - `EmployeeHiredIntegrationEvent` → `UpsertEmployeeSnapshotCommand` (FullName, Email, EmployeeNumber, DepartmentId)
  - `EmployeeTerminatedIntegrationEvent` → `MarkEmployeeSnapshotTerminatedCommand`
  - `EmployeeTransferredIntegrationEvent` → `UpdateEmployeeSnapshotDepartmentCommand`

**API Controllers — 5 controllers, ~22 endpoints**
- `LeaveRequestsController` (`/api/v1/hrm/leave-requests`): List, Get, Submit, Approve, Reject, Cancel
- `PayrollController` (`/api/v1/hrm/payroll`, `/api/v1/hrm/payslips`): ProcessPayroll, GeneratePaySlip, ListPaySlips, GetPaySlipById
- `AttendanceController` (`/api/v1/hrm/attendance`): List, Get, GetByDate, GetSummary (monthly), CheckIn, CheckOut, MarkAbsent
- `WorkShiftsController` (`/api/v1/hrm/work-shifts`): List, Get, Create, Update, Deactivate
- `EmployeesController` (`/api/v1/hrm/employees`): List (search/dept/status), Get

**Repositories (Infrastructure)**
- `EmployeeSnapshotRepository`: GetByIdAsync, GetActiveAsync, **GetPagedAsync** (search/dept/status), Upsert
- `LeaveRequestRepository`, `PaySlipRepository` (full CRUD + paged)
- `AttendanceRepository`: GetByIdAsync, **GetByEmployeeAndDateAsync**, ListAsync (filters), **GetMonthlyOvertimeHoursAsync**, Add, Update
- `WorkShiftRepository`: GetByIdAsync, GetAllAsync (activeOnly filter), Add, Update

**Overtime integration**
- `ProcessPayrollCommand` handler queries `AttendanceRepository.GetMonthlyOvertimeHoursAsync` for each employee, calculates OT pay as `overtimeHours × (grossSalary/160) × 1.5`, and passes both to `PaySlip.Create()`
- `GeneratePaySlipCommand` accepts explicit `OvertimeHours` + `OvertimePay` for one-off slips

#### ⚠️ Tính năng chưa triển khai (không thuộc HRM)

| Tính năng | Lý do |
|-----------|-------|
| `HireEmployeeCommand` / `TerminateEmployeeCommand` | Thuộc **SharedMasterDataService** — MST owns Employee aggregate |
| `CreateDepartmentCommand` | Thuộc **SharedMasterDataService** |
| Org chart | Cần Department hierarchy từ MST; HRM chỉ có EmployeeSnapshot (flat) |
| Performance review | Low priority |
| Calendar / appointment | Low priority |

#### Luồng Vận Hành

```
[Event: EmployeeHired từ mst-topic]
→ MasterDataEventConsumer → UpsertEmployeeSnapshotCommand
→ EmployeeSnapshot created/updated (FullName, Email, EmployeeNumber, DeptId)

Employee → POST /api/v1/hrm/attendance/check-in
         → CheckInCommand(employeeId, checkInTime, workShiftId?)
         → AttendanceRecord.CheckIn() → late check nếu có shift
         → AttendanceCheckedInEvent → Outbox

Employee → POST /api/v1/hrm/attendance/{id}/check-out
         → CheckOutCommand → AttendanceRecord.CheckOut()
         → TotalHours = checkOut - checkIn; OvertimeHours = max(0, TotalHours - 8)
         → AttendanceCheckedOutEvent → Outbox

Employee → POST /api/v1/hrm/leave-requests → SubmitLeaveRequestCommand
         → LeaveRequest.Create() → pending

Manager → POST /api/v1/hrm/leave-requests/{id}/approve → ApproveLeaveCommand
        → LeaveRequest.Approve() → LeaveApprovedEvent
        → SharedMasterDataService consumes LeaveApproved → marks Employee.OnLeave

HR → POST /api/v1/hrm/payroll/process { year, month }
   → ProcessPayrollCommand → lấy active employees từ snapshot
   → mỗi employee: GetMonthlyOvertimeHoursAsync → tính OT pay
   → PaySlip.Create(gross, deductions, overtimeHours, overtimePay)
   → NetSalary = gross + overtimePay - deductions
   → PaySlipGeneratedEvent → Outbox → Kafka

GET /api/v1/hrm/attendance/employee/{id}/summary?year=2026&month=7
→ GetAttendanceSummaryQuery → {PresentDays, AbsentDays, LateDays, TotalHours, OvertimeHours}
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

#### ✅ Đã implement (2026-07-04) — bảng "còn thiếu" trước đây đã lỗi thời

| Tính năng từ bảng cũ | Trạng thái | Ghi chú |
|---|---|---|
| `CreateInvoiceCommand` | ✅ Đã có | Tồn tại từ trước; handler thực |
| `ApproveBillCommand` | ✅ Đã có | Tồn tại từ trước |
| `PostJournalEntryCommand` | ✅ Đã có + **nâng cấp** | Giờ lưu `JournalLine` vào DB |
| `CloseFiscalPeriodCommand` | ✅ Đã có | Tồn tại từ trước |
| Consume `SalesOrderCreated` | ✅ Đã có | RabbitMQ SalesOrderRabbitMqConsumer |
| Consume `PurchaseOrderCreated` | ✅ Đã có | Kafka AccountingKafkaConsumer |
| Consume `PaymentProcessed` | ✅ Đã có | Kafka AccountingKafkaConsumer |
| Consume `WarehouseMovement` | ✅ **MỚI** | Kafka `warehouse-topic`; log + open period check |
| Published events 4 loại | ✅ Đã có | Outbox → Kafka `accounting-topic` |
| All Controllers | ✅ Đã có + **3 mới** | 5 cũ + Reports + TaxRates + BankAccounts |
| Chart of Accounts | ✅ Đã có | AccountsController |
| AR Aging report | ✅ Đã có | `GET /api/v1/invoices/ar-aging` |
| AP Aging report | ✅ Đã có | `GET /api/v1/bills/ap-aging` |
| Trial balance | ✅ **MỚI** | `GET /api/v1/accounting/reports/trial-balance` |
| P&L Statement | ✅ **MỚI** | `GET /api/v1/accounting/reports/profit-and-loss` |
| Balance Sheet | ✅ **MỚI** | `GET /api/v1/accounting/reports/balance-sheet` |
| Tax management (VAT) | ✅ **MỚI** | TaxRate entity + TaxRatesController |
| Bank reconciliation | ✅ **MỚI** | BankAccount + BankStatement + BankAccountsController |

**Ghi chú quan trọng về Trial Balance / P&L / Balance Sheet:**
- Ba báo cáo này aggregate từ `JournalLine` (dòng bút toán lưu DB).
- Trước 2026-07-04, `PostJournalEntryCommand` chỉ lưu `TotalDebit`/`TotalCredit` tổng — dòng line không persist. Đã fix.
- Báo cáo chính xác khi Chart of Accounts được thiết lập và bút toán được post đúng tài khoản.

**Entities mới thêm:**
- `JournalLine`: JournalEntryId (FK cascade), AccountId, Debit, Credit, Description
- `TaxRate`: Name, Code (unique), Rate %, IsActive
- `BankAccount`: AccountName, BankName, AccountNumber, CurrentBalance, GlAccountId?
- `BankStatement`: BankAccountId, StatementDate, OpeningBalance, ClosingBalance, Status (Open/Reconciled)

#### Luồng Vận Hành Thực Tế

```
[Event: SalesOrderCreated — RabbitMQ]
→ SalesOrderRabbitMqConsumer → CreateInvoiceCommand → Invoice.Create()
→ InvoiceCreatedEvent → Outbox → Kafka accounting-topic

[Event: PaymentProcessed — Kafka payment-topic]
→ AccountingKafkaConsumer → MarkInvoicePaidCommand → Invoice.MarkPaid()

[Event: PurchaseOrderCreated — Kafka purchasing-topic]
→ AccountingKafkaConsumer → CreateBillCommand → Bill.Create()

[Event: StockMoved — Kafka warehouse-topic] (MỚI)
→ AccountingKafkaConsumer → WarehouseMovementIntegrationEventHandler
→ Log + check open fiscal period (GL post thủ công hoặc COA mapping)

[Month End]
→ POST /api/v1/fiscal-periods/{id}/close → FiscalPeriodClosedEvent

[Reports]
→ GET /api/v1/accounting/reports/trial-balance
→ GET /api/v1/accounting/reports/profit-and-loss?fromDate=&toDate=
→ GET /api/v1/accounting/reports/balance-sheet?asOfDate=
```

---

### 3.13 Notification Service — Thông Báo

**Mục đích:** Gửi thông báo đa kênh (Email, SMS, Push) dựa trên events từ toàn hệ thống.

> **Đợt rà soát 2026-07-04:** 8/10 mục ❌ trong bảng gốc là claim sai — đã có sẵn và
> hoạt động thật. Chỉ 2 mục thật sự thiếu (retry loop, bulk) và 1 mục mới được thêm
> (webhook outbound). Digest emails bỏ qua (LOW priority, cần aggregation window phức
> tạp).

#### ✅ Tính năng đã có (xác nhận thật sau rà soát)

**Domain**
- `Notification` aggregate: RecipientId, Channel, TemplateId, Subject, Body, Status
  (Queued/Sending/Sent/Delivered/Failed/Bounced/Cancelled), Priority, ScheduledFor,
  RetryCount, FailedAt, SentAt, DeliveredAt
- `NotificationTemplate` entity: Code (unique), Name, Channel, Subject, Body,
  Variables (JSON), IsActive; method `Render(variables)` thực hiện `{key}` substitution
- `NotificationPreference` entity: UserId, EmailEnabled, SmsEnabled, PushEnabled,
  InAppEnabled, DisabledChannels, OptOutAt; method `IsChannelEnabled()`, `OptOut()`
- `NotificationLog` owned entity: EventType, Details, EventTimestamp (immutable)
- `NotificationEvents`: `NotificationSentEvent`, `NotificationFailedEvent`
- **MỚI 2026-07-04:** `WebhookEndpoint` aggregate: Name, Url, Secret, IsActive,
  EventTypesRaw (comma-separated filter); methods `Create/Update/Activate/Deactivate`

**Commands / Queries**
- `SendNotificationCommand` → `SendNotificationCommandHandler`: check preference →
  load+render template → create Notification → immediate deliver nếu High/Critical
- `CreateTemplateCommand`, `UpdatePreferenceCommand`, `UnsubscribeCommand`
- `GetNotificationQuery`, `GetNotificationHistoryQuery` (paginated)
- `GetTemplateQuery`, `ListTemplatesQuery`
- `GetPreferenceQuery`
- **MỚI 2026-07-04:** `BulkSendNotificationCommand` → `BulkSendNotificationCommandHandler`:
  tạo nhiều Notification cùng lúc (1 per recipient), mỗi recipient có thể có
  personalized template variables riêng, bỏ qua nếu channel bị tắt trong preference,
  persist batch qua `AddRangeAsync`
- **MỚI 2026-07-04:** `RegisterWebhookCommand`, `UpdateWebhookCommand`,
  `DeleteWebhookCommand`, `SetWebhookActiveCommand`, `ListWebhooksQuery`,
  `GetWebhookQuery` → `WebhookHandlers`

**Providers (thật, không phải stub)**
- Email: `SmtpEmailSender` — System.Net.Mail, SSL, cấu hình qua `Email:SmtpHost/Port/
  Username/Password/From`
- SMS: `TwilioSmsSender` — HTTP POST tới Twilio Messages API, Basic auth; fallback
  `NullSmsSender` khi `Twilio:AccountSid` chưa cấu hình
- Push: `FcmPushSender` — HTTP POST tới FCM API, Bearer token; fallback `NullPushSender`
  khi `Fcm:ServerKey` chưa cấu hình
- **MỚI 2026-07-04:** `WebhookSender` — HTTP POST JSON với HMAC-SHA256 signature
  (`X-Hub-Signature: sha256=…`) và `X-Event-Type`/`X-Timestamp` headers

**Background Jobs**
- `NotificationDispatchJob` (BackgroundService, interval 10s):
  - `GetPendingAsync()` → gửi queued notifications theo priority
  - **MỚI 2026-07-04 (fix thật sự):** `GetFailedForRetryAsync()` → retry với
    **exponential backoff**: 30s sau lần fail đầu, 60s sau lần fail thứ hai (max 3
    lần tổng). `ShouldRetryNow()` kiểm tra `FailedAt.AddSeconds(30 × 2^(RetryCount-1))
    ≤ UtcNow`. Trước đây method `GetFailedForRetryAsync()` và `CanRetry()` tồn tại
    nhưng job không bao giờ gọi chúng — retry không hoạt động.
  - **MỚI 2026-07-04:** Sau mỗi lần gửi thành công, fire webhooks đang active cho
    event type `"notification.sent"` qua `WebhookSender`
- `IdentityEventConsumer` (IHostedService, Kafka `identity-topic`): dispatch
  `TwoFactorCodeGeneratedDomainEvent` → `TwoFactorCodeGeneratedIntegrationEvent` và
  `UserRegisteredDomainEvent` → `UserCreatedIntegrationEvent`

**Integration Event Handlers** (handler class đã có, MediatR INotificationHandler)
- `UserCreatedIntegrationEvent` → `WELCOME_EMAIL` template
- `SalesOrderCreatedIntegrationEvent` → `ORDER_CONFIRMATION` template (High priority)
- `PaymentProcessedIntegrationEvent` → `PAYMENT_RECEIPT` template (High priority)
- `ShipmentCreatedIntegrationEvent` → `SHIPMENT_NOTIFICATION` template
- `EmployeeHiredIntegrationEvent` → `EMPLOYEE_WELCOME` template
- `TwoFactorCodeGeneratedIntegrationEvent` → `TWO_FACTOR_CODE` template (High
  priority, channel từ event.Method)
- `CaseOpenedIntegrationEvent`, `FlashSaleStartedIntegrationEvent`,
  `PaymentFailedIntegrationEvent`, `SalesOrderCancelledIntegrationEvent` (handler
  đã có, Kafka consumer cho topic tương ứng chưa được thêm)

**Scheduling** (đã có từ trước)
- `Notification.ScheduledFor` — notifications với `ScheduledFor > now` sẽ được giữ
  trong trạng thái Queued cho tới khi `NotificationDispatchJob` tick tiếp theo sau
  thời điểm đó

**Unsubscribe / Preference** (đã có từ trước)
- `POST /api/v1/preferences/{userId}/unsubscribe` → `UnsubscribeCommand` →
  `preference.OptOut()` — tắt tất cả channels, ghi OptOutAt

**API — 4 controllers**

| Controller | Route | Endpoints |
|---|---|---|
| `NotificationsController` | `/api/v1/notifications` | POST / (send), GET /{id}, GET /history/{recipientId}?skip&take, **POST /bulk (MỚI)** |
| `TemplatesController` | `/api/v1/templates` | GET / (list, filter isActive), GET /{id}, POST / |
| `PreferencesController` | `/api/v1/preferences` | GET /{userId}, PUT /{userId}, POST /{userId}/unsubscribe |
| **`WebhooksController` (MỚI)** | `/api/v1/webhooks` | GET / (list), GET /{id}, POST / (register), PUT /{id}, DELETE /{id}, POST /{id}/activate, POST /{id}/deactivate |

**Database**
- `NotificationDbContext`: Notifications (OwnsMany Logs), Templates, Preferences,
  **WebhookEndpoints (MỚI)**, OutboxMessages
- WebhookEndpoint EF config: EventTypesRaw → column "EventTypes", computed `EventTypes`
  property ignored, index on IsActive

#### ⚠️ Còn thiếu / Cố ý bỏ qua

| Tính năng | Trạng thái | Ghi chú |
|---|---|---|
| APNS (Apple Push) | Chưa có | FCM đã có; APNS cần Apple dev certificate + JWT — thêm sau |
| Digest emails | Bỏ qua (LOW) | Cần aggregation window scheduler phức tạp |
| Kafka consumers cho `sales-topic`, `payment-topic`, `shipping-topic`, v.v. | Chưa có | Handler class đã tồn tại; cần thêm IHostedService consumer theo pattern `IdentityEventConsumer` |

#### Luồng Vận Hành

```
[Event-driven — ví dụ TwoFactorCodeGenerated]
IdentityService → Kafka identity-topic (outbox)
→ IdentityEventConsumer (IHostedService) → IMediator.Publish
→ TwoFactorCodeGeneratedIntegrationEventHandler
→ SendNotificationCommand { Channel = SMS|Email từ event.Method, TemplateCode = "TWO_FACTOR_CODE" }
→ SendNotificationCommandHandler:
    1. GetPreferenceAsync → channel enabled?
    2. GetByCodeAsync("TWO_FACTOR_CODE") → template.Render({ code, expiryMinutes })
    3. Notification.Create(…, Priority=High)
    4. NotificationRepository.AddAsync
    5. [Priority=High] → DeliverAsync → TwilioSmsSender / SmtpEmailSender → MarkAsSent/MarkAsFailed

[Dispatch job — normal & Low priority + retry]
NotificationDispatchJob (mỗi 10s):
├── GetPendingAsync → notifications Queued + ScheduledFor ≤ now → gửi theo channel
│   └── sau gửi thành công → FireWebhooksAsync("notification.sent", …)
└── GetFailedForRetryAsync → filter ShouldRetryNow() [backoff 30s/60s]
    └── retry gửi → MarkAsSent hoặc MarkAsFailed (RetryCount max 3)

[Bulk campaign]
Admin → POST /api/v1/notifications/bulk { Recipients:[…], TemplateCode, SharedVariables }
→ BulkSendNotificationCommandHandler:
    for each recipient:
        check preference → skip nếu channel disabled
        merge SharedVariables + recipient.Variables → template.Render()
        Notification.Create(…, ScheduledFor?)
→ AddRangeAsync (1 DB round-trip cho toàn batch)
→ NotificationDispatchJob sẽ gửi trong tick tiếp theo

[Webhook outbound]
POST /api/v1/webhooks { Name, Url, Secret, EventTypes:["notification.sent"] }
→ WebhookEndpoint.Create → WebhookRepository.AddAsync
→ khi notification được gửi:
    WebhookSender.SendAsync(url, secret, "notification.sent", { Id, RecipientId, Channel, Status, SentAt })
    HTTP POST + X-Hub-Signature: sha256=<HMAC-SHA256(timestamp.json, secret)>
```

---

### 3.14 Reporting Service — Báo Cáo & Phân Tích

**Mục đích:** Kho dữ liệu OLAP read-only, cung cấp data cho dashboards và BI tools.

> **2026-07-05:** Đã xử lý 3.14. Phần lớn mục ❌ trong tài liệu cũ là **claim sai** —
> 3 Kafka consumers, tất cả fact tables (Sales/Payment/Inventory), DimDate và ETL inline
> đã tồn tại và hoạt động từ trước. Thực sự thiếu/lỗi: bug TotalProcessed luôn 0, stub
> InventoryReport, topic names sai, thiếu POS/HR/Product consumers và endpoints.

#### ✅ Tính năng đã có (xác nhận sau đối chiếu code thật)

**Domain Models** (`ERP.RPT.Domain/Models/FactSalesOrder.cs`)
- `FactSalesOrder`, `FactPayment`, `FactInventoryMovement`, `DimDate` — từ trước
- `FactPOSTransaction`, `FactHRPayslip`, `DimProduct` — thêm 2026-07-05

**Application**
- `GetSalesDashboardQuery` → tổng doanh thu, đơn hàng, AOV, revenue by day (thật)
- `GetPaymentSummaryQuery` → TotalProcessed/Refunded, ByMethod (đã sửa bug status)
- `GetInventoryReportQuery` → aggregation thật từ `FactInventoryMovements` (đã thay stub)
- `GetHRDashboardQuery` → tổng payslip, payroll, AverageSalary, ByPeriod — **mới 2026-07-05**
- `GetPOSDashboardQuery` → TotalTransactions, TotalRevenue, AverageValue, ByDay — **mới 2026-07-05**

**Infrastructure — Kafka Consumers** (topic → ETL → ReportingDB)
- `SalesOrderEventConsumer`: topics `sales-order-created/approved/shipped/delivered` — từ trước (SaleOrderService dùng RabbitMQ, chưa có `sales-topic` thật)
- `PaymentEventConsumer`: **sửa topic** `"payment-processed"` → `"payment-topic"` + `$type` dispatch — 2026-07-05
- `InventoryEventConsumer`: **sửa topic** `"inventory-updated"` → `"warehouse-topic"` + `$type` dispatch — 2026-07-05
- `POSEventConsumer`: `"pos-topic"` → `FactPOSTransaction` — **mới 2026-07-05**
- `HRMEventConsumer`: `"hrm-topic"` → `FactHRPayslip` — **mới 2026-07-05**
- `ProductEventConsumer`: `"product-topic"` → `DimProduct` (upsert) — **mới 2026-07-05**

**API**: `ReportsController`
- `GET /reports/sales` — sales dashboard
- `GET /reports/payments` — payment summary
- `GET /reports/inventory` — inventory report
- `GET /reports/hr` — HR payroll dashboard — **mới 2026-07-05**
- `GET /reports/pos` — POS transaction dashboard — **mới 2026-07-05**
- `GET /reports/sales/export` — CSV download — **mới 2026-07-05**
- `GET /reports/payments/export` — CSV download — **mới 2026-07-05**

#### ⚠️ Còn thiếu (tồn đọng sau 2026-07-05)

| Tính năng | Ưu tiên | Ghi chú |
|-----------|---------|---------|
| `DimCustomer`, `DimEmployee` | Thấp | PBI tốt hơn với dim riêng; hiện join bằng GUID |
| Pre-aggregated materialized views | Thấp | SQL Server Indexed View cho large dataset |
| Export to Excel (.xlsx) | Thấp | Cần thư viện ClosedXML hoặc EPPlus |
| SalesOrder Kafka consumer thật | Trung | SaleOrderService hiện dùng RabbitMQ, chưa publish lên Kafka |
| APNS push (iOS native) | — | NotificationService vẫn thiếu; không liên quan |

#### Luồng Vận Hành

```
[Mọi service] → OutboxProcessor → Kafka topic per service (TypeNameHandling.All)
→ ReportingService: 6 Kafka consumers (BackgroundService):

payment-topic:   $type.Contains("PaymentProcessed")  → FactPayments (insert)
                 $type.Contains("PaymentRefunded")   → FactPayments (status=Refunded)
warehouse-topic: $type.Contains("StockMoved")|...    → FactInventoryMovements (insert)
pos-topic:       $type.Contains("TransactionCreated") → FactPOSTransactions (insert)
hrm-topic:       $type.Contains("PaySlipGenerated")  → FactHRPayslips (insert)
product-topic:   $type.Contains("ProductCreated")|.. → DimProducts (upsert)

Admin → GET /reports/hr?from=2026-01-01&to=2026-06-30
     → SELECT ... FROM FactHRPayslips GROUP BY Period
     → return HRDashboardDto { TotalPayslips, TotalPayroll, AverageSalary, ByPeriod }

Admin → GET /reports/sales/export → response: text/csv, Content-Disposition: attachment
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
| `InventoryReserved` | Warehouse | Shipping ✅ (`MarkReadyForPickup()` thật, mới 2026-07-03 — trước đây transport có sẵn nhưng handler chỉ log) | Notification |
| `InventoryUpdated` | Warehouse | ProductService ✅ (stock sync, 2026-07-03) | Reporting |
| `PurchaseOrderReceived` | PurchaseOrder | Warehouse ✅ (mới 2026-07-03, khớp theo ProductSku) | Accounting |
| `ReceiptCompleted` | Warehouse | — (mới 2026-07-03) | Notification, Accounting |
| `StockAlert` | Warehouse | — (mới 2026-07-03) | PurchaseOrder (tự tạo PR — chưa làm), Notification |
| `ProductCreated` | Product | POS ✅, Promotion ✅ | SaleOrder, Reporting |
| `ProductUpdated` | Product | — (mới 2026-07-03) | SaleOrder, POS, Promotion |
| `ProductPriceChanged` | Product | SaleOrder ✅ (chỉ flag cảnh báo, 2026-07-03), POS ✅ (mới 2026-07-03 — handler đã thật từ trước nhưng chưa đăng ký/dispatch) | Promotion cần cập nhật consumer để đọc field mới |
| `UserUpdated` | Identity | — (mới 2026-07-03) | CRM, HRM |
| `SalesOrderDelivered` | SaleOrder | — (mới 2026-07-03) | Promotion (loyalty points), CRM (purchase history) |
| `SalesOrderPartiallyShipped` | SaleOrder | — (mới 2026-07-03) | Warehouse, Notification |
| `SalesOrderReturned` | SaleOrder | — (mới 2026-07-03) | Payment (hoàn tiền thật), Accounting, Warehouse (restock) |
| `PaymentAuthorized` | Payment | — (đã có sẵn từ trước, mục ❌ gốc ghi thiếu là sai; nay thực sự được trigger qua webhook — 2026-07-03) | Accounting |
| `PaymentProcessed` | Payment | POS ✅ (mới 2026-07-03 — khớp theo Transaction.Id == OrderId cho kịch bản click-and-collect) | Accounting, CRM, Promotion, Notification, Reporting |
| `PaymentFailed` | Payment | — | Notification, SaleOrder (saga compensation) |
| `InvoiceCreated` | Accounting | Payment ✅ (mới 2026-07-03 — tạo/link payment theo invoice) | Reporting |
| `ShipmentCreated` | Shipping | — | Notification, Reporting |
| `DeliveryConfirmed` | Shipping | — | SaleOrder, Promotion, Accounting |
| `TransactionCreated` | POS | — (publish nay đã thật qua Outbox+Kafka `pos-topic`, mới 2026-07-03 — trước đây hoàn toàn không tồn tại processor nào) | Warehouse (trừ tồn kho — chưa có consumer), Accounting, Reporting |
| `PromotionCreated` | Promotion | POS ✅ (xác nhận có sẵn và hoạt động thật) | SaleOrder |
| `CouponRedeemed` | Promotion | — | Reporting |
| `EmployeeHired` | HRM | — | Identity, Notification |
| `PaySlipGenerated` | HRM | — | Accounting, Notification |

### Events chưa có (cần tạo)

| Event cần tạo | Service | Trigger | Consumers |
|---------------|---------|---------|-----------|
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

> **Cập nhật 2026-07-03:** SaleOrderService (mục 3.3), PurchaseOrderService (mục 3.4)
> và WareHouseService (mục 3.5) đã xử lý xong — PurchaseOrderService hoá ra không phải
> "thiếu" mà là 7/8 handler chính tồn tại nhưng thân hàm rỗng (xem mục 3.4). Nhóm này
> nay không còn service nào — 3.8-3.14 sẽ được rà soát tuần tự tiếp theo, có thể phát
> hiện thêm case tương tự.

| Service | Command còn thiếu | Query còn thiếu |
|---------|-------------------|----------------|
| ~~SaleOrderService~~ | ~~Approve, Cancel, Update, Ship, ListOrders~~ (đã xong 2026-07-03) | ~~ListOrders, GetByCustomer~~ (đã xong) |
| ~~PurchaseOrderService~~ | ~~Create(handler), Approve, ReceiveGoods, Cancel~~ (đã xong 2026-07-03 — thực chất là sửa stub rỗng) | ~~List, GetById~~ (đã xong) |
| ~~WareHouseService~~ | ~~ReceiveStock, AdjustStock, TransferStock, LocationCRUD~~ (đã xong 2026-07-03) | ~~StocktakeQuery~~ (đã xong) |

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
| WareHouseService | `sales_order.SalesOrderCreated/Cancelled/Shipped`, `purchasing-topic` PurchaseOrderReceived (mới 2026-07-03, thay thế PO Created dead code cũ) | RabbitMQ + Kafka |
| PaymentService | `sales_order.SalesOrderCreatedEvent`; `accounting-topic` InvoiceCreated (mới 2026-07-03) | RabbitMQ + Kafka |
| ShippingService | `sales_order.SalesOrderApprovedEvent`, `warehouse_exchange` InventoryReserved (nay `MarkReadyForPickup()` thật, trước đây chỉ log — 2026-07-03) | RabbitMQ (cả hai) |
| AccountingService | `sales_order.SalesOrderCreatedEvent`, `purchasing-topic`, `payment-topic` | RabbitMQ + Kafka |
| CRMService | `identity-topic` UserRegistered, `payment-topic`, `sales_order.SalesOrderCreatedEvent` | Kafka + RabbitMQ |
| POSService | `product-topic` ProductCreated/ProductPriceChanged (ProductPriceChanged mới nối 2026-07-03), `promotion-topic` PromotionCreated, `payment-topic` PaymentProcessed (mới 2026-07-03) | Kafka |
| PromotionService | `product-topic` ProductCreated, `sales_order.SalesOrderCreatedEvent` | Kafka + RabbitMQ |
| HRMService | `mst-topic` (đã có sẵn từ trước, không đổi) | Kafka |
| NotificationService | `identity-topic` (đã có sẵn từ trước, không đổi) | Kafka |
| ReportingService | Sales/Payment/Inventory topics (đã có sẵn từ trước — tài liệu cũ ghi nhầm là thiếu) | Kafka |

**Còn thiếu, chưa làm trong đợt này:** ReportingService chưa consume POS/HR events
(chưa có `FactPOSTransactions`/`DimEmployees` model dù bảng SQL đã có); ProductService
**đã publish** `ProductUpdated`/`ProductPriceChanged` từ 2026-07-03 (mục 3.2). POS's
consumer đã nối lại để đọc giá thật (mục 3.8, 2026-07-03); Promotion's consumer vẫn
đọc theo field cũ (giá mặc định 0) — cần cập nhật, chưa làm ở đợt này. WarehouseService
vẫn cần tự thêm consumer cho POS's `pos-topic` (`TransactionCreatedEvent`) để thực sự
trừ tồn kho khi bán tại quầy — publish giờ đã thật (mục 3.8) nhưng phía consume là
việc của Warehouse khi rà soát tới. `OrderFulfillmentSaga` (SaleOrderService) vẫn là
dead code, chưa quyết định giữ lại làm true orchestration hay bỏ hẳn để dùng
choreography qua các consumer mới.

### Nhóm 4: Thiếu external integrations

| Integration | Cần cho | Ưu tiên |
|------------|---------|---------|
| ~~Payment Gateway (VNPay)~~ | ~~PaymentService~~ | **Đã có sẵn và xác nhận thật, webhook nay nối vào domain (2026-07-03)** — Stripe/MoMo/ZaloPay vẫn chưa có |
| Email Provider (SendGrid/SES) | NotificationService | Cao |
| SMS Provider (Twilio/VIETTEL) | NotificationService | Cao |
| FCM/APNS Push | NotificationService | Cao |
| ~~Carrier API (GHN)~~ | ~~ShippingService~~ | **Đã có sẵn và xác nhận thật, nay nối vào luồng tạo shipment chính (2026-07-03)** — GHTK/J&T/FedEx/DHL vẫn chưa có |
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
   mục 3.4.
3. ~~WarehouseService: ReceiveStockCommand, LocationCRUD API, StockAdjust~~ (đã xong
   2026-07-03 — cộng thêm Stocktake, Transfer Order, Lot/Serial/Expiry tracking,
   Inventory valuation, và sửa 1 crash-risk EF mapping bug độc lập với danh sách ❌
   gốc) — xem mục 3.5
4. ~~PaymentService: Gateway integration (VNPay/Stripe webhook)~~ (đã xong 2026-07-03
   — VNPay đã thật từ trước, nhưng webhook verify chữ ký xong không hề cập nhật DB;
   đã nối thật + refund gateway + saved cards + installment + reconciliation) — xem
   mục 3.6. Stripe/MoMo/ZaloPay vẫn chưa có, chỉ VNPay.
5. ~~ShippingService: Carrier API (GHN/GHTK), rate calculation~~ (đã xong 2026-07-03
   — GHN đã thật từ trước nhưng không nằm trên đường đi chính khi tạo shipment; đã
   nối thật + rate/ETA theo tỉnh + RMA + POD + label PDF + sửa 1 crash-risk
   `AddHttpClient` thiếu) — xem mục 3.7. GHTK/J&T/FedEx/DHL vẫn chưa có.
```

### Sprint 2 — Business Logic Services

```
6. AccountingService: Tất cả commands, queries, controllers, event consumers
7. CRMService: Tất cả commands, queries, controllers, event consumers
8. HRMService: Tất cả commands, queries, controllers, event consumers
9. NotificationService: Email/SMS/Push provider implementation
10. PromotionService: ✅ Hoàn thành 2026-07-04 — FlashSale entity, rules engine, bulk coupons, scheduler, 3 controllers
```

### Sprint 3 — Analytics & Polish

```
11. ReportingService: Kafka consumers, fact/dim tables, ETL handlers
12. ProductService: ~~Full-text search~~ (đã xong), ~~bulk import~~ (đã xong
    2026-07-03, CSV), ~~event publishing đầy đủ~~ (đã xong 2026-07-03 —
    ProductUpdated/ProductPriceChanged) — xem mục 3.2
13. ~~POSService: Event consumers, Terminal management, offline sync~~ (đã xong
    2026-07-03 — phát hiện service này hoàn toàn không có `OutboxProcessor`, domain
    event ghi đúng vào Outbox nhưng không bao giờ lên Kafka; đã thêm + Terminal entity
    + offline sync batch + split payment + denomination counting + Z-report) — xem
    mục 3.8. WarehouseService vẫn cần tự thêm consumer cho `pos-topic` để trừ tồn kho.
14. IdentityService: ~~2FA~~ (đã xong), ~~audit log~~ (đã xong 2026-07-03),
    ~~session management~~ (đã xong 2026-07-03), ~~tenant isolation (IDOR)~~,
    ~~2FA-bypass qua external-login~~ (đã xong 2026-07-08) — xem mục 3.1
```

### Sprint 4 — Production Readiness

```
15. Integration tests cho tất cả end-to-end flows
16. Performance testing (load testing với k6/Artillery)
17. Docker Compose: health checks, restart policies
18. Kubernetes: HPA, resource limits, liveness/readiness probes
19. Observability: OpenTelemetry tracing, Grafana dashboards
20. ~~Security: Rate limiting, input sanitization, OWASP hardening~~ — **IdentityService
    đã xong 2026-07-08** (rate limiting đăng nhập, FluentValidation, CORS/HSTS/security
    headers, secrets hygiene, cross-service JWT trust boundary cho 13 service khác —
    xem mục 3.1). Rate limiting/OWASP hardening cho các endpoint khác của 13 service
    còn lại (ngoài phạm vi auth) vẫn chưa làm.
```

---

*Tài liệu này phản ánh trạng thái hệ thống tại ngày 2026-06-28 và cần được cập nhật sau mỗi sprint.*
