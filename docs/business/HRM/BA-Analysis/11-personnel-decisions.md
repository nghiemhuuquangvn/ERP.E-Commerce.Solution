# 11 — Personnel Decisions (Quyết định nhân sự)

> **Bounded Context**: Personnel Decisions
> **Loại**: Supporting Domain — phát hành văn bản pháp lý nội bộ làm thay đổi trạng thái `Employee Profile` (01).
> **Phụ thuộc**: dùng chung **Approval Engine** (shared kernel, xem `05-request-approval.md`) để duyệt mọi loại quyết định nhân sự trước khi ban hành.
> **Quy ước đặt tên**: theo `00-domain-overview.md` mục 6 — bảng DB prefix `Dec_`, khoá chính `Id`, khoá ngoại `<Entity>Id`, soft-delete `IsDeleted`/`DeletedAtUtc`, audit `CreatedAtUtc/CreatedBy/LastModifiedAtUtc/LastModifiedBy` trên mọi bảng nghiệp vụ (không lặp lại trong từng bảng field bên dưới trừ khi cần nhấn mạnh).

---

## Phần 1 — DDD (Domain-Driven Design)

### 1.1 Bounded Context & Vai trò

Personnel Decisions mô hình hoá 3 nhóm **văn bản pháp lý nội bộ** (quyết định nhân sự) mà doanh nghiệp ban hành để chính thức hoá thay đổi trong quan hệ lao động với nhân viên:

1. **Appointment / Dismissal / Rank-transfer Decision** (Quyết định bổ nhiệm/miễn nhiệm/điều chuyển cấp bậc) — thay đổi chức danh/ngạch bậc.
2. **Transfer Decision** (Quyết định thuyên chuyển) — thay đổi phòng ban/chi nhánh/tổ nhóm.
3. **Resignation / Termination Decision** (Quyết định thôi việc/nghỉ việc) — chấm dứt quan hệ lao động, có điều khoản bàn giao trách nhiệm và xác nhận của HR/Kế toán.

Đặc điểm chung của cả 3 loại: có **số quyết định tự sinh**, có **ngày hiệu lực** (`EffectiveDate`), phải đi qua **Approval Engine dùng chung** trước khi được ban hành (`Issued`), và khi ban hành thì **bắt buộc phát sinh domain event làm thay đổi trạng thái `Employee Profile`** (01) — đây là ranh giới quan trọng nhất của bounded context này: Personnel Decisions **sở hữu** văn bản quyết định, nhưng **không sở hữu** dữ liệu Employee — nó chỉ phát sự kiện tích hợp yêu cầu Employee Profile cập nhật.

### 1.2 Ubiquitous Language (thuật ngữ riêng module)

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ | Giải thích |
|---|---|---|---|
| Quyết định nhân sự | Personnel Decision | `HRMF2510/2610/2700` | Văn bản pháp lý nội bộ làm thay đổi trạng thái nhân viên |
| Quyết định bổ nhiệm/miễn nhiệm/điều chuyển cấp bậc | Appointment / Dismissal / Rank-transfer Decision | `HRMF2510` | Thay đổi chức danh (`TitleId`)/vị trí (`DutyId`)/ngạch bậc |
| Quyết định thuyên chuyển | Transfer Decision | `HRMF2700` | Thay đổi phòng ban/tổ nhóm/chi nhánh |
| Quyết định thôi việc | Resignation / Termination Decision | `HRMF2610` | Chấm dứt quan hệ lao động |
| Điều khoản bàn giao | Handover Clause | `HRMF2610` (bàn giao trách nhiệm) | Danh mục công việc/tài sản/công nợ cần bàn giao khi nghỉ việc |
| Bộ hồ sơ nghỉ việc | Termination Document Package | `HRMF2610` report | Tập hợp văn bản in ấn khi chấm dứt hợp đồng |
| Ngày hiệu lực | Effective Date | — | Ngày quyết định bắt đầu có hiệu lực áp dụng |
| Ngày ban hành | Issue Date | — | Ngày ký/ban hành văn bản |
| Xác nhận của HR | HR Confirmation | — | HR xác nhận hoàn tất thủ tục hồ sơ |
| Xác nhận của Kế toán | Finance Confirmation / Finance Sign-off | — | Kế toán xác nhận hoàn tất công nợ/lương/BHXH liên quan |

### 1.3 Context Map cục bộ

```
 PersonnelDecision (Aggregate Root)
   ├─ AppointmentDetail (Entity, 1-1, khi DecisionType ∈ {Appointment, Dismissal, RankTransfer})
   ├─ TransferDetail     (Entity, 1-1, khi DecisionType = Transfer)
   └─ TerminationDetail  (Entity, 1-1, khi DecisionType ∈ {Resignation, Termination})
        └─ HandoverClauseItem (Entity con của TerminationDetail)

 PersonnelDecision ──references──► EmployeeId (Employee Profile, BC 01)
 PersonnelDecision ──references──► ApprovalId (Approval Engine, shared kernel, BC 05)
 PersonnelDecision ──self-reference──► SupersedesDecisionId (quyết định huỷ/thay thế quyết định trước)
```

> **Ghi chú thiết kế aggregate**: chọn **một Aggregate Root `PersonnelDecision`** dùng chung cho cả 3 loại quyết định (thay vì 3 aggregate tách biệt) vì cả 3 chia sẻ đúng một vòng đời nghiệp vụ giống hệt nhau: `Draft → PendingApproval → Approved/Rejected → Issued → (có thể) Cancelled`, cùng dùng chung cơ chế đánh số chứng từ và Approval Engine. Phần dữ liệu **khác biệt theo loại quyết định** được tách thành entity con 1-1 (`AppointmentDetail`/`TransferDetail`/`TerminationDetail`) theo mẫu thiết kế "table-per-subtype" — tránh một bảng có quá nhiều cột nullable, đồng thời giữ tính toàn vẹn giao dịch trong một aggregate duy nhất.

### 1.4 Aggregates, Entities, Value Objects

#### Aggregate: PersonnelDecision (Aggregate Root)

**PersonnelDecision**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| DecisionNumber | NVARCHAR(30) | ✔ | Số quyết định, tự sinh riêng theo từng `DecisionType` (mã cũ: số văn bản tự sinh trong `HRMF2510/2610/2700`) |
| DecisionType | ENUM | ✔ | `Appointment`, `Dismissal`, `RankTransfer`, `Transfer`, `Resignation`, `Termination` |
| EmployeeId | Id (FK → Employee Profile) | ✔ | Nhân viên bị/được áp dụng quyết định |
| IssueDate | DATE | ✔ | Ngày ban hành/ký văn bản |
| EffectiveDate | DATE | ✔ | Ngày quyết định có hiệu lực |
| ReasonCode | NVARCHAR(50) | ○ | Mã lý do (danh mục cấu hình) |
| ReasonText | NVARCHAR(1000) | ○ | Diễn giải lý do |
| Status | ENUM | ✔ | `Draft`, `PendingApproval`, `Approved`, `Rejected`, `Issued`, `Cancelled` |
| ApprovalId | Id (FK, nullable) | ○ | Tham chiếu Approval Aggregate dùng chung (xem `05-request-approval.md`) |
| IssuedByEmployeeId | Id (FK, nullable → Employee Profile) | ○ | Người ký ban hành |
| SupersedesDecisionId | Id (FK, nullable, self-reference) | ○ | Quyết định trước đó bị quyết định này huỷ/thay thế |

**AppointmentDetail** (Entity con — 1-1 khi `DecisionType ∈ {Appointment, Dismissal, RankTransfer}`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| PersonnelDecisionId | Id (FK, unique) | ✔ | Quyết định gốc |
| OldTitleId | Id (FK, nullable) | ○ | Chức danh cũ |
| NewTitleId | Id (FK, nullable) | ○ | Chức danh mới |
| OldDutyId | Id (FK, nullable) | ○ | Vị trí công việc cũ |
| NewDutyId | Id (FK, nullable) | ○ | Vị trí công việc mới |
| OldGradeId | Id (FK, nullable) | ○ | Ngạch/bậc cũ |
| NewGradeId | Id (FK, nullable) | ○ | Ngạch/bậc mới |

**TransferDetail** (Entity con — 1-1 khi `DecisionType = Transfer`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| PersonnelDecisionId | Id (FK, unique) | ✔ | Quyết định gốc |
| OldDepartmentId | Id (FK, nullable) | ○ | Phòng ban cũ |
| NewDepartmentId | Id (FK, nullable) | ○ | Phòng ban mới |
| OldTeamId | Id (FK, nullable) | ○ | Tổ/nhóm cũ |
| NewTeamId | Id (FK, nullable) | ○ | Tổ/nhóm mới |
| OldBranchId | Id (FK, nullable) | ○ | Chi nhánh cũ |
| NewBranchId | Id (FK, nullable) | ○ | Chi nhánh mới |
| TransferReason | NVARCHAR(500) | ○ | Lý do thuyên chuyển |

**TerminationDetail** (Entity con — 1-1 khi `DecisionType ∈ {Resignation, Termination}`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| PersonnelDecisionId | Id (FK, unique) | ✔ | Quyết định gốc |
| TerminationType | ENUM | ✔ | `Resignation` (tự nguyện), `Termination` (chấm dứt bởi công ty), `Discipline` (kỷ luật) |
| LastWorkingDate | DATE | ✔ | Ngày làm việc cuối cùng |
| HandoverToEmployeeId | Id (FK, nullable → Employee Profile) | ○ | Người tiếp nhận bàn giao chính |
| HrConfirmedBy | Id (FK, nullable → Employee Profile) | ○ | Người của HR xác nhận |
| HrConfirmedAtUtc | DATETIME2 | ○ | Thời điểm HR xác nhận |
| FinanceConfirmedBy | Id (FK, nullable → Employee Profile) | ○ | Người của Kế toán xác nhận |
| FinanceConfirmedAtUtc | DATETIME2 | ○ | Thời điểm Kế toán xác nhận |
| DocumentPackageStatus | ENUM | ✔ | `NotPrepared`, `Prepared`, `Handed` — trạng thái bộ hồ sơ nghỉ việc |

**HandoverClauseItem** (Entity con của `TerminationDetail`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| TerminationDetailId | Id (FK) | ✔ | Thuộc chi tiết quyết định thôi việc nào |
| ClauseDescription | NVARCHAR(1000) | ✔ | Nội dung công việc/tài sản/công nợ cần bàn giao |
| HandoverToEmployeeId | Id (FK, nullable → Employee Profile) | ○ | Người nhận bàn giao cho điều khoản này |
| Status | ENUM | ✔ | `Pending`, `Completed` |

**Value Objects**: `DecisionNumber` (định dạng số theo `DecisionType`, sinh bởi chính sách đánh số dùng chung, không model chi tiết); `EffectivePeriod {IssueDate, EffectiveDate}` dùng để kiểm tra BR-02.

### 1.5 Domain Events

| Domain Event | Phát sinh khi | Hệ quả / Consumer |
|---|---|---|
| `PersonnelDecisionDrafted` | Tạo mới quyết định (Status=Draft) | Khởi tạo bản nháp văn bản |
| `PersonnelDecisionSubmittedForApproval` | Gửi duyệt (Status→PendingApproval) | Khởi tạo luồng duyệt qua Approval Engine |
| `PersonnelDecisionApproved` / `PersonnelDecisionRejected` | Approval Engine trả kết quả duyệt | Cho phép/khoá việc ban hành (`Issue`) |
| `PersonnelDecisionIssued` | Quyết định được ban hành chính thức (Status→Issued) | Phát sinh đúng 1 trong các integration event bên dưới tuỳ `DecisionType` |
| `EmployeeTitleChanged` | `PersonnelDecisionIssued` với `DecisionType ∈ {Appointment, Dismissal, RankTransfer}` | Employee Profile (01) cập nhật `TitleId`/`DutyId`/`GradeId` từ `New*` sang hồ sơ nhân viên |
| `EmployeeDepartmentChanged` | `PersonnelDecisionIssued` với `DecisionType = Transfer` | Employee Profile (01) cập nhật `DepartmentId`/`TeamId`/`BranchId` |
| `EmployeeWorkingStatusChanged` | `PersonnelDecisionIssued` với `DecisionType ∈ {Resignation, Termination}` | Employee Profile (01) cập nhật `WorkingStatus = Terminated` và `LeaveDate = LastWorkingDate`; đồng thời phát tín hiệu để Labor Contract (03) đóng hợp đồng đang hiệu lực |
| `PersonnelDecisionCancelled` | Huỷ một quyết định đã Issued (tạo quyết định huỷ tham chiếu `SupersedesDecisionId`) | Nếu chưa có quyết định nào khác kế thừa, phát sinh event đảo ngược thay đổi tương ứng sang Employee Profile (01) |

### 1.6 Business Rules & Invariants

- **BR-01**: `DecisionNumber` phải **tự sinh và duy nhất theo từng `DecisionType`** (mỗi loại quyết định có dải số/prefix riêng, tương tự cơ chế đánh số chứng từ `HRMF0010` cũ) — không model chi tiết bảng cấu hình đánh số trong tài liệu này.
- **BR-02**: `EffectiveDate` không được nhỏ hơn `IssueDate`, trừ khi người dùng có quyền đặc biệt "cho phép hiệu lực hồi tố" (ghi log lý do khi vượt rule).
- **BR-03**: `PersonnelDecision` chỉ được chuyển `Status = Issued` sau khi đã đi hết các cấp duyệt của Approval Aggregate dùng chung (xem `05-request-approval.md`); chưa duyệt xong không được in ấn/ban hành văn bản chính thức.
- **BR-04**: Với `DecisionType ∈ {Resignation, Termination}`, bắt buộc phải có `HrConfirmedBy` **và** `FinanceConfirmedBy` (khác null) trước khi cho phép chuyển `Status = Issued`.
- **BR-05**: Với `DecisionType ∈ {Resignation, Termination}`, `TerminationDetail.LastWorkingDate` không được sau `EffectiveDate` của quyết định.
- **BR-06**: `PersonnelDecision` đã `Issued` thì **không được sửa/xoá trực tiếp** — chỉ có thể tạo một quyết định mới với `SupersedesDecisionId` trỏ về quyết định gốc để huỷ/thay thế.
- **BR-07**: Khi `Issued`, hệ thống phải phát sinh **đúng một** integration event tương ứng `DecisionType` để đồng bộ Employee Profile; không được `Issue` một `PersonnelDecision` nếu `Employee` đó đang có một `PersonnelDecision` khác **cùng `DecisionType`** ở trạng thái `PendingApproval` hoặc `Approved` (chưa Issue) có `EffectiveDate` trùng ngày — tránh xung đột 2 quyết định hiệu lực cùng lúc.
- **BR-08**: `TransferDetail` phải có ít nhất một cặp giá trị thực sự thay đổi (`NewDepartmentId ≠ OldDepartmentId` hoặc `NewTeamId ≠ OldTeamId` hoặc `NewBranchId ≠ OldBranchId`) — nếu không có gì thay đổi thì từ chối tạo quyết định thuyên chuyển.
- **BR-09**: `AppointmentDetail` phải có ít nhất một trong `NewTitleId ≠ OldTitleId`, `NewDutyId ≠ OldDutyId`, `NewGradeId ≠ OldGradeId` — nếu không có gì thay đổi thì từ chối tạo quyết định bổ nhiệm/điều chuyển cấp bậc.
- **BR-10**: Một `Employee` chỉ được có **tối đa một** `PersonnelDecision` cùng `DecisionType` ở trạng thái `PendingApproval` tại một thời điểm — tránh hai quyết định cùng loại chờ duyệt song song mâu thuẫn nhau.
- **BR-11**: Khi `Termination`/`Resignation` `Issued`, hệ thống bắt buộc cập nhật `Employee.WorkingStatus = Terminated` và `Employee.LeaveDate = LastWorkingDate` (qua domain event `EmployeeWorkingStatusChanged`) — không cho phép Issue mà không có `LastWorkingDate`.
- **BR-12**: Nếu công ty cấu hình bắt buộc hoàn tất bàn giao (`RequireHandoverCompletion = true`, tham số cấu hình), toàn bộ `HandoverClauseItem` của `TerminationDetail` phải ở trạng thái `Completed` trước khi cho phép `Issue` quyết định thôi việc.
- **BR-13**: Không cho phép `Issue` quyết định `Appointment`/`RankTransfer` nếu `NewTitleId`/`NewDutyId` không tồn tại/không còn hiệu lực trong danh mục hệ thống.
- **BR-14**: `PersonnelDecisionCancelled` chỉ được phép tạo cho một quyết định đã `Issued` và **chưa** bị một quyết định khác `SupersedesDecisionId` trỏ tới trước đó (không huỷ 2 lần cùng một quyết định gốc).

### 1.7 Đề xuất DB Schema

| Bảng | Ghi chú khoá/chỉ mục |
|---|---|
| `Dec_PersonnelDecision` | PK `Id`; UNIQUE INDEX `(DecisionType, DecisionNumber)`; FK `EmployeeId` → BC01 `Emp_Employee.Id`; FK `ApprovalWorkflowId` → shared kernel `App_ApprovalWorkflow.Id`; FK `SupersedesDecisionId` → self `Id` (nullable); INDEX `(EmployeeId, DecisionType, Status)` |
| `Dec_AppointmentDetail` | PK `Id`; UNIQUE FK `PersonnelDecisionId` → `Dec_PersonnelDecision.Id` |
| `Dec_TransferDetail` | PK `Id`; UNIQUE FK `PersonnelDecisionId` → `Dec_PersonnelDecision.Id` |
| `Dec_TerminationDetail` | PK `Id`; UNIQUE FK `PersonnelDecisionId` → `Dec_PersonnelDecision.Id` |
| `Dec_HandoverClauseItem` | PK `Id`; FK `TerminationDetailId` → `Dec_TerminationDetail.Id`; INDEX `(TerminationDetailId, Status)` |

Mọi bảng trên bổ sung cột chuẩn theo `00-domain-overview.md` mục 6: `IsDeleted BIT`, `DeletedAtUtc`, `CreatedAtUtc`, `CreatedBy`, `LastModifiedAtUtc`, `LastModifiedBy`.

### 1.8 Quan hệ với Bounded Context khác

- **Employee Profile (01)**: là **downstream trực tiếp** — mọi `PersonnelDecisionIssued` phát sinh đúng một integration event (`EmployeeTitleChanged`/`EmployeeDepartmentChanged`/`EmployeeWorkingStatusChanged`) làm thay đổi trạng thái `Employee` (chức danh, phòng ban, `WorkingStatus`, `LeaveDate`). Personnel Decisions không lưu bản sao dữ liệu Employee, chỉ tham chiếu `EmployeeId` và lưu giá trị `Old*`/`New*` tại thời điểm ra quyết định (snapshot phục vụ in ấn văn bản).
- **Labor Contract (03)**: khi `EmployeeWorkingStatusChanged` (từ quyết định thôi việc) được phát ra, Labor Contract (03) chịu trách nhiệm đóng/kết thúc hợp đồng lao động đang hiệu lực của nhân viên — Personnel Decisions chỉ phát tín hiệu, không trực tiếp sửa dữ liệu hợp đồng. Xem chi tiết vòng đời hợp đồng ở `03-labor-contract.md`.
- **Recruitment (02)**: một luồng khởi tạo `AppointmentDetail` (bổ nhiệm lần đầu) có thể phát sinh gián tiếp sau khi `Probation` (xem `02-recruitment.md`) đạt kết quả `Passed` và nhân viên được bổ nhiệm chính thức vào vị trí — đây là quan hệ nghiệp vụ tham khảo, không có ràng buộc kỹ thuật bắt buộc giữa hai bounded context.
- **Approval Engine (05, shared kernel)**: mọi `PersonnelDecision` áp dụng Approval Aggregate dùng chung qua `ApprovalId` — xem `05-request-approval.md` để biết cơ chế cấp duyệt, trạng thái, và bất biến liên quan. Tài liệu này không định nghĩa lại cơ chế duyệt.

---

## Phần 2 — BDD (Behavior-Driven Development)

### User Story 1 — Soạn thảo & đánh số Quyết định nhân sự

**Là** Chuyên viên Nhân sự, **tôi muốn** soạn thảo quyết định nhân sự với số văn bản tự sinh và ngày hiệu lực hợp lệ, **để** đảm bảo văn bản pháp lý nội bộ luôn đúng quy định.

```gherkin
Feature: Soạn thảo Quyết định nhân sự

  @BR-01
  Scenario: Số quyết định tự sinh duy nhất theo từng loại quyết định
    Given đã tồn tại quyết định "QD-BN-2026-015" thuộc loại "Appointment"
    When hệ thống tự sinh số quyết định mới cho một quyết định loại "Appointment"
    Then số quyết định mới phải khác "QD-BN-2026-015"
    And không trùng với bất kỳ số quyết định "Appointment" nào đã cấp trước đó

  @BR-02
  Scenario: Từ chối tạo quyết định có ngày hiệu lực trước ngày ban hành
    Given tôi soạn thảo một quyết định thuyên chuyển với IssueDate = 15/07/2026
    When tôi nhập EffectiveDate = 10/07/2026
    Then hệ thống từ chối lưu và báo lỗi "Ngày hiệu lực không được nhỏ hơn ngày ban hành"

  @BR-08
  Scenario: Từ chối tạo Quyết định thuyên chuyển không có gì thay đổi
    Given nhân viên "Nguyễn Văn A" hiện đang thuộc Phòng ban "Sản xuất", Tổ "Tổ 1"
    When tôi tạo quyết định thuyên chuyển với NewDepartmentId = Phòng ban "Sản xuất" và NewTeamId = Tổ "Tổ 1" (không đổi)
    Then hệ thống từ chối lưu và báo lỗi "Quyết định thuyên chuyển phải có ít nhất một thông tin thay đổi"

  @BR-09
  Scenario: Từ chối tạo Quyết định bổ nhiệm không có gì thay đổi
    Given nhân viên "Trần Thị B" hiện đang giữ chức danh "Nhân viên QA"
    When tôi tạo quyết định bổ nhiệm với NewTitleId = "Nhân viên QA" (không đổi) và không thay đổi DutyId/GradeId
    Then hệ thống từ chối lưu và báo lỗi "Quyết định bổ nhiệm phải có ít nhất một thông tin thay đổi"

  @BR-13
  Scenario: Từ chối ban hành Quyết định bổ nhiệm với chức danh không hợp lệ
    Given quyết định bổ nhiệm cho nhân viên "Trần Thị B" có NewTitleId trỏ tới một chức danh đã ngừng sử dụng
    When Trưởng phòng Nhân sự ban hành quyết định này
    Then hệ thống từ chối và báo lỗi "Chức danh mới không hợp lệ hoặc không còn hiệu lực"

  @BR-10
  Scenario: Từ chối tạo Quyết định cùng loại thứ 2 đang chờ duyệt cho cùng nhân viên
    Given nhân viên "Nguyễn Văn A" đang có quyết định thuyên chuyển "QD-TC-2026-008" ở trạng thái "PendingApproval"
    When tôi tạo thêm một quyết định thuyên chuyển khác cho nhân viên "Nguyễn Văn A"
    Then hệ thống từ chối và báo lỗi "Nhân viên đang có quyết định cùng loại chờ duyệt"
```

### User Story 2 — Duyệt & Ban hành Quyết định

**Là** Giám đốc Nhân sự, **tôi muốn** chỉ ban hành quyết định sau khi đã duyệt đủ cấp, **để** đảm bảo tính pháp lý của văn bản trước khi tác động tới hồ sơ nhân viên.

```gherkin
Feature: Duyệt và Ban hành Quyết định nhân sự

  @BR-03
  Scenario: Không cho phép ban hành khi chưa duyệt xong
    Given quyết định bổ nhiệm "QD-BN-2026-015" đang ở cấp duyệt 1/2 (Approval Aggregate, xem 05-request-approval.md)
    When Giám đốc Nhân sự cố gắng ban hành (Issue) quyết định này
    Then hệ thống từ chối và báo lỗi "Quyết định chưa được duyệt đủ các cấp yêu cầu"

  @BR-03
  @BR-07
  Scenario: Ban hành Quyết định bổ nhiệm thành công phát sinh sự kiện thay đổi chức danh
    Given quyết định bổ nhiệm "QD-BN-2026-015" đã được duyệt đủ các cấp (Status = "Approved")
    And AppointmentDetail có NewTitleId = "Trưởng nhóm QA", NewDutyId hợp lệ
    When Giám đốc Nhân sự ban hành quyết định
    Then Status của quyết định chuyển thành "Issued"
    And sự kiện EmployeeTitleChanged được phát sinh sang Employee Profile (01) với TitleId = "Trưởng nhóm QA"

  @BR-07
  Scenario: Ban hành Quyết định thuyên chuyển thành công phát sinh sự kiện thay đổi phòng ban
    Given quyết định thuyên chuyển "QD-TC-2026-008" đã được duyệt đủ các cấp
    And TransferDetail có NewDepartmentId = "Phòng Kỹ thuật"
    When Giám đốc Nhân sự ban hành quyết định
    Then sự kiện EmployeeDepartmentChanged được phát sinh sang Employee Profile (01) với DepartmentId = "Phòng Kỹ thuật"

  @BR-06
  Scenario: Không cho phép sửa trực tiếp Quyết định đã ban hành
    Given quyết định bổ nhiệm "QD-BN-2026-015" có Status = "Issued"
    When Chuyên viên Nhân sự cố gắng sửa trực tiếp NewTitleId trên quyết định này
    Then hệ thống từ chối và báo lỗi "Quyết định đã ban hành, không thể sửa trực tiếp — vui lòng tạo quyết định thay thế"
```

### User Story 3 — Quyết định thôi việc & bàn giao trách nhiệm

**Là** Chuyên viên Nhân sự, **tôi muốn** đảm bảo quyết định thôi việc chỉ ban hành khi HR và Kế toán đã xác nhận và bàn giao hoàn tất, **để** tránh phát sinh tranh chấp công nợ/tài sản sau khi nhân viên nghỉ việc.

```gherkin
Feature: Quyết định thôi việc

  @BR-04
  Scenario: Từ chối ban hành Quyết định thôi việc khi thiếu xác nhận HR hoặc Kế toán
    Given quyết định thôi việc "QD-TV-2026-003" đã được duyệt đủ các cấp
    And FinanceConfirmedBy vẫn còn trống (chưa xác nhận)
    When Chuyên viên Nhân sự ban hành quyết định
    Then hệ thống từ chối và báo lỗi "Quyết định thôi việc cần xác nhận của HR và Kế toán trước khi ban hành"

  @BR-05
  Scenario: Từ chối khi ngày làm việc cuối cùng sau ngày hiệu lực
    Given quyết định thôi việc có EffectiveDate = 31/07/2026
    When tôi nhập LastWorkingDate = 05/08/2026
    Then hệ thống từ chối lưu và báo lỗi "Ngày làm việc cuối cùng không được sau ngày hiệu lực quyết định"

  @BR-12
  Scenario: Từ chối ban hành khi còn điều khoản bàn giao chưa hoàn tất (cấu hình bắt buộc)
    Given công ty cấu hình RequireHandoverCompletion = true
    And quyết định thôi việc "QD-TV-2026-003" có 3 điều khoản bàn giao, trong đó 1 điều khoản còn Status = "Pending"
    And HrConfirmedBy và FinanceConfirmedBy đã có giá trị
    When Chuyên viên Nhân sự ban hành quyết định
    Then hệ thống từ chối và báo lỗi "Còn điều khoản bàn giao chưa hoàn tất"

  @BR-11
  @BR-07
  Scenario: Ban hành Quyết định thôi việc thành công cập nhật trạng thái làm việc của nhân viên
    Given quyết định thôi việc "QD-TV-2026-003" đã đủ điều kiện ban hành (đã duyệt, đã xác nhận HR/Kế toán, bàn giao hoàn tất)
    And LastWorkingDate = 31/07/2026
    When Chuyên viên Nhân sự ban hành quyết định
    Then Status của quyết định chuyển thành "Issued"
    And sự kiện EmployeeWorkingStatusChanged được phát sinh sang Employee Profile (01) với WorkingStatus = "Terminated" và LeaveDate = 31/07/2026
```

### User Story 4 — Huỷ/Thay thế Quyết định đã ban hành

**Là** Giám đốc Nhân sự, **tôi muốn** huỷ một quyết định đã ban hành bằng cách phát hành quyết định thay thế, **để** vẫn giữ được lịch sử pháp lý đầy đủ thay vì xoá dữ liệu.

```gherkin
Feature: Huỷ và Thay thế Quyết định nhân sự

  @BR-14
  Scenario: Tạo quyết định huỷ tham chiếu quyết định gốc thành công
    Given quyết định thuyên chuyển "QD-TC-2026-008" đã ở trạng thái "Issued" và chưa bị quyết định nào khác thay thế
    When Giám đốc Nhân sự tạo một quyết định mới với SupersedesDecisionId = "QD-TC-2026-008"
    Then quyết định mới được tạo thành công
    And sự kiện PersonnelDecisionCancelled được phát sinh cho quyết định gốc sau khi quyết định huỷ được Issued

  @BR-14
  Scenario: Từ chối huỷ một quyết định đã bị thay thế trước đó
    Given quyết định "QD-TC-2026-008" đã bị quyết định "QD-TC-2026-011" thay thế (SupersedesDecisionId trỏ tới nó)
    When Giám đốc Nhân sự cố gắng tạo thêm một quyết định huỷ khác cũng trỏ SupersedesDecisionId tới "QD-TC-2026-008"
    Then hệ thống từ chối và báo lỗi "Quyết định này đã bị thay thế trước đó"
```

---

## Phần 3 — TDD (Test-Driven Development)

### 3.1 Bảng Test Case

| TC | Scenario tham chiếu | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | BR-01 | Unit | Sinh `DecisionNumber` mới cho `DecisionType="Appointment"`, đã tồn tại "QD-BN-2026-015" | Số mới khác, không trùng trong cùng `DecisionType` |
| TC-02 | BR-02 | Unit | IssueDate=15/07/2026, EffectiveDate=10/07/2026 | Reject |
| TC-03 | BR-02 | Unit (boundary) | IssueDate=15/07/2026, EffectiveDate=15/07/2026 | Accept (bằng nhau hợp lệ) |
| TC-04 | BR-03 | Integration | `PersonnelDecision.Status="PendingApproval"`, cấp duyệt 1/2 | Reject Issue |
| TC-05 | BR-04 | Unit | `TerminationDetail.FinanceConfirmedBy=null`, các điều kiện khác đủ | Reject Issue |
| TC-06 | BR-05 | Unit | EffectiveDate=31/07/2026, LastWorkingDate=05/08/2026 | Reject |
| TC-07 | BR-05 | Unit (boundary) | EffectiveDate=31/07/2026, LastWorkingDate=31/07/2026 | Accept (bằng nhau hợp lệ) |
| TC-08 | BR-06 | Unit | `Status="Issued"`, cố sửa `NewTitleId` | Reject |
| TC-09 | BR-07 | Integration | `DecisionType="Appointment"`, Issue thành công | Đúng 1 event `EmployeeTitleChanged` phát sinh, không phát sinh event loại khác |
| TC-10 | BR-07 | Integration | `DecisionType="Termination"`, Issue thành công | Đúng 1 event `EmployeeWorkingStatusChanged` phát sinh |
| TC-11 | BR-08 | Unit | `NewDepartmentId=OldDepartmentId`, `NewTeamId=OldTeamId`, `NewBranchId=OldBranchId` | Reject tạo `TransferDetail` |
| TC-12 | BR-08 | Unit (boundary) | Chỉ `NewTeamId≠OldTeamId`, còn lại giữ nguyên | Accept |
| TC-13 | BR-09 | Unit | `NewTitleId=OldTitleId`, `NewDutyId=OldDutyId`, `NewGradeId=OldGradeId` | Reject tạo `AppointmentDetail` |
| TC-14 | BR-10 | Integration | Employee đã có 1 quyết định `Transfer` `PendingApproval`; tạo thêm 1 quyết định `Transfer` khác | Reject |
| TC-15 | BR-10 | Integration (boundary) | Employee đã có 1 quyết định `Transfer` `PendingApproval`; tạo thêm 1 quyết định `Appointment` | Accept (khác `DecisionType`) |
| TC-16 | BR-11 | Integration | Issue quyết định `Termination` với `LastWorkingDate` hợp lệ | `Employee.WorkingStatus="Terminated"`, `LeaveDate=LastWorkingDate` |
| TC-17 | BR-12 | Unit | `RequireHandoverCompletion=true`; 1/3 `HandoverClauseItem.Status="Pending"` | Reject Issue |
| TC-18 | BR-12 | Unit (boundary) | `RequireHandoverCompletion=true`; 3/3 `HandoverClauseItem.Status="Completed"` | Accept Issue (nếu điều kiện khác cũng đủ) |
| TC-19 | BR-13 | Unit | `NewTitleId` trỏ tới chức danh đã ngừng sử dụng | Reject Issue |
| TC-20 | BR-14 | Unit | Tạo quyết định huỷ với `SupersedesDecisionId` trỏ tới quyết định chưa bị thay thế | Accept |
| TC-21 | BR-14 | Unit | Tạo quyết định huỷ thứ 2 trỏ `SupersedesDecisionId` tới cùng 1 quyết định gốc đã bị thay thế | Reject |

### 3.2 Gợi ý chữ ký Domain Service / Repository / Validator

```csharp
namespace Hrm.PersonnelDecisions.Domain
{
    public interface IPersonnelDecisionRepository
    {
        Task<PersonnelDecision?> GetByIdAsync(Guid id);
        Task<bool> HasPendingDecisionOfSameTypeAsync(Guid employeeId, DecisionType decisionType, Guid? excludeDecisionId = null);
        Task<bool> IsAlreadySupersededAsync(Guid decisionId);
        Task SaveAsync(PersonnelDecision decision);
    }

    public class DecisionNumberGenerator
    {
        // BR-01
        public string GenerateNext(DecisionType decisionType);
    }

    public class PersonnelDecisionDateValidator
    {
        // BR-02
        public ValidationResult ValidateEffectiveDate(DateOnly issueDate, DateOnly effectiveDate, bool allowBackdatedOverride = false);

        // BR-05
        public ValidationResult ValidateLastWorkingDate(DateOnly effectiveDate, DateOnly lastWorkingDate);
    }

    public class AppointmentDetailValidator
    {
        // BR-09, BR-13
        public ValidationResult ValidateHasRealChange(AppointmentDetail detail);
        public ValidationResult ValidateNewTitleAndDutyActive(AppointmentDetail detail, ITitleDutyLookup lookup);
    }

    public class TransferDetailValidator
    {
        // BR-08
        public ValidationResult ValidateHasRealChange(TransferDetail detail);
    }

    public class TerminationIssuancePolicy
    {
        // BR-04
        public ValidationResult ValidateHrAndFinanceConfirmed(TerminationDetail detail);

        // BR-12
        public ValidationResult ValidateHandoverCompletion(TerminationDetail detail, bool requireHandoverCompletion);
    }

    public class PersonnelDecisionConcurrencyPolicy
    {
        // BR-10
        public Task<ValidationResult> ValidateNoConflictingPendingDecisionAsync(
            IPersonnelDecisionRepository repository, Guid employeeId, DecisionType decisionType, Guid? currentDecisionId);
    }

    public class PersonnelDecisionIssuanceService
    {
        // BR-03, BR-06, BR-07, BR-11
        public Result<IReadOnlyList<DomainEvent>> Issue(
            PersonnelDecision decision, IApprovalStatusProvider approvalStatusProvider);
    }

    public class PersonnelDecisionCancellationService
    {
        // BR-14
        public Result<PersonnelDecision> CreateSupersedingDecision(
            PersonnelDecision originalDecision, PersonnelDecisionDraft newDraft, IPersonnelDecisionRepository repository);
    }
}
```

**Edge case / boundary value cần bao phủ test**:
- `EffectiveDate = IssueDate` (bằng nhau, đạt) so với `EffectiveDate` sớm hơn 1 ngày (reject) — TC-02, TC-03.
- `LastWorkingDate = EffectiveDate` (bằng nhau, đạt) so với muộn hơn 1 ngày (reject) — TC-06, TC-07.
- Chỉ một trong ba cặp `Old/New` của `TransferDetail`/`AppointmentDetail` thay đổi (đạt) so với không cặp nào thay đổi (reject) — TC-11, TC-12, TC-13.
- `HandoverClauseItem` còn đúng 1/N ở trạng thái `Pending` (reject) so với toàn bộ N/N `Completed` (đạt) — TC-17, TC-18.
- Hai quyết định **khác** `DecisionType` cùng `PendingApproval` cho cùng nhân viên (đạt, không xung đột) so với cùng `DecisionType` (reject) — TC-14, TC-15.
- Quyết định huỷ lần đầu cho một quyết định gốc (đạt) so với huỷ lần thứ hai cho cùng quyết định gốc đã bị thay thế (reject) — TC-20, TC-21.
