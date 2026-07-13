# 10 — Maternity Benefits (Chế độ Thai sản)

> Bounded Context: **Maternity Benefits**. Prefix bảng DB: `Mat_`. Tài liệu tuân thủ quy ước đặt tên và Ubiquitous Language định nghĩa tại [`00-domain-overview.md`](./00-domain-overview.md). Nguồn khảo sát: `HRMT2340` (theo dõi ca thai sản — entity đầy đủ nhất tìm thấy), `HRMT2182`/`HRMT2342` (đính kèm giấy tờ). Nhiều Business Rule trong file này được **suy luận bổ sung theo Bộ luật Lao động và Luật BHXH Việt Nam hiện hành**, khớp với các mốc thời gian đã tìm thấy trong entity gốc — đánh dấu rõ trong từng rule.

## Phần 1 — DDD (Domain-Driven Design)

### 1.1 Bounded Context

Maternity Benefits theo dõi **toàn bộ vòng đời chế độ thai sản** của lao động nữ theo luật lao động Việt Nam: từ khi đăng ký mang thai, các mốc bảo vệ trong thai kỳ (cấm bố trí làm thêm giờ/ca đêm/công tác xa từ tháng thứ 7), điều kiện đủ để hưởng chế độ BHXH thai sản (đóng đủ 6 tháng BHXH trong 12 tháng trước sinh), thời gian nghỉ thai sản, đến khi quay lại làm việc và (tuỳ chọn) nghỉ thêm để chăm con nhỏ.

Bounded context này phụ thuộc Employee Profile (01) (chỉ áp dụng cho lao động nữ) và tích hợp chặt với Social Insurance (07) (điều kiện hưởng và chi trả chế độ do quỹ BHXH đảm nhiệm, không phải doanh nghiệp) và Request & Approval (05) (nghỉ thai sản có thể phát sinh 1 Leave Request tương ứng để đồng bộ lịch nghỉ/chấm công).

### 1.2 Ubiquitous Language (bổ sung riêng cho Maternity Benefits)

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ | Giải thích |
|---|---|---|---|
| Ca thai sản | Maternity Case | `HRMT2340` (`MaternityID`) | Hồ sơ theo dõi 1 lần mang thai/sinh con của 1 nhân viên |
| Ngày dự sinh | Expected Birth Date | `ExpectedBirthDate` | Ngày dự kiến sinh do bác sĩ xác định |
| Ngày sinh thực tế | Actual Birth Date | `ActualBirthDate` | Ngày sinh con thực tế |
| Ngày bắt đầu thai kỳ | Pregnancy Start Date | `PregnancyStartDate` | Ngày ghi nhận bắt đầu thai kỳ trong hệ thống |
| Mốc đủ 6 tháng | Six-Month Eligibility Date | `Enough6MonthsDate` | Ngày xác nhận đã đóng đủ 6 tháng BHXH trong 12 tháng trước sinh — điều kiện hưởng chế độ |
| Nhắc nhở cấm tăng ca | No-Overtime Reminder Date | `NoOTReminderDate` | Mốc thời gian (từ tháng thứ 7 thai kỳ) nhắc hệ thống chặn bố trí OT/ca đêm/công tác xa |
| Nghỉ thai sản | Maternity Leave | `MaternityLeaveStartDate/EndDate` | Thời gian nghỉ sinh theo luật (tiêu chuẩn 6 tháng) |
| Ngày quay lại làm việc | Actual Return Date | `ActualReturnDate` | Ngày nhân viên thực tế đi làm lại sau nghỉ thai sản |
| Nghỉ chăm con nhỏ | Childcare Leave | `ChildcareLeaveStartDate/EndDate` | Thời gian nghỉ thêm (không lương/theo thoả thuận) sau khi hết nghỉ thai sản |
| Kết thúc hưởng chế độ | End of Benefit | `EndOfBenefitDate` | Ngày chế độ thai sản (BHXH chi trả) kết thúc |
| Đính kèm hồ sơ | Case Attachment | `HRMT2342` | Giấy tờ/ảnh scan liên quan ca thai sản (giấy khai sinh, giấy chứng nhận nghỉ việc hưởng BHXH...) |

### 1.3 Aggregate / Entity / Value Object

#### Aggregate Root: `MaternityCase` (mã cũ `HRMT2340`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính (mã cũ `MaternityID`) |
| EmployeeId | Guid (FK) | Có | Nhân viên — **phải là lao động nữ** (BR-01), tham chiếu Employee Profile (01) |
| PregnancyStartDate | date | Không | Ngày bắt đầu thai kỳ được ghi nhận |
| ExpectedBirthDate | date | Có | Ngày dự sinh |
| ActualBirthDate | date | Không | Ngày sinh thực tế (cập nhật sau khi sinh) |
| Enough6MonthsDate | date | Không | Ngày xác nhận đủ điều kiện đóng BHXH 6/12 tháng (BR-04) — tính toán, có thể null nếu chưa đủ điều kiện |
| IsEligibleForBenefit | bool | Có | Cờ dẫn xuất từ `Enough6MonthsDate` — có đủ điều kiện hưởng chế độ BHXH thai sản hay không |
| NoOTReminderDate | date | Không | Mốc bắt đầu tháng thứ 7 thai kỳ — kích hoạt BR-02 (cấm OT/ca đêm/công tác xa) |
| MaternityLeaveStartDate | date | Không | Ngày bắt đầu nghỉ thai sản (có thể trước ngày sinh tối đa 2 tháng theo luật, BR-05) |
| MaternityLeaveEndDate | date | Không | Ngày kết thúc nghỉ thai sản (mặc định `MaternityLeaveStartDate + 6 tháng`, BR-05) |
| ActualReturnDate | date | Không | Ngày thực tế quay lại làm việc |
| ChildcareLeaveStartDate | date | Không | Ngày bắt đầu nghỉ chăm con nhỏ (tuỳ chọn, sau khi hết nghỉ thai sản) |
| ChildcareLeaveEndDate | date | Không | Ngày kết thúc nghỉ chăm con nhỏ |
| EndOfBenefitDate | date | Không | Ngày kết thúc hưởng chế độ (do BHXH chi trả) |
| Status | enum | Có | `Registered` → `PregnancyConfirmed` → `EligibilityConfirmed` (đủ 6 tháng) → `OnMaternityLeave` → `ReturnedToWork` → `OnChildcareLeave` (tuỳ chọn) → `BenefitEnded` — xem 1.4 State Machine |
| RelatedLeaveRequestId | Guid (FK → `Req_LeaveRequest`, module 05) | Không | Đơn nghỉ tương ứng được tự động sinh khi bắt đầu nghỉ thai sản (BR-08) |
| RelatedInsuranceContributionRefId | Guid (FK → `Ins_InsuranceContribution`, module 07) | Không | Tham chiếu tới bằng chứng đóng BHXH dùng xác nhận `Enough6MonthsDate` |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `IX_Mat_MaternityCase_Employee (EmployeeId, ExpectedBirthDate)`.

#### Entity con: `MaternityCaseAttachment` (mã cũ `HRMT2342`, dùng lại Value Object `Attachment` chung — mã cũ `HRMT2182`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| MaternityCaseId | Guid (FK) | Có | Ca thai sản liên quan |
| DocumentType | enum | Không | Loại giấy tờ (VD: `BirthCertificate`, `MaternityLeaveCertificate`, `MedicalRecord`) |
| AttachFileUrl | string(500) | Có | Đường dẫn file đính kèm |
| AttachName | string(200) | Có | Tên hiển thị file |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

### 1.4 State Machine của `MaternityCase.Status`

```
Registered ──► PregnancyConfirmed ──► EligibilityConfirmed ──► OnMaternityLeave ──► ReturnedToWork
                                            │                                            │
                                            │ (không đủ 6 tháng BHXH)                    ▼
                                            ▼                                    OnChildcareLeave (tuỳ chọn)
                                    NotEligibleForBenefit                                │
                                                                                           ▼
                                                                                     BenefitEnded
```

- `Registered`: hồ sơ được tạo, mới có `PregnancyStartDate`/`ExpectedBirthDate`.
- `PregnancyConfirmed`: xác nhận y tế, dữ liệu thai kỳ hợp lệ.
- `EligibilityConfirmed`: hệ thống xác nhận `Enough6MonthsDate` có giá trị (đủ 6/12 tháng đóng BHXH) → `IsEligibleForBenefit = true`. Nếu không đủ điều kiện, chuyển `NotEligibleForBenefit` (BR-04) — vẫn được nghỉ thai sản theo luật lao động nhưng **không** được BHXH chi trả.
- `OnMaternityLeave`: đang trong thời gian nghỉ thai sản (`MaternityLeaveStartDate` ≤ hôm nay ≤ `MaternityLeaveEndDate`).
- `ReturnedToWork`: có `ActualReturnDate`.
- `OnChildcareLeave`: tuỳ chọn, nếu nhân viên đăng ký nghỉ thêm chăm con.
- `BenefitEnded`: có `EndOfBenefitDate`, ca thai sản coi như đóng.

### 1.5 Domain Events

| Domain Event | Khi nào phát sinh | Payload chính |
|---|---|---|
| `MaternityCaseRegistered` | Tạo hồ sơ thai sản mới | `MaternityCaseId, EmployeeId, ExpectedBirthDate` |
| `MaternityEligibilityConfirmed` | Xác nhận đủ 6/12 tháng đóng BHXH | `MaternityCaseId, Enough6MonthsDate` |
| `MaternityEligibilityDenied` | Xác nhận KHÔNG đủ điều kiện hưởng BHXH | `MaternityCaseId, Reason` |
| `NoOvertimeProtectionActivated` | Đến mốc tháng thứ 7 thai kỳ | `MaternityCaseId, EmployeeId, NoOTReminderDate` |
| `MaternityLeaveStarted` | Bắt đầu nghỉ thai sản, tự sinh Leave Request | `MaternityCaseId, MaternityLeaveStartDate, RelatedLeaveRequestId` |
| `MaternityLeaveEnded` | Hết thời gian nghỉ thai sản chuẩn | `MaternityCaseId, MaternityLeaveEndDate` |
| `EmployeeReturnedFromMaternityLeave` | Nhân viên quay lại làm việc | `MaternityCaseId, ActualReturnDate` |
| `ChildcareLeaveStarted` / `ChildcareLeaveEnded` | Bắt đầu/kết thúc nghỉ chăm con nhỏ | `MaternityCaseId, StartDate/EndDate` |
| `MaternityBenefitEnded` | Kết thúc hưởng chế độ | `MaternityCaseId, EndOfBenefitDate` |

### 1.6 Business Rules & Invariants

- **BR-01** *(suy luận theo luật, khớp thiết kế)*: `MaternityCase` chỉ được tạo cho nhân viên có `Employee.IsMale = false` — hệ thống từ chối tạo ca thai sản cho nhân viên nam.
- **BR-02** *(khớp trực tiếp field `NoOTReminderDate`)*: Từ tháng thứ 7 của thai kỳ (mốc `NoOTReminderDate`), hệ thống phải **chặn** việc bố trí nhân viên vào: đơn tăng ca (`Req_OvertimeRequest`, module 05), ca đêm, hoặc đơn công tác xa (`Req_BusinessTripRequest`, module 05) — vi phạm bị hệ thống từ chối khi tạo các loại đơn này cho nhân viên đang trong `MaternityCase` active với `NoOTReminderDate ≤ hôm nay`.
- **BR-03** *(suy luận theo luật)*: Lao động nữ mang thai được nghỉ đi khám thai (không tính vào ngày nghỉ phép năm) — mô hình hoá qua loại `AbsentTypeID` riêng tại Request & Approval (05), tham chiếu chứ không định nghĩa lại ở đây.
- **BR-04** *(khớp trực tiếp field `Enough6MonthsDate`)*: Điều kiện hưởng chế độ BHXH thai sản là đã đóng BHXH **đủ 6 tháng trong vòng 12 tháng trước khi sinh** (`ActualBirthDate` hoặc `ExpectedBirthDate` nếu chưa sinh) — hệ thống xác định `Enough6MonthsDate` bằng cách tổng hợp lịch sử `Ins_InsuranceContribution` (module 07) của nhân viên; nếu không đủ, `IsEligibleForBenefit = false` và chuyển trạng thái `NotEligibleForBenefit`.
- **BR-05** *(suy luận theo luật)*: Thời gian nghỉ thai sản tiêu chuẩn là **6 tháng**, trong đó nhân viên có thể nghỉ **trước sinh tối đa 2 tháng**; `MaternityLeaveStartDate` không được sớm hơn `ExpectedBirthDate - 2 tháng`, và nếu không cấu hình riêng thì `MaternityLeaveEndDate = MaternityLeaveStartDate + 6 tháng`.
- **BR-06** *(suy luận theo luật)*: Trường hợp sinh đôi trở lên, từ con thứ 2 trở đi, mỗi con được cộng thêm thời gian nghỉ (theo luật hiện hành) — đánh dấu **Data Gap**: cần bổ sung field `ChildCount` và quy tắc cộng thêm ngày nghỉ khi generate code, entity gốc `HRMT2340` không thể hiện rõ trường hợp đa thai.
- **BR-07**: Chế độ thai sản (tiền trợ cấp trong thời gian nghỉ) do **quỹ BHXH chi trả**, không phải doanh nghiệp trả lương — vì vậy `MaternityCase` không sinh ra `Pay_PayrollSlipLine` thu nhập từ doanh nghiệp cho giai đoạn `OnMaternityLeave` (trừ khi doanh nghiệp có chính sách hỗ trợ thêm, ghi nhận riêng nếu có).
- **BR-08**: Khi `MaternityCase` chuyển sang `OnMaternityLeave`, hệ thống **tự động sinh** 1 `Req_LeaveRequest` tương ứng bên Request & Approval (05) với khoảng thời gian trùng `MaternityLeaveStartDate`–`MaternityLeaveEndDate`, để đồng bộ vào lịch nghỉ/chấm công — `RelatedLeaveRequestId` lưu liên kết này, và đơn này **không** cần chạy qua chu trình duyệt đa cấp thông thường (được tạo với trạng thái đã duyệt do đây là quyền lợi theo luật, không phải yêu cầu cần xét duyệt).
- **BR-09**: Sau khi hết `MaternityLeaveEndDate`, nếu nhân viên không có `ActualReturnDate` và không có `ChildcareLeaveStartDate`, hệ thống cần cảnh báo (không tự động chuyển trạng thái) để HR xác minh thủ công.
- **BR-10**: `ChildcareLeaveStartDate` chỉ hợp lệ khi đã có `ActualReturnDate` hoặc `MaternityLeaveEndDate` đã qua — không được đăng ký nghỉ chăm con nhỏ chồng lấn thời gian nghỉ thai sản chính thức.

### 1.7 Đề xuất DB Schema (tổng hợp)

```
Mat_MaternityCase            (Id PK, EmployeeId FK, PregnancyStartDate, ExpectedBirthDate, ActualBirthDate,
                               Enough6MonthsDate, IsEligibleForBenefit, NoOTReminderDate,
                               MaternityLeaveStartDate, MaternityLeaveEndDate, ActualReturnDate,
                               ChildcareLeaveStartDate, ChildcareLeaveEndDate, EndOfBenefitDate,
                               Status, RelatedLeaveRequestId FK, RelatedInsuranceContributionRefId FK, ...)
Mat_MaternityCaseAttachment  (Id PK, MaternityCaseId FK, DocumentType, AttachFileUrl, AttachName, ...)
```

Mọi bảng nghiệp vụ đều có `IsDeleted, DeletedAtUtc, CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy` theo quy ước chung.

### 1.8 Quan hệ với Bounded Context khác

| Bounded Context | Quan hệ |
|---|---|
| Employee Profile (01) | `EmployeeId`, ràng buộc `IsMale = false` (BR-01) |
| Social Insurance (07) | Nguồn xác định `Enough6MonthsDate`/`IsEligibleForBenefit` qua lịch sử `Ins_InsuranceContribution`; quỹ BHXH là bên chi trả chế độ (BR-07) |
| Request & Approval (05) | Tự động sinh `Req_LeaveRequest` khi bắt đầu nghỉ thai sản (BR-08); chặn tạo `Req_OvertimeRequest`/`Req_BusinessTripRequest` từ tháng thứ 7 thai kỳ (BR-02) |
| Payroll (06) | Cấp biến `TS` (hệ số/tỷ lệ liên quan thai sản) cho công thức lương khi có áp dụng chính sách hỗ trợ thêm của doanh nghiệp |

## Phần 2 — BDD (Behavior-Driven Development)

### User Stories

- **US-01**: Là nhân viên HR, tôi muốn đăng ký hồ sơ thai sản cho nhân viên nữ để hệ thống tự động theo dõi các mốc thời gian bảo vệ theo luật.
- **US-02**: Là hệ thống, tôi muốn tự động chặn bố trí OT/công tác xa cho lao động nữ mang thai từ tháng thứ 7 để tuân thủ luật lao động.
- **US-03**: Là nhân viên HR, tôi muốn hệ thống tự xác định nhân viên có đủ điều kiện hưởng chế độ BHXH thai sản hay không dựa trên lịch sử đóng bảo hiểm.
- **US-04**: Là nhân viên HR, tôi muốn khi nhân viên bắt đầu nghỉ thai sản, hệ thống tự động tạo đơn nghỉ tương ứng để đồng bộ chấm công mà không cần qua duyệt thủ công.

```gherkin
Feature: Đăng ký và ràng buộc hồ sơ thai sản
  Là nhân viên HR
  Tôi muốn đăng ký hồ sơ thai sản chỉ cho lao động nữ
  Để đảm bảo dữ liệu tuân thủ đúng đối tượng áp dụng luật

  @BR-01
  Scenario: Từ chối đăng ký hồ sơ thai sản cho nhân viên nam
    Given nhân viên "NV010" có Employee.IsMale = true
    When HR đăng ký hồ sơ thai sản cho "NV010"
    Then hệ thống từ chối tạo MaternityCase
    And thông báo lỗi "Chế độ thai sản chỉ áp dụng cho lao động nữ"

  @BR-01
  Scenario: Đăng ký thành công hồ sơ thai sản cho lao động nữ
    Given nhân viên "NV011" có Employee.IsMale = false
    When HR đăng ký hồ sơ thai sản cho "NV011" với ExpectedBirthDate = 15/12/2026
    Then MaternityCase được tạo với Status = "Registered"


Feature: Bảo vệ lao động nữ mang thai từ tháng thứ 7
  Là hệ thống
  Tôi muốn tự động chặn bố trí tăng ca/công tác xa cho lao động nữ mang thai từ tháng thứ 7
  Để tuân thủ Bộ luật Lao động Việt Nam

  Background:
    Given nhân viên "NV011" có MaternityCase đang active với NoOTReminderDate = 01/06/2026

  @BR-02
  Scenario: Từ chối tạo đơn tăng ca sau mốc tháng thứ 7
    Given hôm nay là 10/06/2026 (sau NoOTReminderDate)
    When nhân viên "NV011" tạo đơn xin làm thêm giờ (DXLTG)
    Then hệ thống từ chối tạo đơn
    And thông báo lỗi "Không được bố trí làm thêm giờ cho lao động nữ mang thai từ tháng thứ 7"

  @BR-02
  Scenario: Cho phép tạo đơn tăng ca trước mốc tháng thứ 7
    Given hôm nay là 01/03/2026 (trước NoOTReminderDate)
    When nhân viên "NV011" tạo đơn xin làm thêm giờ (DXLTG)
    Then hệ thống cho phép tạo đơn bình thường


Feature: Xác định điều kiện hưởng chế độ BHXH thai sản
  Là hệ thống
  Tôi muốn tự động xác định nhân viên có đủ 6 tháng đóng BHXH trong 12 tháng trước sinh hay không
  Để quyết định có ghi nhận IsEligibleForBenefit hay không

  @BR-04
  Scenario: Xác nhận đủ điều kiện hưởng chế độ
    Given nhân viên "NV011" có ExpectedBirthDate = 15/12/2026
    And "NV011" có 7 tháng đóng BHXH liên tục trong khoảng 15/12/2025-15/12/2026 (Ins_InsuranceContribution)
    When hệ thống đánh giá điều kiện hưởng chế độ cho MaternityCase của "NV011"
    Then Enough6MonthsDate được xác định có giá trị
    And IsEligibleForBenefit = true, Status chuyển "EligibilityConfirmed"

  @BR-04
  Scenario: Xác nhận KHÔNG đủ điều kiện hưởng chế độ
    Given nhân viên "NV012" có ExpectedBirthDate = 15/12/2026
    And "NV012" chỉ có 4 tháng đóng BHXH trong khoảng 15/12/2025-15/12/2026
    When hệ thống đánh giá điều kiện hưởng chế độ cho MaternityCase của "NV012"
    Then IsEligibleForBenefit = false, Status chuyển "NotEligibleForBenefit"
    And nhân viên vẫn được nghỉ thai sản theo luật lao động nhưng không được BHXH chi trả


Feature: Tự động sinh đơn nghỉ khi bắt đầu nghỉ thai sản
  Là hệ thống
  Tôi muốn tự động tạo Leave Request khi MaternityCase chuyển sang nghỉ thai sản
  Để đồng bộ lịch nghỉ/chấm công mà không cần nhân viên tự tạo đơn

  @BR-08
  Scenario: Tự động tạo Leave Request đã duyệt sẵn khi bắt đầu nghỉ thai sản
    Given MaternityCase của "NV011" có MaternityLeaveStartDate = 15/10/2026 và MaternityLeaveEndDate = 15/04/2027
    When ngày hệ thống chuyển đến 15/10/2026
    Then MaternityCase chuyển Status = "OnMaternityLeave"
    And hệ thống tự động tạo Req_LeaveRequest cho "NV011" từ 15/10/2026 đến 15/04/2027
    And Req_LeaveRequest này có trạng thái đã duyệt, không cần đi qua Approval Engine thông thường

  @BR-05
  Scenario: Nghỉ thai sản trước sinh không vượt quá 2 tháng
    Given nhân viên "NV013" có ExpectedBirthDate = 15/12/2026
    When HR đặt MaternityLeaveStartDate = 01/09/2026 (hơn 3 tháng trước ngày dự sinh)
    Then hệ thống từ chối, thông báo "Không được nghỉ thai sản trước sinh quá 2 tháng"


Feature: Quay lại làm việc và nghỉ chăm con nhỏ
  Là nhân viên HR
  Tôi muốn ghi nhận ngày nhân viên quay lại làm việc và (tuỳ chọn) đăng ký nghỉ thêm chăm con

  @BR-10
  Scenario: Từ chối đăng ký nghỉ chăm con nhỏ chồng lấn nghỉ thai sản chính thức
    Given MaternityCase của "NV011" có MaternityLeaveEndDate = 15/04/2027, chưa có ActualReturnDate
    When HR đăng ký ChildcareLeaveStartDate = 01/04/2027 (trước khi hết nghỉ thai sản)
    Then hệ thống từ chối, thông báo "Nghỉ chăm con nhỏ chỉ được đăng ký sau khi hết nghỉ thai sản"

  Scenario: Ghi nhận quay lại làm việc thành công
    Given MaternityCase của "NV011" có MaternityLeaveEndDate = 15/04/2027
    When HR ghi nhận ActualReturnDate = 16/04/2027
    Then MaternityCase chuyển Status = "ReturnedToWork"
```

## Phần 3 — TDD (Test-Driven Development)

### 3.1 Bảng Test Case

| TC | Scenario tham chiếu | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | Từ chối đăng ký cho nhân viên nam (BR-01) | Unit | `Employee.IsMale = true` | Ném lỗi nghiệp vụ, không tạo `MaternityCase` |
| TC-02 | Đăng ký thành công cho nữ (BR-01) | Unit | `Employee.IsMale = false` | Tạo `MaternityCase`, `Status = Registered` |
| TC-03 | Chặn tạo đơn OT sau mốc tháng 7 (BR-02) | Integration | Hôm nay > `NoOTReminderDate` | `Req_OvertimeRequest` bị từ chối tạo |
| TC-04 | Cho phép tạo đơn OT trước mốc tháng 7 (BR-02, boundary) | Integration | Hôm nay < `NoOTReminderDate` | Đơn được tạo bình thường |
| TC-05 | Biên đúng ngày `NoOTReminderDate` (BR-02, boundary) | Integration (boundary) | Hôm nay == `NoOTReminderDate` | Cần xác nhận nghiệp vụ: chặn hay chưa chặn đúng ngày đó — đánh dấu Data Gap |
| TC-06 | Đủ điều kiện hưởng chế độ (BR-04) | Integration | 7 tháng đóng BHXH trong 12 tháng trước sinh | `IsEligibleForBenefit = true` |
| TC-07 | Đúng biên 6 tháng đóng BHXH (BR-04, boundary) | Integration (boundary) | Đúng 6 tháng đóng trong 12 tháng trước sinh | `IsEligibleForBenefit = true` (đủ điều kiện tối thiểu) |
| TC-08 | Không đủ điều kiện (5 tháng đóng, BR-04, boundary) | Integration (boundary) | 5 tháng đóng trong 12 tháng trước sinh | `IsEligibleForBenefit = false` |
| TC-09 | Tự động sinh Leave Request khi bắt đầu nghỉ (BR-08) | Integration | `MaternityLeaveStartDate` đến hạn | Tạo `Req_LeaveRequest` đã duyệt sẵn, `RelatedLeaveRequestId` được gán |
| TC-10 | Từ chối nghỉ trước sinh quá 2 tháng (BR-05) | Unit | `MaternityLeaveStartDate` sớm hơn `ExpectedBirthDate - 2 tháng` | Ném lỗi nghiệp vụ |
| TC-11 | Đúng biên nghỉ trước sinh 2 tháng (BR-05, boundary) | Unit (boundary) | `MaternityLeaveStartDate = ExpectedBirthDate - 2 tháng` chính xác | Cho phép, không lỗi |
| TC-12 | Mặc định 6 tháng nghỉ thai sản khi không cấu hình riêng (BR-05) | Unit | Không truyền `MaternityLeaveEndDate` | `MaternityLeaveEndDate` tự tính = `Start + 6 tháng` |
| TC-13 | Từ chối nghỉ chăm con chồng lấn nghỉ thai sản (BR-10) | Unit | `ChildcareLeaveStartDate` trước `MaternityLeaveEndDate` | Ném lỗi nghiệp vụ |
| TC-14 | Ghi nhận quay lại làm việc | Unit | `ActualReturnDate` hợp lệ sau `MaternityLeaveEndDate` | `Status` chuyển `ReturnedToWork` |
| TC-15 | Không tạo PayrollSlipLine từ doanh nghiệp trong thời gian nghỉ (BR-07) | Integration | `MaternityCase.Status = OnMaternityLeave` trong kỳ lương | Không phát sinh dòng thu nhập tương ứng, trừ khi có chính sách hỗ trợ riêng |

### 3.2 Gợi ý chữ ký class/method

```csharp
public interface IMaternityCaseService
{
    Task<MaternityCase> RegisterAsync(Guid employeeId, DateOnly expectedBirthDate, CancellationToken ct); // BR-01
    Task<MaternityEligibilityResult> EvaluateEligibilityAsync(Guid maternityCaseId, CancellationToken ct); // BR-04
    Task StartMaternityLeaveAsync(Guid maternityCaseId, DateOnly startDate, CancellationToken ct); // BR-05, BR-08
    Task RegisterActualReturnAsync(Guid maternityCaseId, DateOnly actualReturnDate, CancellationToken ct);
    Task StartChildcareLeaveAsync(Guid maternityCaseId, DateOnly startDate, DateOnly endDate, CancellationToken ct); // BR-10
}

public sealed record MaternityEligibilityResult(bool IsEligible, DateOnly? Enough6MonthsDate, string? DenialReason);

public interface IMaternityOvertimeGuard
{
    // BR-02: kiểm tra trước khi Request & Approval (05) cho phép tạo OT/Business Trip request
    Task<bool> IsOvertimeAllowedAsync(Guid employeeId, DateOnly requestDate, CancellationToken ct);
}

public interface IMaternityLeaveAutoRequestFactory
{
    // BR-08: sinh Req_LeaveRequest đã duyệt sẵn khi MaternityLeaveStarted
    Task<Guid> CreateApprovedLeaveRequestAsync(
        Guid maternityCaseId, Guid employeeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct);
}

public sealed class MaternityCaseValidator : AbstractValidator<MaternityCase>
{
    // BR-01 (giới tính), BR-05 (giới hạn nghỉ trước sinh 2 tháng), BR-10 (không chồng lấn childcare leave)
}
```

### 3.3 Edge case / boundary value cần lưu ý

- Biên chính xác `NoOTReminderDate` — chặn từ ngày đó hay từ ngày sau đó (Data Gap, TC-05).
- Biên chính xác "đủ 6 tháng" đóng BHXH trong 12 tháng trước sinh — có tính tháng đủ 30 ngày hay theo số kỳ đóng (TC-06/07/08, cần xác nhận thêm nghiệp vụ).
- Trường hợp đa thai (sinh đôi/sinh ba) — chưa có field `ChildCount` trong entity gốc, đánh dấu **Data Gap** (BR-06).
- Nhân viên chấm dứt hợp đồng trong thời gian đang nghỉ thai sản — cần xác định rule ưu tiên giữa Personnel Decisions (11, quyết định thôi việc) và Maternity Benefits (luật có bảo vệ lao động nữ mang thai/nuôi con nhỏ khỏi bị đơn phương chấm dứt hợp đồng trong nhiều trường hợp) — đánh dấu **Data Gap quan trọng**, cần bổ sung Business Rule tích hợp giữa 2 module này khi generate code.
- `ActualBirthDate` khác biệt lớn so với `ExpectedBirthDate` (sinh non/sinh muộn) — cần cho phép cập nhật lại `MaternityLeaveStartDate/EndDate` sau khi có `ActualBirthDate` chính thức.
- Ca thai sản bị huỷ giữa chừng (sảy thai, thai lưu) — chưa có trạng thái tương ứng trong `Status` enum đề xuất, đánh dấu **Data Gap** cần bổ sung state (VD: `Terminated`) nếu nghiệp vụ yêu cầu theo dõi trường hợp này.
