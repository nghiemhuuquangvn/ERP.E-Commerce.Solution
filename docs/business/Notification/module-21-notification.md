# Module 21 — Notification
## Technical Design Specification | ERP.E-Commerce.Solution

> Số module (21) được gán mới trong đợt rà soát 2026-07-29, chưa từng có quy ước đánh số toàn cục. Viết theo hướng **reconciliation** — NotificationService đã có code đầy đủ. Xem thêm `docs/business/Notification/NotificationPrivacy.md` (tài liệu hẹp, chỉ về quyền riêng tư dữ liệu người nhận) cho bối cảnh trước đó.
>
> **Cập nhật quan trọng so với CLAUDE.md**: CLAUDE.md ghi "chỉ có 1 Kafka consumer sống (`IdentityEventConsumer`) cho `identity-topic`, 12 service còn lại là thiết kế dự kiến chưa nối dây" — thông tin này **đã lỗi thời**. Xác nhận qua code thật: ngoài `IdentityEventConsumer` (Kafka), còn có **`SalesOrderRabbitMqConsumer`** (RabbitMQ, exchange `sales_order_events`, đã bind 2 routing key `SalesOrderCreatedEvent`/`SalesOrderApprovedEvent`) đang sống và gửi thông báo xác nhận/duyệt đơn hàng thật. Vẫn còn 11 service khác chưa có consumer nối dây, nhưng "chỉ 1 consumer sống" không còn đúng.

---

## 1. Mục tiêu và phạm vi

NotificationService (Port 5013, Code: NTF, database `NotificationDB`) là service **chỉ tiêu thụ sự kiện** (consumer-only, không publish domain event nào ra ngoài — khớp với CLAUDE.md) — gửi thông báo đa kênh (Email/SMS/Push/In-App) dựa trên Template, quản lý tuỳ chọn nhận thông báo theo từng User (Preference/Opt-out), và cung cấp Webhook cho hệ thống bên thứ ba đăng ký nhận sự kiện.

Phạm vi **không bao gồm**: soạn nội dung nghiệp vụ (nội dung do service gọi tới cung cấp qua Template hoặc raw Subject/Body) — Notification chỉ là hạ tầng gửi/theo dõi trạng thái gửi, không phải nguồn sự thật của nội dung nghiệp vụ.

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Các service khác (Identity, Sales, ...) | Publish event → trigger gửi thông báo tự động qua consumer |
| End User (qua Preferences) | Tự cấu hình kênh nhận thông báo, opt-out toàn bộ |
| Admin/CSKH | Gửi thông báo thủ công/hàng loạt (`Send`/`BulkSend`), quản lý Template |
| Hệ thống bên thứ ba | Đăng ký Webhook để nhận sự kiện notification.sent/notification.failed |

## 3. Quy trình nghiệp vụ chính

1. Notification được tạo (`Notification.Create`) ở trạng thái `Queued` — hoặc do gọi trực tiếp `POST /notifications` (Send/BulkSend), hoặc do consumer nội bộ tự tạo khi nhận event từ service khác.
2. Nếu có `ScheduledFor` trong tương lai, `IsReadyToSend()` trả về false cho đến đúng giờ — cơ chế lập lịch gửi.
3. Gửi: `MarkAsSending()` → thử gửi qua kênh tương ứng → `MarkAsSent()` (thành công, phát `NotificationSentEvent`) hoặc `MarkAsFailed(error)` (thất bại, tăng `RetryCount`, phát `NotificationFailedEvent`).
4. Retry tự động theo exponential backoff: `CanRetry()` cho phép tối đa 3 lần thử lại, `ShouldRetryNow()` tính thời điểm retry kế tiếp = 30s × 2^(RetryCount−1) sau lần thất bại gần nhất (30s sau lần 1, 60s sau lần 2).
5. Nếu gateway gửi (email/SMS provider) báo bounce, `MarkAsBounced(reason)` — trạng thái riêng biệt với Failed, không tính vào RetryCount.
6. Toàn bộ vòng đời được ghi lại vào `NotificationLog` (append-only) ngay tại từng bước chuyển trạng thái — không phải log ứng dụng rời rạc mà là 1 phần domain model.
7. User có thể tắt từng kênh (`UpdateChannels`) hoặc `OptOut()` toàn bộ (tắt Email/SMS/Push cùng lúc, ghi nhận `OptOutAt` — lưu ý: `InAppEnabled` KHÔNG bị tắt bởi OptOut, chỉ 3 kênh còn lại).

## 4. Domain Model (Aggregate Roots — DDD, đã xác nhận đúng code thật)

```
Notification (Aggregate Root)
 ├─ Channel: Email | Sms | Push | InApp
 ├─ Status: Queued → Sending → Sent → Delivered
 │                          └→ Failed → (retry) hoặc Bounced
 ├─ Priority: (Low/Normal/High/Critical — xem NotificationPriority enum)
 └─ Logs (owned collection, append-only)
     NotificationLog (Entity) — EventType, Details, EventTimestamp

NotificationTemplate (Aggregate Root)
 ├─ Code (định danh duy nhất), Channel, Subject, Body, Variables (mô tả placeholder)
 └─ Render(variables dict) — thay thế {key} bằng value trong Body

NotificationPreference (Aggregate Root)
 ├─ EmailEnabled / SmsEnabled / PushEnabled / InAppEnabled
 ├─ DisabledChannels (danh sách phụ, hiện không có method nào ghi vào — xem ghi chú)
 └─ OptOutAt

WebhookEndpoint (Aggregate Root)
 ├─ Url, Secret (dùng ký HMAC payload gửi đi — cần xác nhận thêm ở API layer)
 └─ EventTypes (danh sách lọc sự kiện, rỗng = nhận tất cả)
```

**Ghi chú kỹ thuật**: `NotificationPreference.DisabledChannels` là property `IReadOnlyCollection` nhưng không có method public nào (`AddDisabledChannel`, v.v.) từng thêm phần tử vào `_disabledChannels` — trường này tồn tại trong domain model nhưng chưa có luồng nghiệp vụ nào sử dụng, khác với `UpdateChannels`/`OptOut` (dùng 4 cờ boolean riêng) mới là cơ chế thật đang chạy.

## 5. Database Design (`NotificationDB`)

| # | Bảng | Cột chính |
|---|---|---|
| 1 | `NotificationTemplates` | Id, Code (unique cùng TenantId), Name, Channel, Subject, Body, Variables, IsActive |
| 2 | `Notifications` | Id, RecipientId, RecipientEmail/Phone/DeviceToken, Channel, TemplateId, Subject, Body, Status, Priority, ScheduledFor, SentAt/DeliveredAt/FailedAt, RetryCount, ErrorMessage |
| 3 | `NotificationLogs` | Id, NotificationId (FK), EventType, Details, EventTimestamp |
| 4 | `NotificationPreferences` | Id, UserId (unique cùng TenantId), EmailEnabled, SmsEnabled, PushEnabled, InAppEnabled, OptOutAt |
| 5 | `TenantNotificationSettings` / `TenantEmailSettings` / `TenantSmsSettings` | cấu hình hai tầng System/Tenant cho từng kênh gửi (SMTP, SMS gateway...) |
| 6 | `WebhookEndpoints` (nếu có bảng riêng — xác nhận cùng lúc build docx) | Name, Url, Secret, IsActive, EventTypesRaw |

**Index cần lưu ý** (chi tiết đầy đủ ở `docs/architecture/database-performance-indexing-review.md`): `IX_Notifications_Status (Status, CreatedAt)` không filtered, dẫn đầu bằng cột trạng thái đổi liên tục (Queued→Sending→Sent/Failed/Bounced) trên bảng nhiều khả năng có tốc độ ghi cao nhất hệ thống — ứng viên hàng đầu để chuyển thành filtered index theo đúng pattern truy vấn thật (ví dụ "tìm các bản ghi Queued cần gửi" → `(ScheduledFor) WHERE Status = 0`).

## 6. API Design (REST)

```
POST   /api/v1/notifications                      (notifications:send)
GET    /api/v1/notifications/{id}                  (notifications:view + ownership check)
GET    /api/v1/notifications/history/{recipientId} (notifications:view + ownership check)
POST   /api/v1/notifications/bulk                  (notifications:send, 202 Accepted)

# Templates, Preferences, Webhooks, Settings — controllers riêng, xem TemplatesController /
# PreferencesController / WebhooksController / SettingsController
```

**Ghi chú bảo mật đáng chú ý**: `GET /{id}` và `GET /history/{recipientId}` đều có kiểm tra "chỉ chính chủ hoặc người có quyền `notifications:send`" — không phải cứ có `notifications:view` là đọc được thông báo của người khác. Đây là cơ chế ownership-check tương tự PreferencesController, ngăn một caller có quyền view chung dò được `NotificationId` của người khác rồi đọc trộm nội dung.

> **2026-10-26 (P1-6):** the customer messages, recipients, dedup and screens are described in `CustomerNotifications.md`; the consumer list in §7 / §9 is stale (Notification now consumes Sales, Payment, Shipping, Promotion and CRM events).

## 7. Event Design

**Published** (chỉ 2 event, xác nhận đúng CLAUDE.md — service KHÔNG publish domain event nghiệp vụ nào khác ra ngoài):
- `NotificationSentEvent(NotificationId, RecipientId, Channel)` — khi `MarkAsSent()`
- `NotificationFailedEvent(NotificationId, RecipientId, Channel, Error)` — khi `MarkAsFailed()`

**Consumed** (đã xác nhận 4 consumer thật đang chạy — thêm 2 từ 2026-08-04, xem
`docs/fixes/2026-08-04_ERP4Features_SeedDataAndFeatures_Summary.md` §C.2):
| Consumer | Transport | Sự kiện | Trạng thái |
|---|---|---|---|
| `IdentityEventConsumer` | Kafka `identity-topic` | `TwoFactorCodeGenerated` (gửi OTP), `UserCreated`, ... | Sống |
| `SalesOrderRabbitMqConsumer` | RabbitMQ `sales_order_events` | `sales_order.SalesOrderCreatedEvent`, `sales_order.SalesOrderApprovedEvent` | Sống — gửi email xác nhận/duyệt đơn hàng |
| `NotificationKafkaConsumer` (`PurchasingTopic`, thêm 2026-08-04) | Kafka `purchasing-topic` | `PurchaseOrderPendingFinanceApprovalDomainEvent`, `PurchaseOrderApprovedDomainEvent` | Sống — báo người có quyền `purchase_order:finance_approve` khi PO chờ duyệt tài chính, báo xác nhận khi PO được duyệt xong |
| `NotificationKafkaConsumer` (`HrmTopic`, thêm 2026-08-04) | Kafka `hrm-topic` | `LeaveApprovedEvent`, `LeaveRejectedEvent` | Sống — báo nhân viên kết quả duyệt đơn nghỉ phép |

**Bug chặn cả 2 consumer mới lúc mới thêm, đã sửa cùng đợt**: Newtonsoft
`JObject.Value<Guid?>()` ném `InvalidCastException` khi giá trị JSON là chuỗi
thuần (mọi trường GUID trong mọi event đều vậy) — lỗi này ảnh hưởng **toàn bộ**
Kafka consumer của service này, kể cả 2 consumer cũ ở trên, không chỉ 2
consumer mới. Thay bằng helper `GuidOrEmpty(JObject, string)` dùng
`Guid.TryParse`.

9 service còn lại (Warehouse, Accounting, CRM, Payment, Shipping, POS,
Promotion, Product, Reporting) **chưa có consumer nối dây** cho Notification —
thiết kế "tiêu thụ toàn bộ 39+ sự kiện" trong CLAUDE.md vẫn là mục tiêu dự
kiến, không phải hiện trạng.

## 8. Business Rules & Validation

- Notification mới luôn khởi tạo `Queued`, không phân biệt có `ScheduledFor` hay không (code hiện tại gán `Status = Queued` cho cả 2 nhánh — trường hợp `scheduledFor.HasValue` không có nhánh trạng thái riêng, chỉ khác ở việc `IsReadyToSend()` sẽ kiểm tra thêm điều kiện thời gian).
- Retry tối đa 3 lần, chỉ áp dụng khi `Status == Failed` (không tự retry khi Bounced).
- `OptOut()` tắt Email/SMS/Push nhưng giữ nguyên In-App — người dùng luôn nhận được thông báo trong ứng dụng ngay cả khi đã từ chối các kênh khác.
- Đọc thông báo của người khác yêu cầu quyền nâng cao `notifications:send`, không chỉ `notifications:view`.

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 2 (Identity) | `TwoFactorCodeGenerated` → gửi OTP qua SMS/Email (duy nhất kênh giao OTP, không đồng bộ trực tiếp) |
| Module 5 (Sales) | `SalesOrderCreatedEvent`/`SalesOrderApprovedEvent` → email xác nhận/duyệt đơn |
| Purchasing Service (thêm 2026-08-04) | `PurchaseOrderPendingFinanceApprovalDomainEvent`/`PurchaseOrderApprovedDomainEvent` → báo người duyệt tài chính / xác nhận PO đã duyệt xong |
| HRM Service (thêm 2026-08-04) | `LeaveApprovedEvent`/`LeaveRejectedEvent` → báo nhân viên kết quả duyệt đơn nghỉ phép |
| Hệ thống ngoài | Webhook cho phép bên thứ ba tự đăng ký nhận `notification.sent`/`notification.failed` theo `EventTypes` filter |

## 10. So sánh & Reconciliation với project hiện tại (2026-07-29)

Phát hiện chính: CLAUDE.md nói sai hiện trạng consumer (nói "chỉ 1 sống" nhưng thực tế có 2 — thiếu `SalesOrderRabbitMqConsumer`). Domain model có 1 property chết (`NotificationPreference.DisabledChannels` — tồn tại nhưng không method nào ghi vào, cơ chế thật dùng 4 cờ boolean). Không phát hiện gap nghiêm trọng khác — toàn bộ vòng đời gửi/retry/log là logic thật đang chạy.


---

## 11. Approver notifications, record links, read-all and device tokens (2026-11-05, ROADMAP M5)

### 11.1 Purpose and actors
People who must decide something (approve a purchase order, a sales order, a leave request) are told **in the bell of the Admin app** at the moment the thing starts waiting for them, and tapping the message opens the record. Before this, nothing was raised at that moment: the finance step e-mailed a hard-coded demo address and the other three steps were silent.

| Actor | Role |
|---|---|
| Submitter | Submits a PO / sales order / leave request (also gets the message if they hold the permission - the actor is not excluded). |
| Approver | Holds the permission that decides the step; receives one bell message per fact. |
| IdentityService | Answers "who holds permission X in this tenant". |
| NotificationService | Finds the approvers, creates one in-app message each (and a push row when push is on). |
| Admin mobile app | Shows the bell, opens the record, marks read, raises a phone alert while open. |

### 11.2 Requirements and acceptance criteria
| ID | Requirement | Acceptance criterion |
|---|---|---|
| N1 | A submitted purchase order tells the holders of `purchase_order:approve`. | Submit a PO on a tenant whose administrator holds the permission -> within seconds that user's history has an InApp row "Purchase order PO-x is waiting for your approval" with `EntityType=PurchaseOrder`, the PO id, `RouteKey=purchase-order`. |
| N2 | A PO that passed the manager and needs finance approval tells the holders of `purchase_order:finance_approve` (replaces the demo e-mail). | The finance event creates one row per holder; no e-mail to `finance-team@erp-demo.local` is made any more. |
| N3 | A submitted sales order tells the holders of `sales_order:approve`. | Row with `EntityType=SalesOrder`, `RouteKey=sales-order`. |
| N4 | A new leave request tells the holders of `hrm:leave:manage`. | Row with `EntityType=LeaveRequest`, `RouteKey=leave-request`. |
| N5 | The same fact never reaches the same person twice. | `DedupKey = "<event>:<objectId>:<userId>"` (+ `:push`); a redelivered event creates nothing new. |
| N6 | A person who turned bell messages off is skipped. | `SkipWhenOptedOut` on the command. |
| N7 | `PATCH notifications/read-all` marks the caller's own unread messages read and answers `{marked:n}`. | Never takes a recipient from the request; a second call answers 0. |
| N8 | A phone registers / removes its push token. | `PUT device-tokens {token,deviceId,platform}` (upsert per user + device, a token held by another row moves to the caller), `DELETE device-tokens/{deviceId}` (204 even when absent). User id from the token. |
| N9 | Push is real only when Firebase is configured. | Without `Fcm:ProjectId` + `Fcm:ServiceAccountPath`: `NullPushSender`, no Push rows are created. |

### 11.3 Business rules
1. **Approvers = permission holders.** NotificationService asks IdentityService (`GET internal/v1/permissions/{code}/users`, service token of caller `notification`, tenant from the token); the answer is cached 60 s per tenant + permission. Identity mirrors how a token gets its permissions: active user of the tenant, active role of the tenant, role holds the permission, `PermissionScopes.IsGrantableIn` (a platform-only permission has no holder in an ordinary tenant); in the platform tenant the SuperAdmin role holds everything. Per-user grants are **not** considered (tokens do not carry them either).
2. At most **200** approvers per event are told (the rest are logged); more means a wrong role setup.
3. A failing lookup throws, so the consumer's existing retry (5 times, then drop with a log) applies - nobody is silently skipped.
4. Messages are English text (the bell shows what the service wrote); translation of server text is a recorded limitation.
5. Leave **approved / rejected** still go to the demo address `employee-{id}@erp-demo.local` (no employee -> user id) - unchanged, recorded.
6. A push row is made only when `IPushStatus.IsEnabled`; a delivery goes to every device of the user, a dead token (`UNREGISTERED` / 404 / invalid argument) is removed, one broken device does not stop the others, and the delivery fails (is retried by the dispatch job) only when **no** device could be reached.

### 11.4 Flow
Main: PO `Submit()` raises `PurchaseOrderSubmittedDomainEvent` -> outbox -> Kafka `purchasing-topic` -> `NotificationKafkaConsumer` (tenant scope from the event) -> `PurchaseOrderSubmittedIntegrationEvent` -> handler -> `ApproverNotifier` -> Identity lookup -> one `SendNotificationCommand` (InApp) per approver -> bell. Sales: `SalesOrderSubmittedEvent` on RabbitMQ `sales_order_events`, routing key `sales_order.SalesOrderSubmittedEvent`. HRM: `LeaveRequestSubmittedEvent` on Kafka `hrm-topic`.
Alternative: nobody holds the permission -> logged, no message. Exception: Identity down -> message retried, then dropped after 5 tries (R138).

### 11.5 Data model (migration `Notification/015_NotificationLinksAndDeviceTokens.sql`, idempotent)
* `Notifications.EntityType` (100), `EntityId` (100), `RouteKey` (200), all NULL. Carried through `Notification.Create`, `SendNotificationCommand`, `NotificationDto` (all three builders) and the history body.
* `DeviceTokens` (BaseEntity columns): `UserId`, `Token` (500), `DeviceId` (200), `Platform` (android / ios), `LastSeenAt`; unique `(TenantId, Token)` and `(TenantId, UserId, DeviceId)`; index `(TenantId, UserId)`. Tenant-filtered like every tenant-owned row.

### 11.6 Integrations and configuration
* Identity: `InternalPermissionsController` (`[AuthorizeInternalService("notification")]`; never routed in the Gateway). Compose: identity `ServiceAuth__TrustedCallers__notification`; notification `ServiceAuth__Name=notification`, `ServiceAuth__SigningKeys__Source=Ephemeral`, `Identity__BaseUrl`; the service publishes `/internal/jwks.json` (`MapServiceJwks`). **Kubernetes manifests were not updated** (R140).
* FCM HTTP v1: `Fcm__ProjectId`, `Fcm__ServiceAccountPath` (a service-account JSON mounted as a secret); the old legacy `FcmPushSender` (retired Google endpoint) was deleted.
* Gateway: `/ntf/api/v1/{everything}` already routes every verb; `/api/v1/notifications/{everything}` does not route DELETE (device tokens use the `/ntf` route).

### 11.7 Permissions
`notifications:view` for `read-all` and history (in-action check, controller already on the legacy allowlist); `device-tokens` uses `[AuthorizeAnyAuthenticatedUser]` (own account only).

### 11.8 UAT
1. Submit a PO as a user while the glcheck administrator holds `purchase_order:approve` -> administrator's bell +1, the message names the PO. 2. Redeliver the event -> still +1. 3. `read-all` -> badge 0, second call `{marked:0}`. 4. `PUT device-tokens` with an empty token -> 400; with a token -> 204; `DELETE` -> 204. 5. A tenant where nobody holds `sales_order:approve` -> no message, log line.

### 11.9 Assumptions and open issues
Record in DEFERRED-ISSUES R138-R143 (Identity lookup lag and retry, approver cap, the submitter is told too, push unverified without Firebase, polling-based phone alert, k8s manifests).
