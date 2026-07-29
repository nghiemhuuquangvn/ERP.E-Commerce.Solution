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

## 7. Event Design

**Published** (chỉ 2 event, xác nhận đúng CLAUDE.md — service KHÔNG publish domain event nghiệp vụ nào khác ra ngoài):
- `NotificationSentEvent(NotificationId, RecipientId, Channel)` — khi `MarkAsSent()`
- `NotificationFailedEvent(NotificationId, RecipientId, Channel, Error)` — khi `MarkAsFailed()`

**Consumed** (đã xác nhận 2 consumer thật đang chạy, không phải 1 như CLAUDE.md ghi):
| Consumer | Transport | Sự kiện | Trạng thái |
|---|---|---|---|
| `IdentityEventConsumer` | Kafka `identity-topic` | `TwoFactorCodeGenerated` (gửi OTP), `UserCreated`, ... | Sống |
| `SalesOrderRabbitMqConsumer` | RabbitMQ `sales_order_events` | `sales_order.SalesOrderCreatedEvent`, `sales_order.SalesOrderApprovedEvent` | Sống — gửi email xác nhận/duyệt đơn hàng |

11 service còn lại (Warehouse, Accounting, CRM, HRM, Payment, Shipping, POS, Promotion, Purchasing, Product, Reporting) **chưa có consumer nối dây** cho Notification — thiết kế "tiêu thụ toàn bộ 39+ sự kiện" trong CLAUDE.md vẫn là mục tiêu dự kiến, không phải hiện trạng.

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
| Hệ thống ngoài | Webhook cho phép bên thứ ba tự đăng ký nhận `notification.sent`/`notification.failed` theo `EventTypes` filter |

## 10. So sánh & Reconciliation với project hiện tại (2026-07-29)

Phát hiện chính: CLAUDE.md nói sai hiện trạng consumer (nói "chỉ 1 sống" nhưng thực tế có 2 — thiếu `SalesOrderRabbitMqConsumer`). Domain model có 1 property chết (`NotificationPreference.DisabledChannels` — tồn tại nhưng không method nào ghi vào, cơ chế thật dùng 4 cờ boolean). Không phát hiện gap nghiêm trọng khác — toàn bộ vòng đời gửi/retry/log là logic thật đang chạy.
