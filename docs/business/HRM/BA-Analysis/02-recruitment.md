# 02 — Recruitment (Tuyển dụng)

> **Bounded Context**: Recruitment
> **Loại**: Supporting Domain — pipeline vận hành, sinh ra `Employee Profile` (01) khi ứng viên trúng tuyển.
> **Phụ thuộc**: dùng chung **Approval Engine** (shared kernel, xem `05-request-approval.md`) để duyệt kế hoạch tuyển dụng, quyết định tuyển dụng, kết quả thử việc.
> **Quy ước đặt tên**: theo `00-domain-overview.md` mục 6 — bảng DB prefix `Rec_`, khoá chính `Id`, khoá ngoại `<Entity>Id`, soft-delete `IsDeleted`/`DeletedAtUtc`, audit `CreatedAtUtc/CreatedBy/LastModifiedAtUtc/LastModifiedBy` trên mọi bảng nghiệp vụ (không lặp lại trong từng bảng field bên dưới trừ khi cần nhấn mạnh).

---

## Phần 1 — DDD (Domain-Driven Design)

### 1.1 Bounded Context & Vai trò

Recruitment mô hình hoá toàn bộ pipeline tuyển dụng: **Kế hoạch tuyển dụng → Đợt tuyển dụng → Yêu cầu tuyển dụng → Ứng viên → Phỏng vấn (nhiều vòng) → Quyết định tuyển dụng → (nếu trúng tuyển) sinh Employee → Thử việc → (nếu đạt) chuyển sang Labor Contract chính thức**.

Ranh giới context: Recruitment sở hữu toàn bộ dữ liệu về `Candidate` (ứng viên — người **chưa là** nhân viên) cho tới khi có quyết định tuyển dụng. Khi ứng viên trúng tuyển, Recruitment **phát sinh domain event** yêu cầu `Employee Profile` (01) tạo hồ sơ nhân viên mới — Recruitment không sở hữu dữ liệu Employee, chỉ giữ tham chiếu `EmployeeId` sau khi hồ sơ được tạo (dùng cho giai đoạn Thử việc).

### 1.2 Ubiquitous Language (thuật ngữ riêng module)

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ | Giải thích |
|---|---|---|---|
| Kế hoạch tuyển dụng | Recruitment Plan | `HRMT2020`, `HRMF2000/2001` | Kế hoạch tuyển dụng tổng thể theo kỳ (năm/quý), là đơn vị duyệt cấp cao nhất |
| Đợt tuyển dụng | Recruitment Period / Batch | `HRMT2020` (RecruitPeriodID) | Một đợt tuyển cụ thể trong kế hoạch: số lượng, chi phí, số vòng phỏng vấn |
| Yêu cầu tuyển dụng | Recruitment Requirement | `HRMT2024`, `HRMF2010` | Tiêu chí công việc/ứng viên cho 1 vị trí trong đợt tuyển |
| Ứng viên | Candidate | `HRMT1030/1031`, `HRMF1031` | Người ứng tuyển, chưa là nhân viên |
| Phỏng vấn | Interview | `HRMF2041` | Một vòng đánh giá ứng viên |
| Hội đồng tuyển dụng | Recruitment Council / Interview Panel | `HRMF2021/2022` | Nhóm người cùng chấm điểm/phỏng vấn 1 ứng viên |
| Tiêu chí chấm điểm | Interview Criteria | `HRMT1011Checker` | Checklist tiêu chí đánh giá gắn theo `DutyID` |
| Quyết định tuyển dụng | Recruitment Decision | `HRMF2050`, `RecDecisionListEntity` | Quyết định chính thức tuyển hoặc không tuyển 1 hoặc nhiều ứng viên |
| Xác nhận tuyển dụng hàng loạt | Recruitment Batch Confirmation | `HRMF2054` | Xác nhận hàng loạt kết quả tuyển dụng, có thể gắn kết quả đào tạo hội nhập |
| Thử việc | Probation | `HRMF2200/2201` | Giai đoạn đánh giá nhân viên mới trước khi ký hợp đồng chính thức |
| Kết quả thử việc | Probation Result | `HRMF2201` | Đạt / Không đạt |
| Nguồn ứng viên CRM | CRM-sourced Candidate | `CRMT00002_REL` | Ứng viên phát sinh từ 1 Contact trong hệ thống CRM (integration point, không model chi tiết) |

### 1.3 Context Map cục bộ

```
 RecruitmentPlan (Aggregate Root)
   └─ RecruitmentPeriod (Entity)
        └─ RecruitmentRequirement (Entity)

 Candidate (Aggregate Root) ──references──► RecruitmentPeriodId
   │
   ├──referenced by── InterviewSchedule (Aggregate Root) ──contains──► InterviewPanelMember, InterviewCriteriaScore
   ├──referenced by── RecruitmentDecisionLine (trong RecruitmentDecision Aggregate)
   └──referenced by── Probation (Aggregate Root) ──► (sau khi có) EmployeeId

 RecruitmentDecision (Aggregate Root)
   └─ RecruitmentDecisionLine (Entity) ──► CandidateId, TrainingResultId (ref BC 09, optional)

 Approval Engine (shared kernel, 05) được RecruitmentPlan, RecruitmentDecision, Probation tham chiếu qua ApprovalWorkflowId.
```

> **Ghi chú thiết kế aggregate**: `RecruitmentPlan` là aggregate root bao bọc `RecruitmentPeriod` và `RecruitmentRequirement` vì các đợt tuyển và yêu cầu tuyển dụng luôn được tạo/duyệt trong phạm vi 1 kế hoạch (ràng buộc `TotalLevel`, `RecruitQuantity`, `CostBoundary` cần nhất quán trong cùng 1 giao dịch). `Candidate`, `InterviewSchedule`, `RecruitmentDecision`, `Probation` là các aggregate độc lập vì có vòng đời/tốc độ thay đổi riêng — chúng chỉ tham chiếu nhau qua Id (không nhúng object), đúng nguyên tắc 1 transaction = 1 aggregate.

### 1.4 Aggregates, Entities, Value Objects

#### Aggregate: RecruitmentPlan (Aggregate Root)

**RecruitmentPlan**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| PlanCode | NVARCHAR(30) | ✔ | Số chứng từ kế hoạch, tự sinh (mã cũ: `RecruitPlanVoucher`) |
| PlanYear | INT | ✔ | Năm kế hoạch |
| DepartmentId | Id (FK) | ✔ | Phòng ban chủ trì tuyển dụng (mã cũ: `DepartmentID`) |
| Description | NVARCHAR(500) | ○ | Diễn giải kế hoạch |
| Status | ENUM | ✔ | `Draft`, `PendingApproval`, `Approved`, `Rejected`, `Closed` |
| ApprovalWorkflowId | Id (FK, nullable) | ○ | Tham chiếu Approval Aggregate dùng chung (xem `05-request-approval.md`) |

**RecruitmentPeriod** (Entity con của RecruitmentPlan)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| RecruitmentPlanId | Id (FK) | ✔ | Kế hoạch chứa đợt tuyển này |
| PeriodCode | NVARCHAR(30) | ✔ | Số chứng từ đợt tuyển, tự sinh (mã cũ: `RecruitPeriodVoucher`) |
| DepartmentId | Id (FK) | ✔ | Phòng ban cần tuyển (mã cũ: `DepartmentID`) |
| TeamId | Id (FK, nullable) | ○ | Tổ/nhóm cần tuyển (mã cũ: `TeamID`) |
| DutyId | Id (FK) | ✔ | Vị trí/chức danh cần tuyển (mã cũ: `DutyID`) |
| PeriodFromDate | DATE | ✔ | Ngày bắt đầu đợt tuyển |
| PeriodToDate | DATE | ✔ | Ngày kết thúc đợt tuyển |
| RecruitQuantity | INT | ✔ | Số lượng cần tuyển (mã cũ: `RecruitQuantity`) |
| EstimatedCost | DECIMAL(18,2) | ○ | Chi phí dự kiến (mã cũ: `Cost`) |
| ActualCost | DECIMAL(18,2) | ○ | Chi phí thực tế phát sinh (mã cũ: `ActualCost`) |
| CostBoundary | DECIMAL(18,2) | ○ | Giới hạn chi phí cho phép (mã cũ: `CostBoundary`) |
| TotalLevel | INT | ✔ | Tổng số vòng phỏng vấn của đợt tuyển (mã cũ: `TotalLevel`) |
| Status | ENUM | ✔ | `Open`, `Closed`, `Cancelled` |

**RecruitmentRequirement** (Entity con của RecruitmentPeriod)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| RecruitmentPeriodId | Id (FK) | ✔ | Đợt tuyển gắn yêu cầu này |
| RequirementCode | NVARCHAR(30) | ✔ | Số chứng từ yêu cầu tuyển dụng, tự sinh (mã cũ: `RecruitRequireVoucher`) |
| MinAge | INT | ○ | Độ tuổi tối thiểu |
| MaxAge | INT | ○ | Độ tuổi tối đa |
| RequiredGender | ENUM (nullable) | ○ | Giới tính yêu cầu (nếu có) |
| ExpectedSalaryFrom | DECIMAL(18,2) | ○ | Mức lương đề xuất từ |
| ExpectedSalaryTo | DECIMAL(18,2) | ○ | Mức lương đề xuất đến |
| EducationLevelId | Id (FK, nullable) | ○ | Trình độ học vấn yêu cầu |
| LanguageRequirement | NVARCHAR(500) | ○ | Yêu cầu ngoại ngữ (mô tả) |
| ItSkillRequirement | NVARCHAR(500) | ○ | Yêu cầu kỹ năng IT |
| CommunicationSkillRequirement | NVARCHAR(500) | ○ | Yêu cầu kỹ năng giao tiếp |

#### Aggregate: Candidate (Aggregate Root)

**Candidate**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính (mã cũ: `CandidateID`) |
| CandidateCode | NVARCHAR(30) | ✔ | Số chứng từ ứng viên, tự sinh (mã cũ: `CandidateVoucher`, `APK`) |
| RecruitmentPeriodId | Id (FK) | ✔ | Đợt tuyển ứng viên nộp hồ sơ (mã cũ: `RecPeriodID`) |
| LastName | NVARCHAR(100) | ✔ | Họ (Value Object `PersonName`) |
| MiddleName | NVARCHAR(100) | ○ | Tên đệm (Value Object `PersonName`) |
| FirstName | NVARCHAR(100) | ✔ | Tên (Value Object `PersonName`) |
| Gender | ENUM | ✔ | Nam/Nữ/Khác |
| Birthday | DATE | ✔ | Ngày sinh |
| IdentifyCardNo | NVARCHAR(30) | ○ | Số CMND/CCCD (Value Object `IdentityDocument`) |
| IdentifyCardPlace | NVARCHAR(200) | ○ | Nơi cấp (Value Object `IdentityDocument`) |
| IdentifyCardDate | DATE | ○ | Ngày cấp (Value Object `IdentityDocument`) |
| PassportNo | NVARCHAR(30) | ○ | Số hộ chiếu (Value Object `IdentityDocument`) |
| EducationLevelId | Id (FK, nullable) | ○ | Trình độ học vấn |
| RequireSalary | DECIMAL(18,2) | ○ | Mức lương ứng viên yêu cầu (mã cũ: `RequireSalary`) |
| EvaluationKitId | Id (FK, nullable) | ○ | Bộ hồ sơ/đánh giá đính kèm (mã cũ: `EvaluationKitID`) |
| RecruitStatus | ENUM | ✔ | `Registered`, `Interviewing`, `Shortlisted`, `Hired`, `Rejected`, `Withdrawn` |
| CrmContactId | Id (nullable, ref ngoại context) | ○ | Ứng viên xuất phát từ Contact CRM (mã cũ: `CRMT00002_REL`) — integration point, không model chi tiết |

**CandidateLanguageSkill** (Entity con — chuẩn hoá `Language1..3ID+Level` cũ)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| CandidateId | Id (FK) | ✔ | Ứng viên sở hữu kỹ năng này |
| LanguageId | Id (FK) | ✔ | Ngoại ngữ (mã cũ: `LanguageXID`) |
| Level | ENUM/NVARCHAR(20) | ✔ | Trình độ (mã cũ: `LanguageXLevel`) |
| OrderNo | INT | ✔ | Thứ tự ưu tiên (thay cho Language1/2/3 cố định cột) |

**Value Objects của Candidate**: `PersonName {LastName, MiddleName, FirstName}`, `IdentityDocument {IdentifyCardNo, IdentifyCardPlace, IdentifyCardDate, PassportNo}`.

#### Aggregate: InterviewSchedule (Aggregate Root)

**InterviewSchedule**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| ScheduleCode | NVARCHAR(30) | ✔ | Số chứng từ lịch phỏng vấn, tự sinh (mã cũ: `InterviewScheduleVoucher`) |
| CandidateId | Id (FK) | ✔ | Ứng viên được phỏng vấn (mã cũ: `CandidateID`) |
| InterviewLevel | INT | ✔ | Vòng phỏng vấn thứ mấy (mã cũ: `InterviewLevel`, phải ≤ `TotalLevel` của Period) |
| InterviewTypeId | Id (FK) | ✔ | Loại phỏng vấn (mã cũ: `InterviewTypeID`) |
| ScheduledAt | DATETIME2 | ✔ | Thời điểm phỏng vấn |
| ResultFormatId | Id (FK) | ✔ | Định dạng thang điểm (mã cũ: `ResultFormat`) |
| FromValue | DECIMAL(9,2) | ✔ | Giá trị thấp nhất của thang điểm (mã cũ: `FromValue`) |
| ToValue | DECIMAL(9,2) | ✔ | Giá trị cao nhất của thang điểm (mã cũ: `ToValue`) |
| InterviewResult | DECIMAL(9,2) | ○ | Điểm/kết quả cuối cùng (mã cũ: `InterviewResult`) |
| InterviewStatus | ENUM | ✔ | `Scheduled`, `InProgress`, `Completed`, `Cancelled` |

**InterviewPanelMember** (Entity con — Hội đồng tuyển dụng)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| InterviewScheduleId | Id (FK) | ✔ | Buổi phỏng vấn |
| InterviewerEmployeeId | Id (FK → Employee Profile) | ✔ | Thành viên hội đồng (mã cũ: `HRMF2021/2022`) |
| RoleInPanel | ENUM | ✔ | `Chairman`, `Member` |
| ScoreGiven | DECIMAL(9,2) | ○ | Điểm thành viên này chấm |
| Comment | NVARCHAR(1000) | ○ | Nhận xét |

**InterviewCriteriaScore** (Entity con — checklist tiêu chí)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| InterviewScheduleId | Id (FK) | ✔ | Buổi phỏng vấn |
| DutyId | Id (FK) | ✔ | Vị trí áp dụng tiêu chí (mã cũ: `HRMT1011Checker.DutyID`) |
| DetailTypeId | Id (FK) | ✔ | Loại tiêu chí đánh giá (mã cũ: `DetailTypeID`) |
| IsChecked | BIT | ✔ | Đạt tiêu chí hay không (mã cũ: `Checked`) |
| Score | DECIMAL(9,2) | ○ | Điểm số tiêu chí (nếu có) |

#### Aggregate: RecruitmentDecision (Aggregate Root)

**RecruitmentDecision**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính (mã cũ: `RecDecisionID`) |
| DecisionCode | NVARCHAR(30) | ✔ | Số quyết định tuyển dụng, tự sinh (mã cũ: `RecDecisionVoucher`) |
| RecruitmentPeriodId | Id (FK) | ✔ | Đợt tuyển liên quan |
| DecisionDate | DATE | ✔ | Ngày ra quyết định |
| Status | ENUM | ✔ | `Draft`, `PendingApproval`, `Approved`, `Rejected`, `Issued` |
| ApprovalWorkflowId | Id (FK, nullable) | ○ | Tham chiếu Approval Aggregate dùng chung (xem `05-request-approval.md`) |

**RecruitmentDecisionLine** (Entity con — mỗi dòng ứng với 1 ứng viên, hỗ trợ quyết định hàng loạt)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| RecruitmentDecisionId | Id (FK) | ✔ | Quyết định chứa dòng này |
| CandidateId | Id (FK) | ✔ | Ứng viên được quyết định |
| DecisionResult | ENUM | ✔ | `Hired`, `Rejected` |
| OfferedSalary | DECIMAL(18,2) | ○ | Mức lương đề nghị khi trúng tuyển |
| OfferedTitleId | Id (FK, nullable) | ○ | Chức danh đề nghị |
| OfferedDepartmentId | Id (FK, nullable) | ○ | Phòng ban tiếp nhận |
| TrainingResultId | Id (nullable, ref BC 09) | ○ | Kết quả đào tạo hội nhập liên quan (mã cũ: `HRMF2054.TrainingResultID`), xem `09-training.md` |
| EmployeeId | Id (nullable, ref BC 01) | ○ | Điền vào sau khi Employee Profile được tạo từ dòng này |

#### Aggregate: Probation (Aggregate Root)

**Probation**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Id | ✔ | Khoá chính |
| ProbationCode | NVARCHAR(30) | ✔ | Số chứng từ thử việc, tự sinh |
| CandidateId | Id (FK) | ✔ | Ứng viên gốc (mã cũ: `HRMF2200.CandidateID`) |
| EmployeeId | Id (FK, nullable → Employee Profile) | ○ | Bắt buộc có giá trị khi Probation rời trạng thái `Draft` (nhân viên đã được tạo) |
| ProbationFromDate | DATE | ✔ | Ngày bắt đầu thử việc |
| ProbationToDate | DATE | ✔ | Ngày kết thúc thử việc dự kiến |
| EvaluatorEmployeeId | Id (FK, nullable) | ○ | Người đánh giá thử việc |
| ProbationResult | ENUM | ✔ | `Pending`, `Passed`, `Failed` (mã cũ: `HRMF2201`) |
| Status | ENUM | ✔ | `Draft`, `InProgress`, `PendingApproval`, `Approved`, `Rejected` |
| ApprovalWorkflowId | Id (FK, nullable) | ○ | Tham chiếu Approval Aggregate dùng chung (xem `05-request-approval.md`) |

### 1.5 Domain Events

| Domain Event | Phát sinh khi | Hệ quả / Consumer |
|---|---|---|
| `RecruitmentPlanCreated` | Tạo mới kế hoạch tuyển dụng | Khởi tạo luồng duyệt (Approval Engine) |
| `RecruitmentPlanApproved` / `RecruitmentPlanRejected` | Approval Engine trả kết quả duyệt kế hoạch | Cho phép/khoá tạo `RecruitmentPeriod` |
| `RecruitmentPeriodOpened` | Đợt tuyển được mở (Status=Open) | Cho phép đăng ký ứng viên |
| `RecruitmentPeriodClosed` | Hết hạn hoặc đủ số lượng tuyển | Khoá đăng ký ứng viên mới cho đợt |
| `CandidateRegistered` | Ứng viên nộp hồ sơ vào 1 đợt tuyển | Khởi tạo hồ sơ ứng viên |
| `CandidateSourcedFromCrm` | Ứng viên tạo từ Contact CRM | Integration event, ghi nhận `CrmContactId` |
| `InterviewScheduled` | Lên lịch phỏng vấn 1 vòng | Thông báo hội đồng/ứng viên |
| `InterviewCompleted` | Hội đồng hoàn tất chấm điểm 1 vòng | Cập nhật `Candidate.RecruitStatus` |
| `CandidateShortlisted` | Ứng viên qua đủ vòng phỏng vấn yêu cầu | Cho phép tạo `RecruitmentDecisionLine` |
| `RecruitmentDecisionIssued` | Quyết định tuyển dụng được ban hành (Approved) | Với dòng `Hired`: phát sinh `EmployeeProfileRequested` sang BC 01 |
| `EmployeeProfileRequested` | Dòng quyết định có `DecisionResult=Hired` | Employee Profile (01) tạo hồ sơ nhân viên mới, trả `EmployeeId` |
| `ProbationStarted` | `EmployeeId` được gán vào Probation | Bắt đầu theo dõi thử việc |
| `ProbationResultRecorded` | Người đánh giá nhập kết quả thử việc | Chuyển `ProbationResult` |
| `ProbationApproved` | Approval Engine duyệt kết quả thử việc | Nếu `Passed` → phát sinh `LaborContractRequested` sang BC 03; nếu `Failed` → phát sinh yêu cầu cập nhật `WorkingStatus` sang BC 01 |
| `LaborContractRequested` | Probation `Passed` và được duyệt | Labor Contract (03) khởi tạo hợp đồng chính thức |

### 1.6 Business Rules & Invariants

- **BR-01**: `RecruitmentPeriod` chỉ được tạo trong phạm vi 1 `RecruitmentPlan` đang tồn tại; không được tạo Period độc lập không thuộc Plan nào.
- **BR-02**: Tổng `RecruitQuantity` cộng dồn của các `RecruitmentRequirement` trong 1 `RecruitmentPeriod` không được vượt quá `RecruitQuantity` của chính Period đó.
- **BR-03**: `ActualCost` của `RecruitmentPeriod` không được vượt `CostBoundary`; nếu vượt phải có phê duyệt ngoại lệ riêng (ghi log lý do), hệ thống mặc định từ chối cập nhật vượt trần.
- **BR-04**: Một `Candidate` chỉ được đăng ký vào 1 `RecruitmentPeriod` đang ở trạng thái `Open` và trong khoảng `PeriodFromDate ≤ ngày nộp hồ sơ ≤ PeriodToDate`.
- **BR-05**: `InterviewLevel` của `InterviewSchedule` không được vượt quá `TotalLevel` được định nghĩa ở `RecruitmentPeriod` tương ứng.
- **BR-06**: `InterviewResult` ghi nhận phải nằm trong khoảng `[FromValue, ToValue]` của `ResultFormat` áp dụng cho buổi phỏng vấn đó.
- **BR-07**: `RecruitmentDecisionLine` chỉ được tạo cho ứng viên đã hoàn tất (`InterviewStatus = Completed`) **toàn bộ** các vòng phỏng vấn từ 1 đến `TotalLevel` của Period.
- **BR-08**: `RecruitmentDecision` chỉ chuyển `Status = Issued` sau khi đã đi hết các cấp duyệt của Approval Aggregate dùng chung (xem `05-request-approval.md`); chưa duyệt xong không được in/ban hành quyết định.
- **BR-09**: Khi `RecruitmentDecisionLine.DecisionResult = Hired` và quyết định được `Issued`, hệ thống bắt buộc phát sinh `EmployeeProfileRequested` và cập nhật `Candidate.RecruitStatus = Hired`; không cho phép Issue nếu dòng quyết định thiếu `OfferedTitleId`/`OfferedDepartmentId`.
- **BR-10**: `Probation.EmployeeId` bắt buộc phải có giá trị (không null) ngay khi `Status` rời khỏi `Draft` — không được khởi tạo giai đoạn thử việc cho ứng viên chưa có `EmployeeId`.
- **BR-11**: `ProbationResult = Failed` sau khi được duyệt (Approval Engine) thì **không** được phát sinh `LaborContractRequested`; đồng thời phải phát sinh sự kiện cập nhật `WorkingStatus` của Employee sang BC 01 (theo cấu hình công ty, ví dụ `ProbationFailed`/`Terminated`).
- **BR-12**: `ProbationResult = Passed` chỉ có hiệu lực (cho phép sinh Labor Contract chính thức) sau khi đi qua đủ các cấp duyệt của Approval Aggregate dùng chung.
- **BR-13**: Mọi số chứng từ tự sinh (`PlanCode`, `PeriodCode`, `RequirementCode`, `CandidateCode`, `ScheduleCode`, `DecisionCode`, `ProbationCode`) phải **duy nhất theo từng loại chứng từ**, sinh theo chính sách đánh số dùng chung của hệ thống (tương đương cấu hình `HRMF0010` cũ) — không model chi tiết bảng cấu hình đánh số trong tài liệu này.
- **BR-14**: Chứng từ (`RecruitmentPlan`, `RecruitmentDecision`, `Probation`) bị `Rejected` ở bất kỳ cấp duyệt nào thì dừng toàn bộ luồng — không được tự động chuyển sang bước kế tiếp trong pipeline.
- **BR-15**: Chứng từ đã bắt đầu duyệt (có ít nhất 1 cấp `Approved`) hoặc đã duyệt xong thì **không được sửa/xoá trực tiếp** — theo invariant chung của Approval Engine (xem `05-request-approval.md`).
- **BR-16**: Mỗi thành viên `InterviewPanelMember` chỉ được chấm điểm **một lần duy nhất** cho 1 `InterviewSchedule`; điểm cuối `InterviewResult` được tổng hợp từ các `ScoreGiven` theo công thức cấu hình (trung bình hoặc trọng số) — không hard-code công thức trong domain model.
- **BR-17**: `Candidate.CrmContactId` (nếu có) chỉ mang tính tham chiếu, không đồng bộ hai chiều tự động — thay đổi dữ liệu Contact bên CRM không tự động cập nhật `Candidate`.

### 1.7 Đề xuất DB Schema

| Bảng | Ghi chú khoá/chỉ mục |
|---|---|
| `Rec_RecruitmentPlan` | PK `Id`; UNIQUE INDEX `PlanCode`; INDEX `DepartmentId` |
| `Rec_RecruitmentPeriod` | PK `Id`; FK `RecruitmentPlanId` → `Rec_RecruitmentPlan.Id`; UNIQUE INDEX `PeriodCode`; INDEX `(DepartmentId, Status)` |
| `Rec_RecruitmentRequirement` | PK `Id`; FK `RecruitmentPeriodId` → `Rec_RecruitmentPeriod.Id`; UNIQUE INDEX `RequirementCode` |
| `Rec_Candidate` | PK `Id`; FK `RecruitmentPeriodId` → `Rec_RecruitmentPeriod.Id`; UNIQUE INDEX `CandidateCode`; INDEX `(RecruitmentPeriodId, RecruitStatus)`; INDEX `CrmContactId` |
| `Rec_CandidateLanguageSkill` | PK `Id`; FK `CandidateId` → `Rec_Candidate.Id`; INDEX `(CandidateId, OrderNo)` |
| `Rec_InterviewSchedule` | PK `Id`; FK `CandidateId` → `Rec_Candidate.Id`; UNIQUE INDEX `ScheduleCode`; INDEX `(CandidateId, InterviewLevel)` |
| `Rec_InterviewPanelMember` | PK `Id`; FK `InterviewScheduleId` → `Rec_InterviewSchedule.Id`; FK `InterviewerEmployeeId` → BC01 `Emp_Employee.Id`; UNIQUE INDEX `(InterviewScheduleId, InterviewerEmployeeId)` |
| `Rec_InterviewCriteriaScore` | PK `Id`; FK `InterviewScheduleId` → `Rec_InterviewSchedule.Id`; INDEX `(InterviewScheduleId, DutyId)` |
| `Rec_RecruitmentDecision` | PK `Id`; FK `RecruitmentPeriodId` → `Rec_RecruitmentPeriod.Id`; UNIQUE INDEX `DecisionCode`; FK `ApprovalWorkflowId` → shared kernel `App_ApprovalWorkflow.Id` |
| `Rec_RecruitmentDecisionLine` | PK `Id`; FK `RecruitmentDecisionId` → `Rec_RecruitmentDecision.Id`; FK `CandidateId` → `Rec_Candidate.Id`; FK `TrainingResultId` → BC09 (nullable); FK `EmployeeId` → BC01 (nullable); UNIQUE INDEX `(RecruitmentDecisionId, CandidateId)` |
| `Rec_Probation` | PK `Id`; FK `CandidateId` → `Rec_Candidate.Id`; FK `EmployeeId` → BC01 (nullable); FK `ApprovalWorkflowId` → shared kernel; INDEX `(EmployeeId, Status)` |

Mọi bảng trên bổ sung cột chuẩn theo `00-domain-overview.md` mục 6: `IsDeleted BIT`, `DeletedAtUtc`, `CreatedAtUtc`, `CreatedBy`, `LastModifiedAtUtc`, `LastModifiedBy`.

### 1.8 Quan hệ với Bounded Context khác

- **Employee Profile (01)**: Recruitment là **nguồn phát sinh** Employee Profile — `EmployeeProfileRequested` (từ `RecruitmentDecisionLine.Hired`) tạo hồ sơ nhân viên mới; `EmployeeId` được ghi ngược lại vào `Rec_RecruitmentDecisionLine.EmployeeId` và `Rec_Probation.EmployeeId`. Recruitment **không** sở hữu vòng đời Employee sau khi tạo.
- **Labor Contract (03)**: `Probation.ProbationResult = Passed` (đã duyệt) phát sinh `LaborContractRequested` — điểm khởi đầu để BC 03 tạo hợp đồng lao động chính thức. Xem chi tiết vòng đời hợp đồng ở `03-labor-contract.md`.
- **Training (09)**: `Rec_RecruitmentDecisionLine.TrainingResultId` tham chiếu kết quả đào tạo hội nhập (onboarding training) được quản lý bởi `09-training.md` — Recruitment chỉ giữ tham chiếu, không sở hữu dữ liệu đào tạo.
- **Approval Engine (05, shared kernel)**: `RecruitmentPlan`, `RecruitmentDecision`, `Probation` đều áp dụng Approval Aggregate dùng chung qua `ApprovalWorkflowId` — xem `05-request-approval.md` để biết cơ chế cấp duyệt, trạng thái, và bất biến liên quan. Tài liệu này không định nghĩa lại cơ chế duyệt.
- **CRM (ngoại context, integration point)**: `Rec_Candidate.CrmContactId` là tham chiếu một chiều tới Contact trong hệ thống CRM — chỉ ghi nhận nguồn gốc ứng viên, không model chi tiết trong bounded context này.

---

## Phần 2 — BDD (Behavior-Driven Development)

### User Story 1 — Quản lý Kế hoạch & Đợt tuyển dụng

**Là** Trưởng phòng Nhân sự, **tôi muốn** tạo kế hoạch tuyển dụng và mở các đợt tuyển trong kế hoạch, **để** kiểm soát số lượng và chi phí tuyển dụng theo ngân sách.

```gherkin
Feature: Quản lý Kế hoạch và Đợt tuyển dụng

  @BR-01
  Scenario: Không cho phép tạo Đợt tuyển dụng khi chưa gắn Kế hoạch tuyển dụng
    Given tôi đang tạo mới một đợt tuyển dụng (Recruitment Period)
    When tôi không chọn Kế hoạch tuyển dụng (RecruitmentPlanId) cho đợt tuyển
    Then hệ thống từ chối lưu và báo lỗi "Đợt tuyển dụng phải thuộc một Kế hoạch tuyển dụng"

  @BR-02
  Scenario: Từ chối yêu cầu tuyển dụng vượt số lượng của Đợt tuyển
    Given Đợt tuyển dụng "Tuyển QA Q3/2026" có RecruitQuantity = 10
    And đã có Yêu cầu tuyển dụng cho vị trí "QA Engineer" với số lượng 8
    When tôi thêm Yêu cầu tuyển dụng mới cho vị trí "QA Lead" với số lượng 5
    Then hệ thống từ chối lưu vì tổng số lượng (13) vượt quá RecruitQuantity (10) của đợt tuyển

  @BR-03
  Scenario: Cảnh báo/từ chối khi chi phí thực tế vượt giới hạn
    Given Đợt tuyển dụng "Tuyển QA Q3/2026" có CostBoundary = 50,000,000 VNĐ
    When tôi cập nhật ActualCost = 55,000,000 VNĐ mà không có phê duyệt ngoại lệ
    Then hệ thống từ chối cập nhật và báo lỗi "Chi phí thực tế vượt giới hạn cho phép"
```

### User Story 2 — Đăng ký & Quản lý Ứng viên

**Là** Nhân viên tuyển dụng, **tôi muốn** đăng ký ứng viên vào đúng đợt tuyển đang mở, **để** đảm bảo dữ liệu tuyển dụng nhất quán theo từng đợt.

```gherkin
Feature: Đăng ký Ứng viên

  @BR-04
  Scenario: Từ chối đăng ký ứng viên vào Đợt tuyển đã đóng
    Given Đợt tuyển dụng "Tuyển QA Q3/2026" có Status = "Closed"
    When Nhân viên tuyển dụng đăng ký ứng viên "Nguyễn Văn A" vào đợt tuyển này
    Then hệ thống từ chối và báo lỗi "Đợt tuyển dụng đã đóng, không thể tiếp nhận hồ sơ mới"

  @BR-04
  Scenario: Đăng ký ứng viên thành công vào Đợt tuyển đang mở và trong hạn
    Given Đợt tuyển dụng "Tuyển QA Q3/2026" có Status = "Open", PeriodFromDate = 01/07/2026, PeriodToDate = 31/07/2026
    When Nhân viên tuyển dụng đăng ký ứng viên "Nguyễn Văn A" vào ngày 13/07/2026
    Then hồ sơ ứng viên được tạo với RecruitStatus = "Registered"
    And sự kiện CandidateRegistered được phát sinh
```

### User Story 3 — Phỏng vấn nhiều vòng & Hội đồng tuyển dụng

**Là** Thành viên hội đồng tuyển dụng, **tôi muốn** chấm điểm ứng viên theo từng vòng phỏng vấn đúng thang điểm quy định, **để** đảm bảo kết quả đánh giá khách quan và có thể kiểm chứng.

```gherkin
Feature: Phỏng vấn ứng viên

  @BR-05
  Scenario: Từ chối lên lịch phỏng vấn vượt số vòng quy định
    Given Đợt tuyển dụng "Tuyển QA Q3/2026" có TotalLevel = 2
    When tôi lên lịch phỏng vấn vòng thứ 3 (InterviewLevel = 3) cho ứng viên "Nguyễn Văn A"
    Then hệ thống từ chối và báo lỗi "Vòng phỏng vấn vượt quá số vòng tối đa của đợt tuyển"

  @BR-06
  Scenario: Từ chối ghi nhận kết quả phỏng vấn ngoài thang điểm
    Given buổi phỏng vấn vòng 1 của ứng viên "Nguyễn Văn A" có thang điểm FromValue = 0, ToValue = 10
    When hội đồng nhập InterviewResult = 12
    Then hệ thống từ chối lưu và báo lỗi "Kết quả phỏng vấn phải nằm trong khoảng thang điểm cho phép"

  @BR-16
  Scenario: Một thành viên hội đồng không được chấm điểm 2 lần cho cùng 1 buổi phỏng vấn
    Given "Trần Thị B" đã chấm điểm cho buổi phỏng vấn vòng 1 của ứng viên "Nguyễn Văn A"
    When "Trần Thị B" cố gắng chấm điểm thêm một lần nữa cho cùng buổi phỏng vấn đó
    Then hệ thống từ chối và báo lỗi "Thành viên hội đồng đã chấm điểm buổi phỏng vấn này"

  @BR-07
  Scenario: Từ chối tạo Quyết định tuyển dụng khi ứng viên chưa hoàn tất đủ số vòng phỏng vấn
    Given Đợt tuyển dụng "Tuyển QA Q3/2026" có TotalLevel = 2
    And ứng viên "Nguyễn Văn A" mới hoàn tất phỏng vấn vòng 1 (InterviewStatus = "Completed")
    And vòng 2 chưa được thực hiện
    When Nhân viên tuyển dụng tạo dòng Quyết định tuyển dụng cho ứng viên này
    Then hệ thống từ chối và báo lỗi "Ứng viên chưa hoàn tất đủ các vòng phỏng vấn yêu cầu"
```

### User Story 4 — Ra Quyết định tuyển dụng & sinh Employee

**Là** Trưởng phòng Nhân sự, **tôi muốn** ban hành quyết định tuyển dụng sau khi được duyệt đủ cấp, **để** hệ thống tự động tạo hồ sơ nhân viên mới cho ứng viên trúng tuyển.

```gherkin
Feature: Quyết định tuyển dụng

  @BR-08
  Scenario: Không cho phép ban hành Quyết định tuyển dụng khi chưa duyệt xong
    Given Quyết định tuyển dụng "QD-2026-0042" đang ở cấp duyệt 1/3 (Approval Aggregate, xem 05-request-approval.md)
    When Trưởng phòng Nhân sự cố gắng ban hành (Issue) quyết định này
    Then hệ thống từ chối và báo lỗi "Quyết định chưa được duyệt đủ các cấp yêu cầu"

  @BR-09
  Scenario: Ban hành Quyết định tuyển dụng thành công tạo yêu cầu tạo Employee Profile
    Given Quyết định tuyển dụng "QD-2026-0042" đã được duyệt đủ các cấp (Status = "Approved")
    And dòng quyết định cho ứng viên "Nguyễn Văn A" có DecisionResult = "Hired", OfferedTitleId và OfferedDepartmentId đã điền đủ
    When Trưởng phòng Nhân sự ban hành quyết định
    Then Status của quyết định chuyển thành "Issued"
    And Candidate "Nguyễn Văn A".RecruitStatus chuyển thành "Hired"
    And sự kiện EmployeeProfileRequested được phát sinh sang Employee Profile (01)

  @BR-09
  Scenario: Từ chối ban hành dòng quyết định "Hired" thiếu thông tin đề nghị
    Given dòng quyết định cho ứng viên "Nguyễn Văn A" có DecisionResult = "Hired" nhưng OfferedTitleId để trống
    When Trưởng phòng Nhân sự ban hành quyết định
    Then hệ thống từ chối và báo lỗi "Thiếu chức danh đề nghị cho ứng viên trúng tuyển"
```

### User Story 5 — Thử việc & chuyển tiếp Hợp đồng chính thức

**Là** Trưởng phòng Nhân sự, **tôi muốn** theo dõi kết quả thử việc của nhân viên mới, **để** quyết định có ký hợp đồng lao động chính thức hay không.

```gherkin
Feature: Thử việc

  @BR-10
  Scenario: Không cho phép khởi tạo Thử việc khi chưa có Employee
    Given ứng viên "Nguyễn Văn A" đã trúng tuyển nhưng Employee Profile chưa được tạo (EmployeeId rỗng)
    When Nhân viên tuyển dụng khởi tạo giai đoạn thử việc cho ứng viên này với Status khác "Draft"
    Then hệ thống từ chối và báo lỗi "Chưa có hồ sơ nhân viên để bắt đầu thử việc"

  @BR-11
  Scenario: Thử việc không đạt thì không sinh yêu cầu tạo Hợp đồng chính thức
    Given nhân viên "Nguyễn Văn A" (EmployeeId đã có) đang trong giai đoạn thử việc
    When người đánh giá ghi nhận ProbationResult = "Failed" và kết quả được duyệt (Approval Engine)
    Then hệ thống KHÔNG phát sinh sự kiện LaborContractRequested
    And sự kiện cập nhật WorkingStatus của Employee được phát sinh sang Employee Profile (01)

  @BR-12
  Scenario: Thử việc đạt và được duyệt thì sinh yêu cầu tạo Hợp đồng chính thức
    Given nhân viên "Nguyễn Văn A" đang trong giai đoạn thử việc với ProbationResult = "Passed"
    When kết quả thử việc được duyệt đủ các cấp (Approval Aggregate, xem 05-request-approval.md)
    Then sự kiện LaborContractRequested được phát sinh sang Labor Contract (03)

  @BR-15
  Scenario: Không cho phép sửa Kết quả thử việc đã bắt đầu duyệt
    Given kết quả thử việc của "Nguyễn Văn A" đã có ít nhất 1 cấp duyệt "Approved"
    When người đánh giá cố gắng sửa trực tiếp ProbationResult
    Then hệ thống từ chối và báo lỗi "Chứng từ đã bắt đầu duyệt, không thể sửa trực tiếp"
```

### User Story 6 — Xác nhận tuyển dụng hàng loạt

**Là** Nhân viên tuyển dụng, **tôi muốn** xác nhận kết quả tuyển dụng cho nhiều ứng viên cùng lúc và gắn kết quả đào tạo hội nhập, **để** rút ngắn thời gian xử lý cuối đợt tuyển.

```gherkin
Feature: Xác nhận tuyển dụng hàng loạt

  @BR-13
  Scenario: Số chứng từ quyết định tuyển dụng phải duy nhất
    Given quyết định tuyển dụng "QD-2026-0042" đã tồn tại trong hệ thống
    When hệ thống tự sinh số chứng từ cho quyết định tuyển dụng mới
    Then số chứng từ mới phải khác "QD-2026-0042" và không trùng với bất kỳ số nào đã cấp trước đó

  Scenario: Gắn kết quả đào tạo hội nhập vào dòng quyết định khi xác nhận hàng loạt
    Given có 3 ứng viên trúng tuyển trong đợt "Tuyển QA Q3/2026" đã hoàn tất đào tạo hội nhập
    When Nhân viên tuyển dụng thực hiện xác nhận tuyển dụng hàng loạt
    Then mỗi dòng quyết định tương ứng được gắn TrainingResultId từ kết quả đào tạo (tham chiếu 09-training.md)
```

---

## Phần 3 — TDD (Test-Driven Development)

### 3.1 Bảng Test Case

| TC | Scenario tham chiếu | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | BR-01 | Unit | `RecruitmentPeriod` với `RecruitmentPlanId = null` | `ValidationException("Đợt tuyển dụng phải thuộc một Kế hoạch tuyển dụng")` |
| TC-02 | BR-02 | Unit | Period.RecruitQuantity=10; requirements hiện có tổng=8; thêm requirement quantity=5 | Reject, tổng 13 > 10 |
| TC-03 | BR-02 | Unit (boundary) | Period.RecruitQuantity=10; requirements tổng=8; thêm requirement quantity=2 | Accept, tổng đúng bằng 10 |
| TC-04 | BR-03 | Unit | CostBoundary=50,000,000; ActualCost update=55,000,000, không có approval ngoại lệ | Reject |
| TC-05 | BR-03 | Unit (boundary) | CostBoundary=50,000,000; ActualCost update=50,000,000 | Accept (bằng giới hạn vẫn hợp lệ) |
| TC-06 | BR-04 | Unit | Period.Status="Closed"; đăng ký Candidate mới | Reject |
| TC-07 | BR-04 | Integration | Period.Status="Open", FromDate=01/07/2026, ToDate=31/07/2026; đăng ký ngày 13/07/2026 | `CandidateRegistered` event; `RecruitStatus="Registered"` |
| TC-08 | BR-05 | Unit | Period.TotalLevel=2; tạo InterviewSchedule.InterviewLevel=3 | Reject |
| TC-09 | BR-05 | Unit (boundary) | Period.TotalLevel=2; tạo InterviewSchedule.InterviewLevel=2 | Accept |
| TC-10 | BR-06 | Unit | FromValue=0, ToValue=10; InterviewResult=12 | Reject |
| TC-11 | BR-06 | Unit (boundary) | FromValue=0, ToValue=10; InterviewResult=10 | Accept |
| TC-12 | BR-16 | Unit | InterviewerEmployeeId đã tồn tại `InterviewPanelMember` cho cùng `InterviewScheduleId` | Reject trùng chấm điểm |
| TC-13 | BR-07 | Integration | Candidate mới Completed vòng 1/2 | Reject tạo `RecruitmentDecisionLine` |
| TC-14 | BR-08 | Integration | `RecruitmentDecision.Status="PendingApproval"`, cấp duyệt 1/3 | Reject Issue |
| TC-15 | BR-09 | Integration | Decision Approved; line Hired đủ OfferedTitleId/OfferedDepartmentId | `EmployeeProfileRequested` phát sinh; `Candidate.RecruitStatus="Hired"` |
| TC-16 | BR-09 | Unit | line Hired nhưng OfferedTitleId=null | Reject Issue |
| TC-17 | BR-10 | Unit | Probation.EmployeeId=null; Status chuyển "InProgress" | Reject |
| TC-18 | BR-11 | Integration | ProbationResult="Failed", đã Approved | Không có `LaborContractRequested`; có event cập nhật `WorkingStatus` |
| TC-19 | BR-12 | Integration | ProbationResult="Passed", đã Approved đủ cấp | `LaborContractRequested` phát sinh |
| TC-20 | BR-15 | Unit | Probation có ≥1 cấp duyệt Approved; sửa trực tiếp ProbationResult | Reject |
| TC-21 | BR-13 | Unit | Sinh `DecisionCode` mới trong hệ thống đã có "QD-2026-0042" | Mã mới khác, không trùng |
| TC-22 | BR-14 | Integration | `RecruitmentPlan` bị Rejected ở cấp 2 | Không cho phép tạo `RecruitmentPeriod` mới thuộc plan này |

### 3.2 Gợi ý chữ ký Domain Service / Repository / Validator

```csharp
namespace Hrm.Recruitment.Domain
{
    public interface IRecruitmentPeriodRepository
    {
        Task<RecruitmentPeriod?> GetByIdAsync(Guid id);
        Task<int> GetRequirementQuantityTotalAsync(Guid recruitmentPeriodId);
        Task SaveAsync(RecruitmentPeriod period);
    }

    public class RecruitmentRequirementValidator
    {
        // BR-02
        public ValidationResult ValidateQuantity(RecruitmentPeriod period, RecruitmentRequirement newRequirement, int existingQuantityTotal);
    }

    public class RecruitmentPeriodCostPolicy
    {
        // BR-03
        public ValidationResult ValidateActualCost(decimal costBoundary, decimal newActualCost, bool hasCostExceptionApproval);
    }

    public class CandidateRegistrationService
    {
        // BR-04
        public Candidate RegisterCandidate(RecruitmentPeriod period, CandidateRegistrationRequest request, DateTime registrationDateUtc);
    }

    public class InterviewSchedulingService
    {
        // BR-05
        public InterviewSchedule ScheduleInterview(RecruitmentPeriod period, Candidate candidate, int interviewLevel, InterviewTypeId interviewTypeId, DateTime scheduledAtUtc);

        // BR-06
        public ValidationResult ValidateInterviewResult(decimal fromValue, decimal toValue, decimal interviewResult);

        // BR-16
        public ValidationResult ValidatePanelMemberNotAlreadyScored(Guid interviewScheduleId, Guid interviewerEmployeeId);
    }

    public class RecruitmentDecisionService
    {
        // BR-07
        public ValidationResult ValidateAllInterviewLevelsCompleted(Guid candidateId, int totalLevel);

        // BR-08, BR-09
        public Result<PersonnelEventEnvelope> IssueDecision(RecruitmentDecision decision, IApprovalStatusProvider approvalStatusProvider);
    }

    public class ProbationService
    {
        // BR-10
        public ValidationResult ValidateEmployeeAssigned(Probation probation);

        // BR-11, BR-12
        public Result<DomainEvent> ApplyApprovedProbationResult(Probation probation);

        // BR-15
        public ValidationResult ValidateNotLockedByApproval(Probation probation, IApprovalStatusProvider approvalStatusProvider);
    }

    public class VoucherNumberGenerator
    {
        // BR-13
        public string GenerateNext(string voucherType); // "CandidateVoucher" | "RecruitPlanVoucher" | ...
    }
}
```

**Edge case / boundary value cần bao phủ test**:
- `RecruitQuantity` tổng bằng chính xác giới hạn (đạt), lớn hơn 1 đơn vị (reject) — TC-02, TC-03.
- `ActualCost` bằng đúng `CostBoundary` (đạt) — TC-05.
- `InterviewResult` bằng đúng `FromValue`/`ToValue` (đạt), lệch 0.01 ngoài khoảng (reject).
- `InterviewLevel` bằng đúng `TotalLevel` (đạt), `TotalLevel + 1` (reject) — TC-08, TC-09.
- Candidate đăng ký đúng ngày `PeriodFromDate` và đúng ngày `PeriodToDate` (đạt, biên đầu/cuối).
- Approval ở cấp cuối cùng vừa `Approved` (cho phép Issue) so với còn 1 cấp `Pending` (reject) — TC-14/TC-15.
