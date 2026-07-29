# Module 10 — CRM (Customer Relationship Management)
## Technical Design Specification | ERP.E-Commerce.Solution

> Số module (10) được gán mới trong đợt rà soát 2026-07-29 — CRMService trước đó chưa có tài liệu BA riêng (chỉ có phần gap-analysis trong `ERP-Business-Analysis.md`). Tài liệu này viết theo hướng **reconciliation** hoàn toàn — mọi chi tiết bên dưới đã được đối chiếu trực tiếp với code thật (`src/services/CRMService`).

---

## 1. Mục tiêu và phạm vi

CRMService (Port 5006, Code: CRM, database `CrmDB`) quản lý toàn bộ vòng đời tương tác khách hàng: Contact (liên hệ) → Lead (khách hàng tiềm năng) → Opportunity (cơ hội bán hàng, pipeline) → Case (yêu cầu hỗ trợ/khiếu nại), cùng với Activity (lịch sử tương tác: gọi điện, email, họp, ghi chú, task) gắn với bất kỳ thực thể nào ở trên.

Phạm vi **không bao gồm**: xử lý đơn hàng thật (Sales sở hữu), thanh toán (Payment sở hữu) — CRM chỉ nhận event từ các module đó để tạo Activity ghi nhận, không lưu bản sao dữ liệu đơn hàng/thanh toán.

## 2. Actor tham gia

| Actor | Vai trò |
|---|---|
| Sales Rep | Tạo/chuyển đổi Lead, quản lý Opportunity qua từng Stage pipeline |
| Marketing | Tạo Lead từ chiến dịch, gắn Tag cho Contact để phân khúc |
| Customer Service | Mở/xử lý/đóng Case, ghi nhận Activity hỗ trợ khách |
| Sales Manager | Xem `GetPipelineSummaryQuery` (tổng hợp giá trị theo từng Stage) |
| Khách hàng (gián tiếp) | Không có giao diện trực tiếp — Contact được tạo tự động khi đăng ký tài khoản (event `UserRegisteredDomainEvent` từ Identity) |

## 3. Quy trình nghiệp vụ chính

1. **Contact**: tạo thủ công hoặc tự động khi có `UserRegisteredDomainEvent` từ IdentityService (qua `CreateContactCommand`, `ContactSource=UserRegistration`). **Không có cơ chế chống trùng lặp** theo Email/Phone — tạo Contact mới không kiểm tra đã tồn tại Contact nào cùng Email hay chưa (xác nhận: `IX_Contacts_Email` chỉ là index thường, không phải unique).
2. **Lead**: tạo ở trạng thái `New` → `Contact()` (chuyển `Contacted`, chỉ hợp lệ khi đang `New`) → `Qualify(opportunityId)`. **Điểm quan trọng**: "Qualify" một Lead **chính là hành động chuyển đổi (convert) — không có bước riêng biệt.** `QualifyLeadCommandHandler` tự động tạo một `Opportunity` mới (kế thừa `LeadName`, `ContactId`, `EstimatedValue`, hạn chốt mặc định = hôm nay + 3 tháng) rồi mới gọi `Lead.Qualify()` — người dùng không có quyền kiểm soát ngày chốt/probability của Opportunity sinh ra.
3. **Opportunity**: đi qua các Stage `Prospecting(10%) → Qualification(20%) → Proposal(40%) → Negotiation(60%)` bằng `AdvanceStage()` (mỗi lần gọi tăng đúng 1 bước, xác suất lấy theo bảng cố định `{10,20,40,60,100,0}`) — **không thể dùng `AdvanceStage()` để đạt `Won`**, bắt buộc phải gọi `MarkWon()`/`MarkLost(reason)` riêng (2 nhánh kết thúc, cho phép gọi từ bất kỳ stage nào chưa kết thúc, không bắt buộc phải qua `Negotiation` trước).
4. **Case**: mở với `CaseNumber` tự sinh (`CASE-{yyyyMMdd}-{8 ký tự})`), `Priority` do người mở tự chọn (không có công thức tính SLA tự động). `Assign(userId)` tự chuyển `Open→InProgress`. `Resolve()`→`Close()` là luồng chuẩn, nhưng **`Close()` có thể gọi trực tiếp từ `Open`/`InProgress`, bỏ qua bước `Resolved`** — không có ràng buộc bắt buộc phải Resolve trước khi Close.
5. **Activity**: tự động phân loại `Scheduled` (nếu `ScheduledAt` ở tương lai) hoặc `Completed` ngay lập tức (nếu không có hoặc đã qua) — không có trạng thái "Draft" trung gian.

## 4. Domain Model (Aggregate Roots — DDD, đã xác nhận đúng code thật)

```
Contact (Aggregate Root) — Name, Email, Phone, Company, ContactSource, UserId (nullable)
 └─ ContactTag (Entity riêng, không phải owned collection của Contact)

Lead (Aggregate Root)
 └─ LeadStatus: New → Contacted → Qualified | Lost
    (Lưu ý: SQL comment cũ nhắc tới Unqualified/Converted nhưng enum C# thật chỉ có 4 giá trị)

Opportunity (Aggregate Root)
 └─ OpportunityStage: Prospecting → Qualification → Proposal → Negotiation → Won | Lost

Case (Aggregate Root)
 └─ CaseStatus: Open → InProgress → Resolved → Closed (Close có thể bỏ qua Resolved)
    CasePriority: Low | Medium | High | Critical (do người mở chọn thủ công)

Activity (Aggregate Root) — ActivityType: Call|Email|Meeting|Note|Task
 └─ Status: Scheduled | Completed | Cancelled (tự suy ra khi tạo, không cần set thủ công)
```

## 5. Database Design (`CrmDB`, đã đối chiếu `src/Database/CRM/001_CreateCrmDB.sql` + `002_FixCrmEntityColumns.sql`)

| # | Bảng | Cột chính |
|---|---|---|
| 1 | `Contacts` | Id, TenantId, UserId, Name, Email, Phone, Company, ContactSource, IsDeleted |
| 2 | `ContactTags` | Id, ContactId (FK), Tag (lowercase/trim khi lưu) |
| 3 | `Leads` | Id, LeadName, LeadStatus, LeadSource, EstimatedValue, ContactId |
| 4 | `Opportunities` | Id, OpportunityName, ContactId, OpportunityStage, ProbabilityPercent, EstimatedValue, ExpectedCloseDate, AssignedToUserId, LostReason |
| 5 | `Cases` | Id, CaseNumber (unique), CustomerId, CaseSubject, CaseDescription, CasePriority, CaseStatus, ResolutionNote, AssignedToUserId, **SlaDeadline (cột chết — không code nào đọc/ghi)** |
| 6 | `Activities` | Id, ContactId, OpportunityId, CaseId, ActivityType, Subject, Notes, Status, ScheduledAt, CompletedAt, AssignedToUserId, Source |
| 7 | `OutboxMessages` | chuẩn Outbox pattern |

**Ghi chú migration quan trọng**: `001_CreateCrmDB.sql` ban đầu **không khớp** tên cột với entity C# thật (ví dụ Opportunities dùng `Name/Stage/Probability/Amount` trong SQL gốc nhưng entity dùng `OpportunityName/OpportunityStage/ProbabilityPercent/EstimatedValue`) — `002_FixCrmEntityColumns.sql` là migration khắc phục toàn bộ sai lệch này, tự ghi rõ trong header comment. Đây là bằng chứng nên luôn đối chiếu entity thật trước khi tin vào SQL gốc.

**Cảnh báo an toàn dữ liệu quan trọng** (không phải vấn đề B-Tree, nhưng nghiêm trọng hơn): **không có `HasQueryFilter` toàn cục nào cho `IsDeleted` hay `TenantId`** trong `CrmDbContext`. Soft-delete được áp thủ công theo từng method Repository (dễ sót khi thêm method mới); **hoàn toàn không có filter theo `TenantId` ở bất kỳ Repository nào** — mọi truy vấn hiện tại chạy xuyên suốt tất cả tenant. Đây là rủi ro cách ly dữ liệu đa tenant cần ưu tiên xử lý trước khi đưa vào production nhiều khách hàng.

## 6. API Design (REST) — tất cả dùng `[AuthorizePermission("...")]`, route `api/v1/crm/...`

```
GET/POST   /crm/contacts, /crm/contacts/{id}                    (crm:contacts:view / :manage)
POST       /crm/contacts/{id}/tags, DELETE .../tags/{tag}        (crm:contacts:manage)
GET        /crm/contacts/{id}/tags, /by-tag/{tag}, /tags/all      (crm:contacts:view)

GET/POST   /crm/leads, /crm/leads/{id}                            (crm:leads:view / :manage)
POST       /crm/leads/{id}/contact, /qualify, /lose               (crm:leads:manage)

GET/POST   /crm/opportunities, /{id}                              (crm:opportunities:view / :manage)
GET        /crm/opportunities/pipeline/summary                    (crm:opportunities:view)
POST       /crm/opportunities/{id}/advance, /win, /lose            (crm:opportunities:manage)

GET/POST   /crm/cases, /{id}                                      (crm:cases:view / :manage)
POST       /crm/cases/{id}/assign, /resolve, /close                (crm:cases:manage)

GET/POST   /crm/activities, /{id}                                 (crm:activities:view / :manage)
POST       /crm/activities/{id}/complete, /cancel
PATCH      /crm/activities/{id}/notes
```

## 7. Event Design

**Published** (qua Outbox → Kafka topic `crm-topic`):
- `LeadCreatedEvent`, `LeadQualifiedEvent(LeadId, OpportunityId)`, `LeadLostEvent`
- `OpportunityCreatedEvent` — **chỉ phát khi tạo qua `CreateOpportunityCommand` trực tiếp, KHÔNG phát khi Opportunity sinh ra từ Lead-qualification** — bất đối xứng cần lưu ý khi audit
- `OpportunityWonEvent(OpportunityId, Value)`, `OpportunityLostEvent`
- `CaseOpenedEvent`, `CaseResolvedEvent` (lưu ý: `Close()` không phát event nào)
- `ContactCreatedEvent`, `ActivityCreatedEvent`, `ActivityCompletedEvent`

**Consumed** (đã xác nhận đúng, sửa lại phần CLAUDE.md ghi sai):
| Nguồn | Transport | Sự kiện thật | Xử lý |
|---|---|---|---|
| Identity | Kafka `identity-topic` | `UserRegisteredDomainEvent` (không phải "UserCreated" như CLAUDE.md ghi) | Tạo Contact mới (`ContactSource=UserRegistration`), Email được dùng luôn làm Name tạm (Identity không có tên hiển thị trong event) |
| Payment | Kafka `payment-topic` | `PaymentProcessedEvent` (không phải "PaymentReceived") | **Chỉ log, không thao tác gì trên dữ liệu CRM** — vì CRM chưa có cách tra Contact/Opportunity theo OrderId |
| Sales | RabbitMQ `sales_order_events` | `sales_order.SalesOrderCreatedEvent` | Tìm Contact theo CustomerId → tạo Activity (loại Note) nếu tìm thấy; nếu không tìm thấy Contact thì chỉ log warning, không tạo gì |

## 8. Business Rules & Validation

- Lead **không có** hành động "convert" tách biệt — Qualify chính là convert, tự động tạo Opportunity không kiểm soát được ngày chốt/probability ban đầu.
- Không có cơ chế chống trùng Contact theo Email/Phone.
- Case Priority hoàn toàn thủ công — không có công thức SLA nào trong code dù cột `SlaDeadline` đã tồn tại sẵn trong DB.
- `Close()` một Case không bắt buộc phải qua `Resolved` trước.
- FluentValidation: `ExpectedCloseDate` của Opportunity bắt buộc phải ở **tương lai** tại thời điểm tạo.

## 9. Tích hợp với module khác

| Module | Loại tích hợp |
|---|---|
| Module 2 (Identity) | Nhận `UserRegisteredDomainEvent` → tự tạo Contact |
| Module 5 (Sales) | Nhận `SalesOrderCreatedEvent` → tạo Activity ghi nhận (chỉ khi đã có Contact khớp CustomerId) |
| Module 11 (Payment) | Nhận `PaymentProcessedEvent` — hiện tại chỉ log, chưa có tác động dữ liệu thật |

## 10. So sánh & Reconciliation với project hiện tại (2026-07-29)

Phát hiện chính cần lưu ý cho đội phát triển:
1. **Rủi ro cách ly dữ liệu đa tenant**: không Repository nào lọc theo `TenantId` — cần bổ sung trước khi vận hành đa khách hàng thật.
2. `OpportunityCreatedEvent` không nhất quán — thiếu khi tạo qua đường Lead-qualification.
3. Cột `SlaDeadline` tồn tại trong DB nhưng chết hoàn toàn trong code — cần quyết định implement thật hoặc bỏ cột.
4. Tên sự kiện tiêu thụ trong CLAUDE.md không khớp code thật (`UserCreated`→`UserRegisteredDomainEvent`, `PaymentReceived`→`PaymentProcessedEvent`) — đã sửa lại đúng ở §7.
