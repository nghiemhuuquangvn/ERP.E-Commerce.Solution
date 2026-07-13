# 09 — Training (Đào tạo)

> Bounded Context: **Training**. Tài liệu này mô hình hoá nghiệp vụ đào tạo nhân sự: đề xuất/kế hoạch đào tạo theo lĩnh vực hoặc phòng ban, lịch đào tạo/khoá học cụ thể, đăng ký người tham gia (nhân viên hiện hữu hoặc ứng viên mới trúng tuyển — đào tạo hội nhập), và đánh giá kết quả sau đào tạo. Toàn bộ quy trình duyệt (kế hoạch và kết quả) sử dụng lại **Approval Engine** dùng chung — xem chi tiết mô hình hoá tại `05-request-approval.md`, tài liệu này chỉ tham chiếu.
>
> **Ghi chú khảo sát**: Dữ liệu khảo sát được từ code cũ cho module này khá mỏng (HRMF2080, HRMF2120/2130/2131, business layer HRMF2070BL–HRMF2130BL). Phần mô hình bên dưới được suy luận hợp lý dựa trên nghiệp vụ đào tạo nhân sự chuẩn trong ERP, giữ nhất quán với các field/khoá đã khảo sát được (`TrainingFieldID`, `TrainingCostID`, `TrainingScheduleID`, `TrainingType`, `CostAmount`, `AssignedToUserID`, `TrainingResultID`) và với liên kết đã xác nhận sang Recruitment (`HRMF2054.CandidateID ↔ TrainingResultID`).

## Phần 1 — DDD

### 1.1 Ubiquitous Language

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ (tham khảo) | Giải thích |
|---|---|---|---|
| Kế hoạch đào tạo | Training Plan | `HRMF2080` | Đề xuất nhu cầu đào tạo theo lĩnh vực chuyên môn và/hoặc theo phòng ban, là gốc của toàn bộ vòng đời đào tạo |
| Lĩnh vực đào tạo | Training Field | `TrainingFieldID` | Phân loại chuyên môn của nội dung đào tạo (kỹ thuật, quản lý, an toàn lao động...) |
| Loại chi phí đào tạo | Training Cost Type | `TrainingCostID` | Phân loại nguồn/tính chất chi phí (nội bộ, thuê ngoài, có phí/miễn phí...) |
| Lịch đào tạo / Khoá học | Training Course | `HRMF2120`, `HRMF2130`, `HRMF2131` | Một khoá học/buổi đào tạo cụ thể (thời gian, địa điểm, giảng viên) nằm trong 1 kế hoạch |
| Người phụ trách đào tạo | Assigned Trainer/Coordinator | `AssignedToUserID` | Nhân viên được giao phụ trách tổ chức/theo dõi khoá học |
| Người tham gia đào tạo | Training Participant | *(suy luận)* | Nhân viên hiện hữu hoặc ứng viên mới đăng ký/được cử tham gia 1 khoá học |
| Đào tạo hội nhập | Onboarding Training | *(suy luận, liên kết `HRMF2054`)* | Đào tạo dành cho ứng viên mới trúng tuyển, thường là điều kiện để hoàn tất thử việc |
| Kết quả đào tạo | Training Result | `HRMF2120Entity`, `DataPrintTrainingResult`, `TrainingResultID` | Đánh giá đạt/không đạt của 1 người tham gia sau khi kết thúc khoá học |
| Báo cáo Đào tạo HCNS | HR Training Report | `HRMF2120DataPrint` | Báo cáo in tổng hợp đào tạo do phòng Hành chính Nhân sự quản lý |

### 1.2 Aggregate / Entity / Value Object

**Ghi chú thiết kế**: `TrainingPlan` là Aggregate Root, tương ứng khái niệm "Kế hoạch đào tạo" (`HRMF2080`) hợp nhất với "Lịch đào tạo" (`HRMF2120/2130`, mã cũ gọi là Training Schedule) — trong hệ thống mới không tách 2 khái niệm này thành 2 aggregate riêng vì chúng cùng vòng đời và cùng đi qua Approval Engine 1 lần cho phần kế hoạch. Bên trong 1 `TrainingPlan` có thể có nhiều `TrainingCourse` (khoá học cụ thể); mỗi `TrainingCourse` có nhiều `TrainingParticipant` (người đăng ký); mỗi `TrainingParticipant` sau khi khoá học kết thúc có thể có 1 `TrainingResult` (đánh giá kết quả), và bản thân `TrainingResult` đi qua 1 vòng duyệt Approval Engine riêng (duyệt kết quả đào tạo) độc lập với vòng duyệt kế hoạch.

#### Aggregate Root: `TrainingPlan` (mã cũ: `HRMF2080` + `HRMF2120`/`HRMF2130`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| PlanCode | string(30) | Có | Mã kế hoạch đào tạo, unique |
| PlanName | string(255) | Có | Tên kế hoạch/đợt đào tạo |
| TrainingFieldId | Guid (FK `Trn_TrainingField`) | Có | Lĩnh vực đào tạo (`TrainingFieldID`) |
| DepartmentId | Guid (FK `Emp_Department`, module 01) | Không | Phòng ban đề xuất/thụ hưởng đào tạo — có thể null nếu đào tạo áp dụng toàn công ty theo lĩnh vực (xem BR-01) |
| TrainingCostTypeId | Guid (FK `Trn_TrainingCostType`) | Có | Loại chi phí đào tạo (`TrainingCostID`) |
| TrainingType | enum (`Internal`, `External`, `Onboarding`, `Online`) | Có | Hình thức đào tạo |
| PlanYear | int | Có | Năm kế hoạch |
| ProposedByEmployeeId | Guid (FK `Emp_Employee`) | Có | Người đề xuất kế hoạch |
| AssignedToEmployeeId | Guid (FK `Emp_Employee`) | Không | Người phụ trách chung tổ chức đào tạo (`AssignedToUserID`) |
| EstimatedCostAmount | decimal(18,2) | Có | Tổng chi phí dự kiến (`CostAmount`), mặc định 0 |
| Status | enum (`Draft`, `PendingApproval`, `Approved`, `Rejected`, `InProgress`, `Completed`, `Cancelled`) | Có | Trạng thái kế hoạch |
| ApprovalWorkflowId | Guid (FK shared kernel `App_ApprovalWorkflow`) | Không | Tham chiếu hồ sơ duyệt kế hoạch — xem `05-request-approval.md` |
| Description | string(2000) | Không | Mô tả/mục tiêu đào tạo |
| IsDeleted | bool | Có | Soft delete |
| DeletedAtUtc | DateTime? | Không | Thời điểm xoá mềm |
| CreatedAtUtc / CreatedBy | DateTime / Guid | Có | Audit |
| LastModifiedAtUtc / LastModifiedBy | DateTime? / Guid? | Không | Audit |

#### Entity con: `TrainingCourse` (mã cũ: `HRMF2130`/`HRMF2131` — khoá học cụ thể trong 1 kế hoạch)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| TrainingPlanId | Guid (FK `Trn_TrainingPlan`) | Có | Kế hoạch chứa khoá học này |
| CourseCode | string(30) | Có | Mã khoá học |
| CourseName | string(255) | Có | Tên khoá học |
| StartDate | DateTime | Có | Ngày bắt đầu |
| EndDate | DateTime | Có | Ngày kết thúc (`EndDate >= StartDate`) |
| Location | string(255) | Không | Địa điểm tổ chức |
| TrainerName | string(255) | Không | Giảng viên/đơn vị đào tạo |
| MaxParticipants | int? | Không | Sức chứa tối đa (BR-07) |
| CourseCostAmount | decimal(18,2) | Có | Chi phí thực tế của khoá học, mặc định 0 (BR-05) |
| Status | enum (`Planned`, `Ongoing`, `Completed`, `Cancelled`) | Có | Trạng thái khoá học |
| IsDeleted / DeletedAtUtc | bool / DateTime? | Có/Không | Soft delete |
| CreatedAtUtc / CreatedBy / LastModifiedAtUtc / LastModifiedBy | — | Có/Không | Audit |

#### Entity con: `TrainingParticipant` (danh sách người tham gia của 1 khoá học)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| TrainingCourseId | Guid (FK `Trn_TrainingCourse`) | Có | Khoá học đăng ký |
| ParticipantType | enum (`Employee`, `Candidate`) | Có | Loại người tham gia (BR-03) |
| EmployeeId | Guid? (FK `Emp_Employee`, module 01) | Điều kiện | Bắt buộc khi `ParticipantType = Employee`, ngược lại null |
| CandidateId | Guid? (FK `Rec_Candidate`, module 02) | Điều kiện | Bắt buộc khi `ParticipantType = Candidate`, ngược lại null |
| RegisteredAtUtc | DateTime | Có | Thời điểm đăng ký/được cử đi học |
| AttendanceStatus | enum (`Registered`, `Attended`, `Absent`, `Cancelled`) | Có | Trạng thái tham dự thực tế |
| IsDeleted / DeletedAtUtc | bool / DateTime? | Có/Không | Soft delete |
| CreatedAtUtc / CreatedBy / LastModifiedAtUtc / LastModifiedBy | — | Có/Không | Audit |

#### Entity: `TrainingResult` (mã cũ: `HRMF2120Entity`, `DataPrintTrainingResult`, khoá `TrainingResultID`/`TrainingScheduleID`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính (`TrainingResultID`) |
| TrainingCourseId | Guid (FK `Trn_TrainingCourse`) | Có | Khoá học được đánh giá (`TrainingScheduleID`) |
| TrainingParticipantId | Guid (FK `Trn_TrainingParticipant`) | Có | Người tham gia được đánh giá |
| Score | decimal(5,2)? | Không | Điểm đánh giá (nếu khoá học có chấm điểm) |
| ResultStatus | enum (`Pending`, `Pass`, `Fail`) | Có | Kết quả đạt/không đạt |
| IsOnboardingCondition | bool | Có | Đánh dấu kết quả này là điều kiện bắt buộc cho quy trình thử việc/tuyển dụng của ứng viên liên quan (BR-04), mặc định `false`, chỉ `true` khi `ParticipantType = Candidate` và khoá học thuộc loại `Onboarding` |
| Comment | string(1000) | Không | Nhận xét của người đánh giá |
| EvaluatedByEmployeeId | Guid (FK `Emp_Employee`) | Có | Người đánh giá |
| EvaluatedAtUtc | DateTime | Có | Thời điểm đánh giá |
| Status | enum (`Draft`, `PendingApproval`, `Approved`, `Rejected`) | Có | Trạng thái duyệt kết quả (vòng duyệt riêng, xem BR-02) |
| ApprovalWorkflowId | Guid? (FK shared kernel `App_ApprovalWorkflow`) | Không | Hồ sơ duyệt kết quả đào tạo |
| IsDeleted / DeletedAtUtc | bool / DateTime? | Có/Không | Soft delete |
| CreatedAtUtc / CreatedBy / LastModifiedAtUtc / LastModifiedBy | — | Có/Không | Audit |

#### Value Object / Lookup: `TrainingField`, `TrainingCostType`

Hai bảng danh mục đơn giản (`Id`, `Code`, `Name`, `IsActive`, audit, soft delete) — dùng làm tham chiếu lọc/phân loại cho `TrainingPlan`. Không xoá cứng nếu đang bị tham chiếu (BR-10).

### 1.3 Domain Events

- `TrainingPlanCreated`
- `TrainingPlanSubmittedForApproval`
- `TrainingPlanApproved` / `TrainingPlanRejected` (phát sinh từ Approval Engine, xem 05)
- `TrainingCourseAdded`
- `ParticipantEnrolled`
- `ParticipantAttendanceRecorded`
- `TrainingResultRecorded`
- `TrainingResultSubmittedForApproval`
- `TrainingResultApproved` / `TrainingResultRejected`
- `OnboardingTrainingRequirementSatisfied` (kết quả `Pass` cho khoá học `Onboarding` gắn với 1 `CandidateId` — sự kiện được Recruitment/Personnel Decisions lắng nghe để mở khoá đánh giá thử việc)
- `TrainingPlanCancelled`

### 1.4 Business Rules & Invariants

- **BR-01**: Một `TrainingPlan` phải xác định ít nhất một trong hai: `TrainingFieldId` (theo lĩnh vực) hoặc `DepartmentId` (theo phòng ban) — không được để cả hai đều rỗng ý nghĩa (thực tế `TrainingFieldId` luôn bắt buộc, `DepartmentId` là điều kiện lọc thêm tuỳ chọn).
- **BR-02**: `TrainingPlan` phải qua Approval Engine (chuyển `Draft → PendingApproval → Approved`) trước khi được phép chuyển sang `InProgress`; tương tự `TrainingResult` phải qua một vòng duyệt Approval Engine riêng (`Draft → PendingApproval → Approved`) trước khi được xem là kết quả chính thức. Quy tắc sửa/xoá khi đã có cấp duyệt tuân theo shared kernel (xem `05-request-approval.md`).
- **BR-03**: Mỗi `TrainingParticipant` phải thuộc đúng một trong hai loại — hoặc là `Employee` hiện hữu (`EmployeeId` bắt buộc, `CandidateId` phải null), hoặc là `Candidate` mới (`CandidateId` bắt buộc, `EmployeeId` phải null). Ràng buộc XOR này thực thi bằng `CHECK constraint` ở DB và validator ở domain layer.
- **BR-04**: Khi `TrainingParticipant.ParticipantType = Candidate` và khoá học có `TrainingType = Onboarding`, `TrainingResult.IsOnboardingCondition` phải là `true`; quy trình đánh giá thử việc (Probation — module 02/11) **không được phép** chuyển trạng thái "Đạt" cho ứng viên/nhân viên tương ứng nếu tồn tại một `TrainingResult` với `IsOnboardingCondition = true` mà `ResultStatus` khác `Pass` hoặc `Status` khác `Approved`.
- **BR-05**: `TrainingCourse.CourseCostAmount` và `TrainingPlan.EstimatedCostAmount` phải >= 0.
- **BR-06**: `TrainingResult` chỉ được phép tạo/ghi nhận khi `TrainingCourse.EndDate` đã qua (`EndDate <= ngày hiện tại`) và `TrainingParticipant.AttendanceStatus = Attended`. Không ghi nhận kết quả cho người `Absent` hoặc `Cancelled`.
- **BR-07**: Số lượng `TrainingParticipant` có `AttendanceStatus` khác `Cancelled` trong 1 `TrainingCourse` không được vượt quá `MaxParticipants` (nếu có thiết lập).
- **BR-08**: Không được xoá `TrainingPlan` nếu tồn tại ít nhất 1 `TrainingCourse` đã `Approved`/`Completed` hoặc đã có `TrainingResult` được ghi nhận — chỉ cho phép `Cancel` (đổi trạng thái), không xoá cứng.
- **BR-09**: Nếu `ParticipantType = Employee`, `EmployeeId` phải tham chiếu một nhân viên đang có `WorkingStatus = Active` tại thời điểm `RegisteredAtUtc` (nhân viên đã nghỉ việc không được đăng ký đào tạo mới).
- **BR-10**: `TrainingField` và `TrainingCostType` là bảng danh mục dùng chung — không được xoá (kể cả xoá mềm) nếu đang có `TrainingPlan`/`TrainingCourse` tham chiếu còn hiệu lực.

### 1.5 Đề xuất DB Schema (prefix `Trn_`)

```sql
-- Danh mục
Trn_TrainingField (
  Id UNIQUEIDENTIFIER PK,
  Code NVARCHAR(30) NOT NULL UNIQUE,
  Name NVARCHAR(255) NOT NULL,
  IsActive BIT NOT NULL DEFAULT 1,
  IsDeleted BIT NOT NULL DEFAULT 0, DeletedAtUtc DATETIME2 NULL,
  CreatedAtUtc DATETIME2 NOT NULL, CreatedBy UNIQUEIDENTIFIER NOT NULL,
  LastModifiedAtUtc DATETIME2 NULL, LastModifiedBy UNIQUEIDENTIFIER NULL
)

Trn_TrainingCostType (
  Id UNIQUEIDENTIFIER PK,
  Code NVARCHAR(30) NOT NULL UNIQUE,
  Name NVARCHAR(255) NOT NULL,
  IsActive BIT NOT NULL DEFAULT 1,
  IsDeleted BIT NOT NULL DEFAULT 0, DeletedAtUtc DATETIME2 NULL,
  CreatedAtUtc DATETIME2 NOT NULL, CreatedBy UNIQUEIDENTIFIER NOT NULL,
  LastModifiedAtUtc DATETIME2 NULL, LastModifiedBy UNIQUEIDENTIFIER NULL
)

-- Aggregate root
Trn_TrainingPlan (
  Id UNIQUEIDENTIFIER PK,
  PlanCode NVARCHAR(30) NOT NULL UNIQUE,
  PlanName NVARCHAR(255) NOT NULL,
  TrainingFieldId UNIQUEIDENTIFIER NOT NULL FK -> Trn_TrainingField(Id),
  DepartmentId UNIQUEIDENTIFIER NULL FK -> Emp_Department(Id),
  TrainingCostTypeId UNIQUEIDENTIFIER NOT NULL FK -> Trn_TrainingCostType(Id),
  TrainingType NVARCHAR(20) NOT NULL, -- enum
  PlanYear INT NOT NULL,
  ProposedByEmployeeId UNIQUEIDENTIFIER NOT NULL FK -> Emp_Employee(Id),
  AssignedToEmployeeId UNIQUEIDENTIFIER NULL FK -> Emp_Employee(Id),
  EstimatedCostAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
  Status NVARCHAR(20) NOT NULL, -- enum
  ApprovalWorkflowId UNIQUEIDENTIFIER NULL FK -> App_ApprovalWorkflow(Id),
  Description NVARCHAR(2000) NULL,
  IsDeleted BIT NOT NULL DEFAULT 0, DeletedAtUtc DATETIME2 NULL,
  CreatedAtUtc DATETIME2 NOT NULL, CreatedBy UNIQUEIDENTIFIER NOT NULL,
  LastModifiedAtUtc DATETIME2 NULL, LastModifiedBy UNIQUEIDENTIFIER NULL,
  INDEX IX_TrainingPlan_Year_Department (PlanYear, DepartmentId),
  INDEX IX_TrainingPlan_Status (Status)
)

-- Entity con
Trn_TrainingCourse (
  Id UNIQUEIDENTIFIER PK,
  TrainingPlanId UNIQUEIDENTIFIER NOT NULL FK -> Trn_TrainingPlan(Id),
  CourseCode NVARCHAR(30) NOT NULL,
  CourseName NVARCHAR(255) NOT NULL,
  StartDate DATETIME2 NOT NULL,
  EndDate DATETIME2 NOT NULL,
  Location NVARCHAR(255) NULL,
  TrainerName NVARCHAR(255) NULL,
  MaxParticipants INT NULL,
  CourseCostAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
  Status NVARCHAR(20) NOT NULL, -- enum
  IsDeleted BIT NOT NULL DEFAULT 0, DeletedAtUtc DATETIME2 NULL,
  CreatedAtUtc DATETIME2 NOT NULL, CreatedBy UNIQUEIDENTIFIER NOT NULL,
  LastModifiedAtUtc DATETIME2 NULL, LastModifiedBy UNIQUEIDENTIFIER NULL,
  INDEX IX_TrainingCourse_Plan (TrainingPlanId),
  INDEX IX_TrainingCourse_Dates (StartDate, EndDate)
)

Trn_TrainingParticipant (
  Id UNIQUEIDENTIFIER PK,
  TrainingCourseId UNIQUEIDENTIFIER NOT NULL FK -> Trn_TrainingCourse(Id),
  ParticipantType NVARCHAR(20) NOT NULL, -- enum Employee|Candidate
  EmployeeId UNIQUEIDENTIFIER NULL FK -> Emp_Employee(Id),
  CandidateId UNIQUEIDENTIFIER NULL FK -> Rec_Candidate(Id),
  RegisteredAtUtc DATETIME2 NOT NULL,
  AttendanceStatus NVARCHAR(20) NOT NULL, -- enum
  IsDeleted BIT NOT NULL DEFAULT 0, DeletedAtUtc DATETIME2 NULL,
  CreatedAtUtc DATETIME2 NOT NULL, CreatedBy UNIQUEIDENTIFIER NOT NULL,
  LastModifiedAtUtc DATETIME2 NULL, LastModifiedBy UNIQUEIDENTIFIER NULL,
  CONSTRAINT CK_TrainingParticipant_XOR CHECK (
    (ParticipantType = 'Employee' AND EmployeeId IS NOT NULL AND CandidateId IS NULL) OR
    (ParticipantType = 'Candidate' AND CandidateId IS NOT NULL AND EmployeeId IS NULL)
  ),
  INDEX IX_TrainingParticipant_Course (TrainingCourseId),
  UNIQUE INDEX UX_TrainingParticipant_Course_Employee (TrainingCourseId, EmployeeId) WHERE EmployeeId IS NOT NULL,
  UNIQUE INDEX UX_TrainingParticipant_Course_Candidate (TrainingCourseId, CandidateId) WHERE CandidateId IS NOT NULL
)

Trn_TrainingResult (
  Id UNIQUEIDENTIFIER PK,
  TrainingCourseId UNIQUEIDENTIFIER NOT NULL FK -> Trn_TrainingCourse(Id),
  TrainingParticipantId UNIQUEIDENTIFIER NOT NULL FK -> Trn_TrainingParticipant(Id),
  Score DECIMAL(5,2) NULL,
  ResultStatus NVARCHAR(20) NOT NULL, -- enum
  IsOnboardingCondition BIT NOT NULL DEFAULT 0,
  Comment NVARCHAR(1000) NULL,
  EvaluatedByEmployeeId UNIQUEIDENTIFIER NOT NULL FK -> Emp_Employee(Id),
  EvaluatedAtUtc DATETIME2 NOT NULL,
  Status NVARCHAR(20) NOT NULL, -- enum
  ApprovalWorkflowId UNIQUEIDENTIFIER NULL FK -> App_ApprovalWorkflow(Id),
  IsDeleted BIT NOT NULL DEFAULT 0, DeletedAtUtc DATETIME2 NULL,
  CreatedAtUtc DATETIME2 NOT NULL, CreatedBy UNIQUEIDENTIFIER NOT NULL,
  LastModifiedAtUtc DATETIME2 NULL, LastModifiedBy UNIQUEIDENTIFIER NULL,
  INDEX IX_TrainingResult_Participant (TrainingParticipantId),
  INDEX IX_TrainingResult_Status (ResultStatus, Status)
)
```

### 1.6 Quan hệ với Bounded Context khác

- **Employee Profile (01)**: `TrainingParticipant.EmployeeId`, `TrainingPlan.ProposedByEmployeeId`/`AssignedToEmployeeId`, `TrainingResult.EvaluatedByEmployeeId` đều tham chiếu `Emp_Employee`; `TrainingPlan.DepartmentId` tham chiếu `Emp_Department`.
- **Recruitment (02)**: `TrainingParticipant.CandidateId` tham chiếu `Rec_Candidate`; sự kiện `OnboardingTrainingRequirementSatisfied` được Recruitment/Probation lắng nghe (tương ứng liên kết cũ `HRMF2054.CandidateID ↔ TrainingResultID`).
- **Personnel Decisions (11) / Probation**: quy trình đánh giá thử việc kiểm tra `TrainingResult.IsOnboardingCondition` trước khi cho phép chuyển trạng thái thử việc "Đạt" (BR-04).
- **Request & Approval (05) — Shared Kernel Approval Engine**: `TrainingPlan.ApprovalWorkflowId` và `TrainingResult.ApprovalWorkflowId` tham chiếu `App_ApprovalWorkflow`.
- **Payroll (06)**: chi phí đào tạo (`EstimatedCostAmount`/`CourseCostAmount`) có thể là input cho phân bổ chi phí đào tạo/phúc lợi (ngoài phạm vi tính lương trực tiếp, chỉ ghi nhận tham chiếu).

## Phần 2 — BDD

### User Stories

- **US-01**: Là Trưởng phòng/Quản lý đào tạo, tôi muốn đề xuất một `TrainingPlan` theo lĩnh vực và/hoặc phòng ban để đáp ứng nhu cầu nâng cao năng lực nhân viên.
- **US-02**: Là Nhân sự phụ trách đào tạo, tôi muốn thêm các `TrainingCourse` cụ thể vào một kế hoạch đã được duyệt.
- **US-03**: Là Người phụ trách đào tạo, tôi muốn đăng ký người tham gia (nhân viên hiện hữu hoặc ứng viên mới) vào một khoá học.
- **US-04**: Là Người đánh giá, tôi muốn ghi nhận kết quả đào tạo sau khi khoá học kết thúc và trình duyệt kết quả.
- **US-05**: Là Hệ thống quản lý thử việc, tôi muốn kiểm tra kết quả đào tạo hội nhập trước khi cho phép đánh giá thử việc "Đạt".

```gherkin
Feature: Đề xuất và duyệt kế hoạch đào tạo

  Scenario: Đề xuất kế hoạch đào tạo theo lĩnh vực và phòng ban (BR-01)
    Given tôi là Trưởng phòng "Sản xuất"
    When tôi tạo một TrainingPlan mới với TrainingFieldId là "Kỹ năng vận hành máy" và DepartmentId là "Phòng Sản xuất"
    Then hệ thống tạo TrainingPlan ở trạng thái "Draft"
    And sự kiện TrainingPlanCreated được phát sinh

  Scenario: Không thể tạo kế hoạch đào tạo thiếu lĩnh vực đào tạo (BR-01)
    Given tôi đang tạo một TrainingPlan mới
    When tôi để trống TrainingFieldId
    Then hệ thống từ chối tạo và báo lỗi "TrainingFieldId là bắt buộc"

  Scenario: Kế hoạch đào tạo phải được duyệt trước khi triển khai (BR-02)
    Given TrainingPlan "TP-2026-001" đang ở trạng thái "PendingApproval"
    When cấp duyệt cuối cùng trong Approval Engine phê duyệt
    Then TrainingPlan chuyển sang trạng thái "Approved"
    And TrainingPlan có thể chuyển sang "InProgress"

  Scenario: Không thể chuyển kế hoạch sang triển khai khi chưa được duyệt (BR-02)
    Given TrainingPlan "TP-2026-002" đang ở trạng thái "Draft"
    When tôi cố gắng chuyển trạng thái sang "InProgress"
    Then hệ thống từ chối và báo lỗi "Kế hoạch chưa được duyệt"

Feature: Quản lý khoá học và đăng ký người tham gia

  Scenario: Thêm khoá học vào kế hoạch đã duyệt
    Given TrainingPlan "TP-2026-001" đang ở trạng thái "Approved"
    When tôi thêm một TrainingCourse "Kỹ năng vận hành máy CNC" từ 10/08/2026 đến 12/08/2026
    Then TrainingCourse được tạo ở trạng thái "Planned"
    And chi phí khoá học phải >= 0 (BR-05)

  Scenario: Chi phí khoá học không được âm (BR-05)
    Given tôi đang tạo một TrainingCourse mới
    When tôi nhập CourseCostAmount là "-1000000"
    Then hệ thống từ chối và báo lỗi "Chi phí đào tạo không được nhỏ hơn 0"

  Scenario: Đăng ký nhân viên hiện hữu tham gia khoá học (BR-03)
    Given TrainingCourse "Kỹ năng vận hành máy CNC" đang ở trạng thái "Planned"
    When tôi đăng ký nhân viên "Nguyễn Văn A" (EmployeeId) làm người tham gia
    Then hệ thống tạo TrainingParticipant với ParticipantType là "Employee"
    And CandidateId của bản ghi này là null

  Scenario: Đăng ký ứng viên mới tham gia đào tạo hội nhập (BR-03)
    Given TrainingCourse "Đào tạo hội nhập Q3/2026" có TrainingType là "Onboarding"
    When tôi đăng ký ứng viên "Trần Thị B" (CandidateId) làm người tham gia
    Then hệ thống tạo TrainingParticipant với ParticipantType là "Candidate"
    And EmployeeId của bản ghi này là null

  Scenario: Không thể đăng ký người tham gia thiếu cả EmployeeId và CandidateId (BR-03)
    Given tôi đang đăng ký người tham gia mới cho một TrainingCourse
    When tôi không cung cấp EmployeeId lẫn CandidateId
    Then hệ thống từ chối và báo lỗi "Phải xác định người tham gia là nhân viên hoặc ứng viên"

  Scenario: Nhân viên đã nghỉ việc không được đăng ký đào tạo (BR-09)
    Given nhân viên "Lê Văn C" có WorkingStatus là "Resigned"
    When tôi cố gắng đăng ký nhân viên "Lê Văn C" vào một TrainingCourse
    Then hệ thống từ chối và báo lỗi "Nhân viên không ở trạng thái đang làm việc"

  Scenario: Không thể đăng ký vượt quá sức chứa khoá học (BR-07)
    Given TrainingCourse "Kỹ năng vận hành máy CNC" có MaxParticipants là 20 và đã có 20 người tham gia hợp lệ
    When tôi đăng ký thêm 1 người tham gia mới
    Then hệ thống từ chối và báo lỗi "Khoá học đã đủ số lượng tham gia tối đa"

Feature: Ghi nhận và duyệt kết quả đào tạo

  Scenario: Ghi nhận kết quả đào tạo sau khi khoá học kết thúc (BR-06)
    Given TrainingCourse "Kỹ năng vận hành máy CNC" đã EndDate là "12/08/2026" đã qua
    And TrainingParticipant "Nguyễn Văn A" có AttendanceStatus là "Attended"
    When tôi ghi nhận TrainingResult với ResultStatus là "Pass"
    Then hệ thống tạo TrainingResult ở trạng thái "Draft"

  Scenario: Không thể ghi nhận kết quả khi khoá học chưa kết thúc (BR-06)
    Given TrainingCourse "Kỹ năng vận hành máy CNC" có EndDate là ngày trong tương lai
    When tôi cố gắng ghi nhận TrainingResult cho một người tham gia
    Then hệ thống từ chối và báo lỗi "Khoá học chưa kết thúc"

  Scenario: Không thể ghi nhận kết quả cho người vắng mặt (BR-06)
    Given TrainingParticipant "Phạm Văn D" có AttendanceStatus là "Absent"
    When tôi cố gắng ghi nhận TrainingResult cho người này
    Then hệ thống từ chối và báo lỗi "Không thể ghi nhận kết quả cho người vắng mặt"

  Scenario: Kết quả đào tạo phải qua duyệt trước khi chính thức (BR-02)
    Given TrainingResult của "Nguyễn Văn A" đang ở trạng thái "PendingApproval"
    When cấp duyệt cuối cùng trong Approval Engine phê duyệt
    Then TrainingResult chuyển sang trạng thái "Approved"

Feature: Đào tạo hội nhập là điều kiện đánh giá thử việc

  Scenario: Kết quả đào tạo hội nhập đạt cho phép đánh giá thử việc (BR-04)
    Given ứng viên "Trần Thị B" có TrainingResult với IsOnboardingCondition là true, ResultStatus là "Pass", Status là "Approved"
    When hệ thống Probation kiểm tra điều kiện đánh giá thử việc "Đạt" cho "Trần Thị B"
    Then điều kiện đào tạo hội nhập được xác nhận thoả mãn
    And sự kiện OnboardingTrainingRequirementSatisfied được phát sinh

  Scenario: Không thể đánh giá thử việc đạt khi chưa hoàn tất đào tạo hội nhập (BR-04)
    Given ứng viên "Trần Thị B" có TrainingResult với IsOnboardingCondition là true, ResultStatus là "Fail"
    When hệ thống Probation cố gắng đánh giá thử việc "Đạt" cho "Trần Thị B"
    Then hệ thống từ chối và báo lỗi "Chưa hoàn tất điều kiện đào tạo hội nhập bắt buộc"

Feature: Quản lý danh mục và vòng đời kế hoạch

  Scenario: Không xoá kế hoạch đã có kết quả đào tạo (BR-08)
    Given TrainingPlan "TP-2026-001" đã có ít nhất 1 TrainingResult được ghi nhận
    When tôi cố gắng xoá TrainingPlan "TP-2026-001"
    Then hệ thống từ chối và báo lỗi "Không thể xoá kế hoạch đã có kết quả đào tạo, chỉ có thể huỷ"

  Scenario: Không xoá danh mục lĩnh vực đào tạo đang được sử dụng (BR-10)
    Given TrainingField "Kỹ năng vận hành máy" đang được tham chiếu bởi ít nhất 1 TrainingPlan hiệu lực
    When tôi cố gắng xoá TrainingField "Kỹ năng vận hành máy"
    Then hệ thống từ chối và báo lỗi "Danh mục đang được sử dụng, không thể xoá"
```

## Phần 3 — TDD

### 3.1 Bảng Test Case

| Mã TC | Kịch bản (Gherkin) | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | Đề xuất kế hoạch hợp lệ (BR-01) | Unit | `TrainingFieldId` set, `DepartmentId` set | Tạo thành công, `Status = Draft` |
| TC-02 | Thiếu TrainingFieldId (BR-01) | Unit | `TrainingFieldId = null` | Ném `DomainValidationException("TrainingFieldId là bắt buộc")` |
| TC-03 | Duyệt kế hoạch qua Approval Engine (BR-02) | Integration | `ApprovalRequestId` đã Approved | `TrainingPlan.Status = Approved` |
| TC-04 | Chuyển InProgress khi chưa duyệt (BR-02) | Unit | `Status = Draft`, gọi `StartExecution()` | Ném `InvalidStateTransitionException` |
| TC-05 | Chi phí khoá học âm (BR-05) | Unit | `CourseCostAmount = -1000000` | Ném `DomainValidationException` |
| TC-06 | Đăng ký participant Employee hợp lệ (BR-03) | Unit | `ParticipantType=Employee`, `EmployeeId=X`, `CandidateId=null` | Tạo thành công |
| TC-07 | Đăng ký participant Candidate hợp lệ (BR-03) | Unit | `ParticipantType=Candidate`, `CandidateId=Y`, `EmployeeId=null` | Tạo thành công |
| TC-08 | Participant thiếu cả 2 tham chiếu (BR-03) | Unit | `EmployeeId=null`, `CandidateId=null` | Ném `DomainValidationException` |
| TC-09 | Đăng ký nhân viên đã nghỉ việc (BR-09) | Integration | `EmployeeId` có `WorkingStatus=Resigned` | Ném `BusinessRuleViolationException` |
| TC-10 | Vượt sức chứa khoá học (BR-07) | Unit | `MaxParticipants=20`, đã có 20 active | Ném `CapacityExceededException` |
| TC-11 | Ghi nhận kết quả khi khoá học đã kết thúc + Attended (BR-06) | Unit | `EndDate` quá khứ, `AttendanceStatus=Attended` | Tạo `TrainingResult` thành công |
| TC-12 | Ghi nhận kết quả khi khoá học chưa kết thúc (BR-06 — boundary) | Unit | `EndDate = ngày hiện tại + 1` | Ném `DomainValidationException` |
| TC-13 | Ghi nhận kết quả khi khoá học kết thúc đúng hôm nay (BR-06 — boundary) | Unit | `EndDate = ngày hiện tại` | Cho phép tạo (biên hợp lệ) |
| TC-14 | Ghi nhận kết quả cho người Absent (BR-06) | Unit | `AttendanceStatus=Absent` | Ném `DomainValidationException` |
| TC-15 | Duyệt kết quả đào tạo qua Approval Engine (BR-02) | Integration | `TrainingResult.ApprovalRequestId` Approved | `TrainingResult.Status = Approved` |
| TC-16 | Probation gate — đào tạo hội nhập Pass + Approved (BR-04) | Integration | `IsOnboardingCondition=true`, `ResultStatus=Pass`, `Status=Approved` | `IsOnboardingRequirementSatisfied() == true` |
| TC-17 | Probation gate — đào tạo hội nhập Fail (BR-04) | Integration | `IsOnboardingCondition=true`, `ResultStatus=Fail` | `IsOnboardingRequirementSatisfied() == false`, chặn đánh giá thử việc Đạt |
| TC-18 | Xoá kế hoạch đã có kết quả (BR-08) | Unit | `TrainingPlan` có `TrainingResult` liên quan | Ném `DeleteNotAllowedException` |
| TC-19 | Xoá danh mục đang sử dụng (BR-10) | Integration | `TrainingField` đang tham chiếu bởi `TrainingPlan` hiệu lực | Ném `DeleteNotAllowedException` |

### 3.2 Gợi ý chữ ký code (Domain Service / Repository / Validator)

```csharp
// Aggregate
public class TrainingPlan : AggregateRoot<Guid>
{
    public TrainingPlanStatus Status { get; private set; }
    private readonly List<TrainingCourse> _courses = new();
    public IReadOnlyCollection<TrainingCourse> Courses => _courses.AsReadOnly();

    public static TrainingPlan Create(TrainingPlanCreateCommand cmd);
    public TrainingCourse AddCourse(TrainingCourseCreateCommand cmd);
    public void SubmitForApproval();
    public void ApplyApprovalResult(ApprovalOutcome outcome); // gọi bởi Approval Engine callback
    public void StartExecution();     // yêu cầu Status == Approved
    public void Complete();
    public void Cancel(string reason);
}

public class TrainingCourse : Entity<Guid>
{
    private readonly List<TrainingParticipant> _participants = new();
    public TrainingParticipant Enroll(EnrollParticipantCommand cmd, IEmployeeStatusReader employeeStatusReader);
    public TrainingResult RecordResult(Guid participantId, RecordResultCommand cmd);
}

// Domain services
public interface ITrainingPlanRepository
{
    Task<TrainingPlan?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(TrainingPlan plan, CancellationToken ct);
    Task<bool> HasApprovedCourseOrResultAsync(Guid trainingPlanId, CancellationToken ct); // hỗ trợ BR-08
}

public interface ITrainingResultRepository
{
    Task<TrainingResult?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TrainingResult>> GetOnboardingResultsByCandidateAsync(Guid candidateId, CancellationToken ct);
}

public class TrainingParticipantValidator
{
    // BR-03: XOR EmployeeId/CandidateId theo ParticipantType
    public ValidationResult Validate(EnrollParticipantCommand cmd);
}

public class TrainingCourseCapacityValidator
{
    // BR-07
    public ValidationResult Validate(TrainingCourse course, int currentActiveCount);
}

// Probation gate — được Personnel Decisions (11) / Recruitment (02) gọi
public interface IOnboardingTrainingGateService
{
    // BR-04
    Task<bool> IsOnboardingRequirementSatisfiedAsync(Guid candidateOrEmployeeId, CancellationToken ct);
}

public class OnboardingTrainingGateService : IOnboardingTrainingGateService
{
    private readonly ITrainingResultRepository _resultRepository;

    public async Task<bool> IsOnboardingRequirementSatisfiedAsync(Guid candidateOrEmployeeId, CancellationToken ct)
    {
        var results = await _resultRepository.GetOnboardingResultsByCandidateAsync(candidateOrEmployeeId, ct);
        if (!results.Any(r => r.IsOnboardingCondition)) return true; // không có điều kiện ràng buộc
        return results.Where(r => r.IsOnboardingCondition)
                       .All(r => r.ResultStatus == TrainingResultStatus.Pass && r.Status == TrainingResultApprovalStatus.Approved);
    }
}
```

### 3.3 Edge case / Boundary value cần lưu ý khi generate

- Biên `EndDate` của `TrainingCourse` so với "ngày hiện tại": kết thúc đúng hôm nay vẫn được phép ghi nhận kết quả (TC-13); kết thúc vào tương lai thì bị chặn (TC-12).
- Ràng buộc XOR `EmployeeId`/`CandidateId` phải kiểm tra ở cả tầng domain (validator) lẫn tầng DB (`CHECK constraint`) để tránh dữ liệu rác khi insert trực tiếp.
- `MaxParticipants = null` nghĩa là không giới hạn — validator phải bỏ qua kiểm tra sức chứa trong trường hợp này (không coi là 0).
- Khi 1 `TrainingParticipant` bị chuyển `AttendanceStatus = Cancelled` sau khi đã có `TrainingResult`, cần quyết định nghiệp vụ: giữ nguyên `TrainingResult` lịch sử nhưng đánh dấu không hợp lệ (khuyến nghị dùng `Status` riêng, không xoá).
