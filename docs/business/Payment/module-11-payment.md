# Module 11 — Payment
## Technical Design Specification | ERP.E-Commerce.Solution

> Số module (11) được gán mới trong đợt rà soát 2026-07-29 — PaymentService trước đó chưa có tài liệu BA riêng. Viết theo hướng **reconciliation** hoàn toàn — mọi chi tiết đã đối chiếu trực tiếp với code thật (`src/services/PaymentService`).

---

## 1. Mục tiêu và phạm vi

PaymentService (Port 5009, Code: PAY, database `PaymentDB`) quản lý toàn bộ vòng đời giao dịch thanh toán: khởi tạo → xác thực (Authorize) → thu tiền (Capture) → hoàn tiền (Refund, hỗ trợ hoàn một phần nhiều lần) → đối soát (Settlement). Đây là service **duy nhất tích hợp cổng thanh toán thật** (VNPay) trong toàn hệ thống — không phải giả lập.

Phạm vi **không bao gồm**: tạo Invoice (Accounting sở hữu), xử lý đơn hàng (Sales sở hữu) — Payment chỉ nhận `OrderId`/`InvoiceId` tham chiếu, không lưu bản sao dữ liệu đơn hàng.

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Khách hàng (qua Storefront/POS) | Thực hiện thanh toán, được redirect qua cổng VNPay |
| OrderFulfillmentSaga (Sales) | Ra lệnh `InitiatePaymentCommand`/`RefundPaymentCommand` qua saga, không gọi trực tiếp REST |
| Kế toán/Tài chính | Xử lý đối soát (`ProcessSettlementCommand`), theo dõi payment bị gắn cờ gian lận |
| VNPay (cổng thanh toán) | Gửi callback redirect + IPN (server-to-server) xác nhận kết quả giao dịch |

## 3. Quy trình nghiệp vụ chính

1. `InitiatePaymentCommand` tạo `Payment` ở trạng thái `Pending` — **có cơ chế chống trùng lặp**: nếu đã tồn tại Payment cho cùng `OrderId` (unique index DB-level), trả về Payment đã có thay vì tạo mới — quan trọng vì lệnh này có thể được gọi lại (retry) từ saga.
2. Đồng thời, `IFraudDetectionService` kiểm tra số tiền — nếu vượt ngưỡng cấu hình (`Fraud:LargeAmountThreshold`), Payment bị gắn cờ `IsFlaggedForReview` (quy tắc duy nhất, không có chấm điểm hành vi/velocity check).
3. Khách được chuyển tới VNPay (`POST /payment-gateway/{id}/create-url`) → thanh toán → VNPay gọi lại qua `GET /payment-gateway/callback` (redirect trình duyệt) và/hoặc `POST /payment-gateway/ipn` (server-to-server, `[AllowAnonymous]` — an toàn nhờ chữ ký HMAC-SHA512, không phải JWT).
4. `ProcessGatewayCallbackCommand` là điểm xử lý chung cho cả 2 luồng trên: `Authorize()` → `Capture()` nếu thành công, `Fail()` nếu thất bại — **an toàn khi gọi lại nhiều lần** (nếu Payment đã ở trạng thái kết thúc `Captured`/`Failed` thì chỉ trả về kết quả hiện có, không xử lý lại).
5. Hoàn tiền: `RefundPaymentCommand` gọi `Payment.AddRefund()` (kiểm tra tổng đã hoàn không vượt quá số tiền gốc) → **gọi thật tới VNPay refund API** → cập nhật `Refund` thành Completed/Failed theo kết quả gateway → `ReconcileStatusWithConfirmedRefunds()` tính lại Status cuối cùng chỉ dựa trên các Refund đã **được gateway xác nhận** (tránh trường hợp gateway từ chối hoàn tiền nhưng Status vẫn hiển thị "đã hoàn").
6. Đối soát: `ProcessSettlementCommand` gom toàn bộ Payment đã `Captured` trong một khoảng thời gian, tạo `Settlement`, và gắn từng Payment vào Settlement đó (`AssignSettlement`).
7. Trả góp (Installment): tạo `InstallmentPlan` gắn với **một Payment cụ thể** (không gắn với phương thức thanh toán đã lưu), yêu cầu tối thiểu 2 kỳ; số tiền chia đều, kỳ cuối nhận phần dư làm tròn để tổng luôn khớp chính xác.

## 4. Domain Model (Aggregate Roots — DDD, đã xác nhận đúng code thật)

```
Payment (Aggregate Root)
 ├─ Status: Pending → Authorized → Captured → Refunded | PartiallyRefunded
 │                          └→ Failed / Cancelled (nhánh thoát sớm)
 └─ Refunds (owned collection)
     Refund (Entity) — Status: Pending → Processing → Completed | Failed

SavedPaymentMethod (Aggregate Root) — Token (opaque, KHÔNG lưu số thẻ thật — giảm phạm vi PCI-DSS)

InstallmentPlan (Aggregate Root, gắn 1-1 với 1 Payment cụ thể)
 └─ InstallmentLine (Entity) — Status: Pending → Paid | Overdue

Settlement (Aggregate Root) — Status: Pending → Processing → Settled
```

## 5. Database Design (`PaymentDB`, đã đối chiếu `src/Database/Payment/001-004*.sql`)

| # | Bảng | Cột chính |
|---|---|---|
| 1 | `Payments` | Id, OrderId (**unique index** — nền tảng cơ chế chống trùng), InvoiceId, Amount, Currency, Method, Status, TransactionRef, GatewayTransactionId, GatewayProvider, FailureReason, IsFlaggedForReview, FraudReviewReason, SettlementId (FK) |
| 2 | `PaymentRefunds` | Id, PaymentId (FK), Amount, Reason, Status, GatewayRefundId |
| 3 | `SavedPaymentMethods` | Id, CustomerId (**không có TenantId** — xem ghi chú), Provider, Token, Brand, Last4, ExpiryMonth/Year, IsDefault, IsActive |
| 4 | `InstallmentPlans` | Id, PaymentId (**unique index** — 1 Payment chỉ có tối đa 1 plan), TotalAmount, TotalInstallments |
| 5 | `InstallmentLines` | Id, PlanId (FK), SequenceNo, Amount, DueDate, Status, PaidAt |
| 6 | `Settlements` | Id, PeriodStart, PeriodEnd, TotalAmount, TransactionCount, Status, SettledAt |
| 7 | `SystemPaymentSettings` / `TenantPaymentSettings` | 2 tầng System/Tenant, `TenantPaymentSettings.VnPaySecretKeyEncrypted` (đã mã hoá trước khi lưu qua `ISecretProtector`) |
| 8 | `OutboxMessages` | chuẩn Outbox pattern |

**Ghi chú migration quan trọng**: migration `001` đặt tên cột gateway là `GatewayRef` nhưng entity C# luôn dùng `GatewayTransactionId` — `002` phải `sp_rename` để khớp, tự ghi rõ trong header: *"database tạo từ 001 một mình sẽ thiếu cột EF thực sự truy vấn/ghi, và `Payment.Authorize()` sẽ lỗi khi chạy với DB thật."* `003` tiếp tục bổ sung các cột `BaseEntity` còn thiếu (`IsDeleted`, `RowVersion`, ...) và `FailedAt` — cùng một dạng "migration vá lỗi lệch schema" như đã thấy ở CRM/Product.

**Cảnh báo an toàn dữ liệu**: giống CRM, **không có `HasQueryFilter` toàn cục** cho `IsDeleted`/`TenantId`. Còn nghiêm trọng hơn CRM: `PaymentRepository`'s các method truy vấn (`GetByIdAsync`, `GetByOrderIdAsync`, ...) **hoàn toàn không lọc `IsDeleted`** — kể cả kiểu lọc thủ công từng method mà CRM có cũng không tồn tại ở đây.

**Index cần lưu ý** (chi tiết ở tài liệu Index Review riêng): `IX_Payments_Status` là index đơn lẻ trên cột trạng thái đổi liên tục (Pending→Authorized→Captured→Refunded) — ứng viên gộp vào `(TenantId, Status)` hoặc chuyển filtered index.

## 6. API Design (REST)

```
GET/POST   /payments, /{id}, /order/{orderId}                    (payments:view / :create)
POST       /payments/{id}/authorize, /capture, /refund, /cancel    (payments:authorize/capture/refund/cancel)

GET/POST   /settlements, /reconciliation                          (payments:settlements:view / :manage)

POST       /payment-gateway/{paymentId}/create-url                 (payments:create)
GET        /payment-gateway/callback                               (public redirect, xác thực bằng chữ ký VNPay)
POST       /payment-gateway/ipn                                    ([AllowAnonymous] — VNPay server-to-server không mang JWT được, chữ ký HMAC là ranh giới bảo mật thật)

GET/POST   /payment-methods/customer/{customerId}, /{id}/set-default, DELETE /{id}  (payments:view / :create)
GET/POST   /installment-plans/payment/{paymentId}, /{id}/installments/{seq}/pay     (payments:view / :create / :capture)
```

## 7. Event Design

**Published** (Outbox → Kafka topic `payment-topic`):
- `PaymentInitiatedEvent`, `PaymentAuthorizedEvent`
- `PaymentProcessedEvent(PaymentId, OrderId, Amount, Currency, Method, ProcessedAt)` — phát khi `Capture()`
- `PaymentFailedEvent`, `PaymentRefundedEvent`
- **`SettlementCreated` — KHÔNG tồn tại trong code**, mặc dù CLAUDE.md ghi có. `Settlement` không phát domain event nào cả — cần sửa lại tài liệu kiến trúc.

**Consumed** (đã xác nhận, sửa lại phần CLAUDE.md ghi lỗi thời):
| Nguồn | Transport | Sự kiện thật | Ghi chú |
|---|---|---|---|
| Accounting | Kafka `accounting-topic` | `InvoiceCreatedEvent` | Nếu Payment cho OrderId đã tồn tại → liên kết Invoice; nếu chưa → tạo Payment mới (Method=BankTransfer mặc định) |
| Sales | RabbitMQ `sales_order_events` | `sales_order.InitiatePaymentCommand` / `sales_order.RefundPaymentCommand` | **Không còn** là subscribe trực tiếp `SalesOrderCreatedEvent` như CLAUDE.md ghi — đã đổi sang lệnh do saga phát ra (2026-07-14) để tránh khởi tạo thanh toán 2 lần |

## 8. Business Rules & Validation

- **Hoàn tiền một phần**: được hỗ trợ đầy đủ, nhiều lần, miễn tổng không vượt số tiền gốc — Status tự chuyển `PartiallyRefunded` hay `Refunded` tuỳ đã hoàn hết hay chưa.
- **Chống thanh toán trùng (double-charge) khi retry**: đảm bảo bằng unique index `OrderId` + kiểm tra tồn tại trước khi tạo trong `InitiatePaymentCommandHandler`.
- **Callback/IPN idempotent**: gọi lại nhiều lần trên Payment đã ở trạng thái kết thúc chỉ trả kết quả có sẵn, không xử lý lại.
- **InstallmentPlan** bắt buộc ≥2 kỳ, gắn cứng với 1 Payment cụ thể (không gắn với SavedPaymentMethod); một Payment chỉ được tạo tối đa 1 plan.
- `SavedPaymentMethod` chỉ lưu token từ cổng thanh toán (không lưu số thẻ thật) — giảm phạm vi tuân thủ PCI-DSS. Phương thức lưu đầu tiên của khách tự động thành mặc định.
- Kiểm tra gian lận chỉ dựa trên 1 quy tắc: số tiền vượt ngưỡng cấu hình — không có chấm điểm hành vi/tốc độ giao dịch (velocity check) vì Payment không có đủ dữ liệu khách hàng tại chỗ.

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 5 (Sales) | `OrderFulfillmentSaga` ra lệnh `InitiatePaymentCommand`/`RefundPaymentCommand` qua RabbitMQ, không gọi REST trực tiếp |
| Module 12 (Accounting) | Nhận `InvoiceCreatedEvent` → tự tạo/liên kết Payment |
| VNPay (bên ngoài) | Tích hợp thật: tạo URL thanh toán, xác thực callback/IPN bằng chữ ký HMAC-SHA512, gọi API hoàn tiền thật |

## 10. So sánh & Reconciliation với project hiện tại (2026-07-29)

Phát hiện chính:
1. **`SettlementCreated` không tồn tại** — CLAUDE.md liệt kê sai, cần sửa tài liệu kiến trúc.
2. **Tên sự kiện tiêu thụ đã lỗi thời trong CLAUDE.md**: không còn subscribe trực tiếp `SalesOrderCreated`, đã chuyển sang lệnh saga từ 2026-07-14.
3. **Rủi ro cách ly dữ liệu**: `PaymentRepository` hoàn toàn không lọc `IsDeleted` ở bất kỳ method nào — nghiêm trọng hơn tình trạng đã thấy ở CRM (CRM ít nhất còn lọc thủ công từng method).
4. **Logic thiết kế nhưng chưa vận hành**: `InstallmentLine.MarkOverdue()` và `InstallmentPlan.IsFullyPaid()` tồn tại trong code nhưng không có job/handler nào gọi tới — cần một job quét định kỳ nếu muốn tính năng "quá hạn trả góp" hoạt động thật.
5. **Tích hợp gateway là thật**, không phải giả lập — `VnPayGateway` triển khai đầy đủ tạo URL/xác thực chữ ký/gọi API hoàn tiền thật với VNPay; có `NullPaymentGateway` dự phòng cho môi trường dev không có credential thật.
