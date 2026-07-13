# 07 — Social Insurance (Bảo hiểm Xã hội / Y tế / Thất nghiệp — BHXH/BHYT/BHTN)

> Bounded Context: **Social Insurance**. Prefix bảng DB: `Ins_`. Tài liệu tuân thủ quy ước đặt tên và Ubiquitous Language định nghĩa tại [`00-domain-overview.md`](./00-domain-overview.md). Nguồn khảo sát: `HRMF2300BL.CaculatorBHXH` (tính đóng bảo hiểm), `HRMF2190BL.InheritData/GetDataEmployee` (kế thừa đăng ký kỳ trước), `HRMT2190` (hồ sơ khai báo bảo hiểm theo kỳ), field bảo hiểm trên Employee (`SoInsuranceNo`, `HeInsuranceNo`, `HospitalID`).

## Phần 1 — DDD (Domain-Driven Design)

### 1.1 Bounded Context

Social Insurance chịu trách nhiệm **quản lý đăng ký tham gia và tính toán mức đóng** 3 loại bảo hiểm bắt buộc theo luật lao động Việt Nam — Bảo hiểm Xã hội (BHXH/Social), Bảo hiểm Y tế (BHYT/Health), Bảo hiểm Thất nghiệp (BHTN/Unemployment) — cho từng nhân viên theo từng kỳ (tháng). Đây là **downstream** của Payroll (06) — dùng một khoản lương do Payroll tính ra làm cơ sở đóng, và **upstream** của Maternity Benefits (10) — chế độ thai sản do quỹ BHXH chi trả, phụ thuộc lịch sử đóng BHXH đủ điều kiện.

Điểm cốt lõi phân biệt bounded context này: mỗi loại bảo hiểm có **2 tỷ lệ đóng độc lập** (bên người lao động và bên doanh nghiệp), và việc tính đóng phải **gate theo trạng thái làm việc/hợp đồng** của nhân viên tại thời điểm tính — không phải nhân viên nào cũng được tính đóng ở mọi thời điểm.

### 1.2 Ubiquitous Language (bổ sung riêng cho Social Insurance)

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ | Giải thích |
|---|---|---|---|
| Bảo hiểm xã hội | Social Insurance (SI) | `BHXH`, `S...` | Bảo hiểm bắt buộc, quỹ hưu trí/ốm đau/thai sản/tai nạn lao động |
| Bảo hiểm y tế | Health Insurance (HI) | `BHYT`, `H...` | Bảo hiểm khám chữa bệnh bắt buộc |
| Bảo hiểm thất nghiệp | Unemployment Insurance (UI) | `BHTN`, `T...` | Bảo hiểm trợ cấp khi mất việc |
| Tỷ lệ đóng NLĐ | Employee Contribution Rate | `SRate/HRate/TRate` | Tỷ lệ % người lao động phải đóng cho từng loại bảo hiểm |
| Tỷ lệ đóng DN | Employer Contribution Rate | `SRate2/HRate2/TRate2` | Tỷ lệ % doanh nghiệp phải đóng cho từng loại bảo hiểm |
| Lương đóng bảo hiểm | Insurance Base Salary | `BaseSalaryFieldID`, `InsuranceSalary` | Khoản lương dùng làm cơ sở tính mức đóng — có thể khác lương thực nhận |
| Hồ sơ đăng ký bảo hiểm | Insurance Enrollment | `HRMT2190` | Bản ghi khai báo tham gia bảo hiểm của 1 nhân viên trong 1 kỳ |
| Kế thừa đăng ký | Enrollment Rollover | `HRMF2190BL.InheritData` | Mang dữ liệu đăng ký từ kỳ trước sang kỳ mới, tránh khai báo lại |
| Số sổ BHXH | Social Insurance Book No. | `SoInsuranceNo` | Mã số sổ BHXH của nhân viên (thuộc Employee Profile) |
| Số thẻ BHYT | Health Insurance Card No. | `HeInsuranceNo` | Mã số thẻ BHYT của nhân viên (thuộc Employee Profile) |
| Nơi khám ban đầu | Registered Hospital | `HospitalID` | Cơ sở y tế đăng ký khám chữa bệnh ban đầu theo thẻ BHYT |
| Kỳ đóng bảo hiểm | Insurance Period | `TranMonth/TranYear` | Chu kỳ tính đóng bảo hiểm, thường theo tháng |
| Điều chỉnh chung | General Adjustment | `IsGeneralCo/GeneralCoID` | Điều chỉnh áp dụng chung cho một nhóm/toàn Division |
| Điều chỉnh vắng chung | General Absence Adjustment | `IsGeneralAbsent/GeneralAbsentID/AbsentAmount` | Điều chỉnh mức đóng khi có ngày vắng áp dụng chung |

### 1.3 Aggregate / Entity / Value Object

#### Aggregate Root: `InsuranceRateTable` (bảng tỷ lệ đóng, mã cũ tham số `SRate/HRate/TRate/SRate2/HRate2/TRate2`)

Cấu hình tỷ lệ đóng theo thời gian hiệu lực — **không hard-code tỷ lệ trong code**, vì luật có thể thay đổi tỷ lệ theo từng thời kỳ.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| DivisionId | Guid (FK) | Không | Đơn vị áp dụng (null = áp dụng toàn hệ thống) |
| EffectiveFrom | date | Có | Ngày tỷ lệ có hiệu lực |
| EffectiveTo | date | Không | Ngày hết hiệu lực (null = còn hiệu lực) |
| SocialEmployeeRate | decimal(5,2) | Có | Tỷ lệ BHXH bên NLĐ đóng (mã cũ `SRate`) |
| SocialEmployerRate | decimal(5,2) | Có | Tỷ lệ BHXH bên DN đóng (mã cũ `SRate2`) |
| HealthEmployeeRate | decimal(5,2) | Có | Tỷ lệ BHYT bên NLĐ đóng (mã cũ `HRate`) |
| HealthEmployerRate | decimal(5,2) | Có | Tỷ lệ BHYT bên DN đóng (mã cũ `HRate2`) |
| UnemploymentEmployeeRate | decimal(5,2) | Có | Tỷ lệ BHTN bên NLĐ đóng (mã cũ `TRate`) |
| UnemploymentEmployerRate | decimal(5,2) | Có | Tỷ lệ BHTN bên DN đóng (mã cũ `TRate2`) |
| BaseSalaryIncomeTypeId | Guid (FK → `Pay_IncomeType`) | Có | Khoản lương dùng làm cơ sở đóng (mã cũ `BaseSalaryFieldID`) |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `IX_Ins_InsuranceRateTable_Division (DivisionId, EffectiveFrom)`.

#### Aggregate Root: `InsuranceEnrollment` (mã cũ `HRMT2190`)

Hồ sơ đăng ký/khai báo tham gia bảo hiểm của 1 nhân viên theo kỳ.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính (mã cũ `InsurFileID`) |
| EmployeeId | Guid (FK) | Có | Tham chiếu Employee Profile (01) |
| DepartmentId | Guid (FK) | Có | Phòng ban tại thời điểm khai báo |
| TranMonth | int | Có | Tháng kỳ đóng |
| TranYear | int | Có | Năm kỳ đóng |
| Mode | enum | Có | Chế độ khai báo (mã cũ `Mode` — VD: `New`, `Adjustment`, `Rollover`) |
| SocialInsuranceBookNo | string(20) | Không | Snapshot số sổ BHXH tại thời điểm khai báo (nguồn Employee Profile) |
| HealthInsuranceCardNo | string(20) | Không | Snapshot số thẻ BHYT |
| RegisteredHospitalId | Guid (FK) | Không | Nơi khám ban đầu (mã cũ `HospitalID`) |
| IsInheritedFromPreviousPeriod | bool | Có | Đánh dấu bản ghi được sinh ra qua cơ chế kế thừa (BR-05) |
| SourceEnrollmentId | Guid (FK, tự tham chiếu) | Không | Trỏ tới bản ghi kỳ trước nếu `IsInheritedFromPreviousPeriod = true` |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `UX_Ins_InsuranceEnrollment_EmployeePeriod (EmployeeId, TranMonth, TranYear)`.

#### Aggregate Root: `InsuranceContribution` (kết quả tính đóng, mã cũ output của `CaculatorBHXH`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| InsuranceEnrollmentId | Guid (FK) | Có | Hồ sơ đăng ký liên quan |
| EmployeeId | Guid (FK) | Có | Nhân viên |
| PayrollPeriodId | Guid (FK → `Pay_PayrollPeriod`) | Có | Kỳ lương liên quan (nguồn cấp lương cơ sở) |
| InsuranceRateTableId | Guid (FK) | Có | Bảng tỷ lệ áp dụng khi tính (audit trace) |
| BaseSalaryAmount | decimal(18,2) | Có | Lương cơ sở dùng tính đóng (mã cũ input `BaseSalary`) |
| SocialEmployeeAmount | decimal(18,2) | Có | Số tiền BHXH bên NLĐ đóng |
| SocialEmployerAmount | decimal(18,2) | Có | Số tiền BHXH bên DN đóng |
| HealthEmployeeAmount | decimal(18,2) | Có | Số tiền BHYT bên NLĐ đóng |
| HealthEmployerAmount | decimal(18,2) | Có | Số tiền BHYT bên DN đóng |
| UnemploymentEmployeeAmount | decimal(18,2) | Có | Số tiền BHTN bên NLĐ đóng |
| UnemploymentEmployerAmount | decimal(18,2) | Có | Số tiền BHTN bên DN đóng |
| GeneralAdjustmentAmount | decimal(18,2) | Không | Điều chỉnh chung áp dụng (mã cũ `IsGeneralCo/GeneralCoID`) |
| GeneralAbsenceAdjustmentAmount | decimal(18,2) | Không | Điều chỉnh do vắng chung (mã cũ `IsGeneralAbsent/GeneralAbsentID/AbsentAmount`) |
| WorkingStatusSnapshot | string(50) | Có | Trạng thái làm việc tại thời điểm tính (BR-01) |
| ContractStatusSnapshot | string(50) | Có | Trạng thái hợp đồng tại thời điểm tính (BR-01) |
| IsExcludedFromCalculation | bool | Có | Đánh dấu bị loại trừ khỏi kỳ tính (BR-01) |
| ExclusionReason | string(500) | Không | Lý do loại trừ (VD "Đã chấm dứt hợp đồng trước ngày tính") |
| CalculatedAtUtc | datetime | Không | Thời điểm tính |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `UX_Ins_InsuranceContribution_EnrollmentPeriod (InsuranceEnrollmentId, PayrollPeriodId)`.

### 1.4 Domain Events

| Domain Event | Khi nào phát sinh | Payload chính |
|---|---|---|
| `InsuranceRateTableConfigured` | Khai báo/sửa bảng tỷ lệ đóng | `InsuranceRateTableId, DivisionId, EffectiveFrom` |
| `InsuranceEnrollmentCreated` | Tạo hồ sơ đăng ký mới (thủ công) | `InsuranceEnrollmentId, EmployeeId, TranMonth, TranYear` |
| `InsuranceEnrollmentInherited` | Kế thừa hồ sơ từ kỳ trước | `InsuranceEnrollmentId, SourceEnrollmentId, EmployeeId` |
| `InsuranceEnrollmentValidationFailed` | Kế thừa thất bại do dữ liệu nguồn không hợp lệ | `SourceEnrollmentId, EmployeeId, Reason` |
| `InsuranceContributionCalculated` | Tính xong mức đóng 1 nhân viên trong kỳ | `InsuranceContributionId, EmployeeId, PayrollPeriodId` |
| `InsuranceContributionExcluded` | Nhân viên bị loại trừ khỏi kỳ tính | `EmployeeId, PayrollPeriodId, ExclusionReason` |

### 1.5 Business Rules & Invariants

- **BR-01**: Việc tính đóng BHXH/BHYT/BHTN của một nhân viên trong kỳ **phải căn cứ** `WorkingStatus` và `StatusContract` (snapshot từ Employee Profile 01 / Labor Contract 03) tại thời điểm tính (`CalDate`) — nhân viên đã chấm dứt hợp đồng hoặc thuộc diện loại trừ theo cấu hình thì `IsExcludedFromCalculation = true` và không phát sinh số tiền đóng.
- **BR-02**: Mỗi loại bảo hiểm (BHXH/BHYT/BHTN) có **2 tỷ lệ đóng độc lập** (NLĐ và DN) — không được gộp chung thành 1 tỷ lệ; tổng số tiền đóng = `BaseSalaryAmount × Rate` làm tròn theo chính sách của Division.
- **BR-03**: `BaseSalaryAmount` dùng để tính đóng được lấy từ một khoản lương **cấu hình được** (`InsuranceRateTable.BaseSalaryIncomeTypeId` trỏ tới `Pay_IncomeType`), không nhất thiết là tổng thu nhập — có thể khác `InsuranceSalary` khai báo trên hồ sơ nhân viên.
- **BR-04**: Một `InsuranceEnrollment` chỉ được tạo bằng cơ chế **kế thừa** (`InheritData`) sau khi đã chạy **bước kiểm tra dữ liệu nguồn** (kỳ trước) hợp lệ (nhân viên vẫn đang làm việc, chưa có thay đổi trạng thái ảnh hưởng đăng ký) — nếu kiểm tra thất bại, hệ thống phát sinh `InsuranceEnrollmentValidationFailed` và **không** tự động tạo bản ghi kế thừa.
- **BR-05**: Một `InsuranceEnrollment` được đánh dấu `IsInheritedFromPreviousPeriod = true` phải giữ liên kết `SourceEnrollmentId` để truy vết nguồn gốc dữ liệu qua các kỳ.
- **BR-06**: `InsuranceContribution` phải lưu **snapshot** `WorkingStatusSnapshot`/`ContractStatusSnapshot`/`InsuranceRateTableId` tại thời điểm tính — thay đổi cấu hình tỷ lệ sau đó không được làm sai lệch kết quả đã tính của các kỳ trước (tương tự nguyên tắc idempotent tại Payroll BR-10).
- **BR-07**: Không được tính đóng bảo hiểm 2 lần cho cùng một `(EmployeeId, PayrollPeriodId)` — ràng buộc unique tại `InsuranceContribution`.
- **BR-08**: Điều chỉnh chung (`GeneralAdjustmentAmount`) và điều chỉnh do vắng chung (`GeneralAbsenceAdjustmentAmount`) là các khoản **cộng/trừ bổ sung** ngoài công thức tỷ lệ chuẩn, phải ghi rõ số tiền và có thể truy vết riêng biệt với số tiền tính theo tỷ lệ chuẩn.

### 1.6 Đề xuất DB Schema (tổng hợp)

```
Ins_InsuranceRateTable      (Id PK, DivisionId FK, EffectiveFrom, EffectiveTo,
                              SocialEmployeeRate, SocialEmployerRate,
                              HealthEmployeeRate, HealthEmployerRate,
                              UnemploymentEmployeeRate, UnemploymentEmployerRate,
                              BaseSalaryIncomeTypeId FK, ...)
Ins_InsuranceEnrollment     (Id PK, EmployeeId FK, DepartmentId FK, TranMonth, TranYear, Mode,
                              SocialInsuranceBookNo, HealthInsuranceCardNo, RegisteredHospitalId FK,
                              IsInheritedFromPreviousPeriod, SourceEnrollmentId FK, ...)
Ins_InsuranceContribution   (Id PK, InsuranceEnrollmentId FK, EmployeeId FK, PayrollPeriodId FK,
                              InsuranceRateTableId FK, BaseSalaryAmount,
                              SocialEmployeeAmount, SocialEmployerAmount,
                              HealthEmployeeAmount, HealthEmployerAmount,
                              UnemploymentEmployeeAmount, UnemploymentEmployerAmount,
                              GeneralAdjustmentAmount, GeneralAbsenceAdjustmentAmount,
                              WorkingStatusSnapshot, ContractStatusSnapshot,
                              IsExcludedFromCalculation, ExclusionReason, CalculatedAtUtc, ...)
```

Mọi bảng nghiệp vụ đều có `IsDeleted, DeletedAtUtc, CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy` theo quy ước chung.

### 1.7 Quan hệ với Bounded Context khác

| Bounded Context | Quan hệ |
|---|---|
| Employee Profile (01) | `EmployeeId`; snapshot `SoInsuranceNo`, `HeInsuranceNo`, `HospitalID` từ hồ sơ nhân viên |
| Labor Contract (03) | Cấp `StatusContract` làm điều kiện gate BR-01 |
| Payroll (06) | Upstream — cấp `BaseSalaryAmount` qua `Pay_PayrollSlipLine` của khoản được cấu hình làm cơ sở đóng; `PayrollPeriodId` neo kỳ tính |
| Personal Income Tax (08) | Đồng cấp (sibling) — cùng dùng lương từ Payroll làm cơ sở, nhưng tính độc lập; khoản BHXH/BHYT/BHTN do NLĐ đóng thường được trừ trước khi tính thu nhập chịu thuế (điểm tích hợp cần xác nhận thứ tự tính khi generate code) |
| Maternity Benefits (10) | Downstream tiêu thụ — điều kiện hưởng chế độ thai sản phụ thuộc lịch sử `InsuranceContribution`/`InsuranceEnrollment` đủ 6 tháng trong 12 tháng trước sinh |

## Phần 2 — BDD (Behavior-Driven Development)

### User Stories

- **US-01**: Là kế toán bảo hiểm, tôi muốn hệ thống tự động tính số tiền đóng BHXH/BHYT/BHTN theo tỷ lệ cấu hình cho từng nhân viên hợp lệ trong kỳ.
- **US-02**: Là kế toán bảo hiểm, tôi muốn kế thừa hồ sơ đăng ký bảo hiểm từ kỳ trước sang kỳ mới để không phải khai báo lại toàn bộ mỗi tháng.
- **US-03**: Là kế toán bảo hiểm, tôi muốn hệ thống tự động loại trừ nhân viên đã chấm dứt hợp đồng khỏi kỳ tính đóng.

```gherkin
Feature: Tính đóng bảo hiểm theo kỳ
  Là kế toán bảo hiểm
  Tôi muốn hệ thống tự động tính số tiền đóng BHXH/BHYT/BHTN theo tỷ lệ cấu hình
  Để đảm bảo đúng nghĩa vụ đóng bảo hiểm bắt buộc theo luật

  Background:
    Given bảng tỷ lệ đóng "Ins_InsuranceRateTable" hiệu lực từ 01/01/2026 có:
      | Loại bảo hiểm | Tỷ lệ NLĐ | Tỷ lệ DN |
      | BHXH           | 8%        | 17.5%    |
      | BHYT           | 1.5%      | 3%       |
      | BHTN            | 1%        | 1%       |
    And khoản lương cơ sở đóng bảo hiểm là "Lương đóng BHXH" với giá trị 10,000,000

  @BR-02
  Scenario: Tính đúng 6 khoản đóng (NLĐ + DN cho từng loại bảo hiểm)
    Given nhân viên "NV001" có InsuranceEnrollment hợp lệ cho kỳ "07/2026"
    When hệ thống tính đóng bảo hiểm kỳ "07/2026" cho "NV001"
    Then SocialEmployeeAmount = 800,000 và SocialEmployerAmount = 1,750,000
    And HealthEmployeeAmount = 150,000 và HealthEmployerAmount = 300,000
    And UnemploymentEmployeeAmount = 100,000 và UnemploymentEmployerAmount = 100,000

  @BR-01
  Scenario: Loại trừ nhân viên đã chấm dứt hợp đồng trước ngày tính
    Given nhân viên "NV002" có StatusContract = "Đã chấm dứt" từ 15/07/2026
    And ngày tính đóng bảo hiểm kỳ "07/2026" là 25/07/2026
    When hệ thống tính đóng bảo hiểm kỳ "07/2026"
    Then InsuranceContribution của "NV002" có IsExcludedFromCalculation = true
    And không phát sinh số tiền đóng nào cho "NV002" trong kỳ này

  @BR-03
  Scenario: Dùng đúng khoản lương cấu hình làm cơ sở đóng thay vì tổng thu nhập
    Given nhân viên "NV003" có tổng thu nhập kỳ "07/2026" là 25,000,000
    And khoản lương cấu hình làm cơ sở đóng bảo hiểm chỉ là "Lương đóng BHXH" = 10,000,000
    When hệ thống tính đóng bảo hiểm kỳ "07/2026" cho "NV003"
    Then BaseSalaryAmount dùng để tính = 10,000,000, không phải 25,000,000

  @BR-07
  Scenario: Từ chối tính đóng trùng lặp cho cùng nhân viên trong cùng kỳ
    Given nhân viên "NV001" đã có InsuranceContribution cho kỳ "07/2026"
    When hệ thống cố tính đóng bảo hiểm lần nữa cho "NV001" kỳ "07/2026"
    Then hệ thống từ chối tạo bản ghi InsuranceContribution thứ hai
    And thông báo lỗi "Đã tồn tại kết quả tính đóng bảo hiểm cho nhân viên trong kỳ này"


Feature: Kế thừa hồ sơ đăng ký bảo hiểm từ kỳ trước
  Là kế toán bảo hiểm
  Tôi muốn kế thừa hồ sơ đăng ký từ kỳ trước sang kỳ mới
  Để tiết kiệm thời gian khai báo lại mỗi tháng

  @BR-04
  Scenario: Kế thừa thành công khi dữ liệu nguồn hợp lệ
    Given nhân viên "NV004" có InsuranceEnrollment hợp lệ ở kỳ "06/2026"
    And "NV004" vẫn đang làm việc bình thường, không có thay đổi trạng thái ảnh hưởng đăng ký
    When kế toán bảo hiểm thực hiện kế thừa đăng ký sang kỳ "07/2026"
    Then hệ thống tạo InsuranceEnrollment mới cho "NV004" kỳ "07/2026"
    And bản ghi mới có IsInheritedFromPreviousPeriod = true và SourceEnrollmentId trỏ về bản ghi kỳ "06/2026"

  @BR-04
  Scenario: Từ chối kế thừa khi dữ liệu nguồn không hợp lệ
    Given nhân viên "NV005" có InsuranceEnrollment ở kỳ "06/2026" nhưng đã chấm dứt hợp đồng ngày 20/06/2026
    When kế toán bảo hiểm thực hiện kế thừa đăng ký sang kỳ "07/2026"
    Then hệ thống không tạo InsuranceEnrollment mới cho "NV005"
    And phát sinh sự kiện InsuranceEnrollmentValidationFailed với lý do "Nhân viên đã chấm dứt hợp đồng"


Feature: Bảo toàn snapshot khi cấu hình tỷ lệ thay đổi sau khi đã tính
  Là kiểm toán nội bộ
  Tôi muốn kết quả tính đóng của kỳ đã qua không bị ảnh hưởng khi tỷ lệ đóng thay đổi sau này

  @BR-06
  Scenario: Kết quả kỳ cũ giữ nguyên khi tỷ lệ mới có hiệu lực
    Given InsuranceContribution của "NV001" kỳ "07/2026" đã tính với tỷ lệ BHXH NLĐ 8%
    When từ 01/01/2027 tỷ lệ BHXH NLĐ được cấu hình mới thành 8.5%
    Then InsuranceContribution của "NV001" kỳ "07/2026" vẫn giữ SocialEmployeeAmount tính theo tỷ lệ 8%
```

## Phần 3 — TDD (Test-Driven Development)

### 3.1 Bảng Test Case

| TC | Scenario tham chiếu | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | Tính đúng 6 khoản đóng (BR-02) | Unit | BaseSalary=10,000,000; SRate=8%/17.5%; HRate=1.5%/3%; TRate=1%/1% | 6 giá trị amount đúng công thức `Base × Rate` |
| TC-02 | Loại trừ nhân viên đã chấm dứt hợp đồng (BR-01) | Integration | StatusContract="Đã chấm dứt" từ 15/07, ngày tính 25/07 | `IsExcludedFromCalculation = true`, không có amount |
| TC-03 | Biên đúng ngày chấm dứt hợp đồng | Integration (boundary) | StatusContract chấm dứt đúng ngày tính (CalDate == ngày chấm dứt) | Cần xác nhận nghiệp vụ: tính hay loại trừ ở đúng ngày biên — đánh dấu Data Gap |
| TC-04 | Dùng đúng khoản lương cấu hình làm cơ sở (BR-03) | Unit | BaseSalaryIncomeTypeId trỏ khoản "Lương đóng BHXH" = 10,000,000, tổng thu nhập = 25,000,000 | `BaseSalaryAmount = 10,000,000` |
| TC-05 | Từ chối tính trùng lặp (BR-07) | Integration | Đã tồn tại `InsuranceContribution` cho `(NV001, 07/2026)` | Ném lỗi nghiệp vụ, không tạo bản ghi thứ 2 |
| TC-06 | Kế thừa thành công (BR-04) | Integration | Nguồn kỳ 06/2026 hợp lệ | Tạo bản ghi mới, `IsInheritedFromPreviousPeriod=true`, `SourceEnrollmentId` đúng |
| TC-07 | Từ chối kế thừa khi nguồn không hợp lệ (BR-04) | Integration | Nguồn kỳ 06/2026 có nhân viên đã chấm dứt hợp đồng | Không tạo bản ghi, phát sinh `InsuranceEnrollmentValidationFailed` |
| TC-08 | Snapshot bảo toàn kết quả cũ (BR-06) | Integration (regression) | Đổi `InsuranceRateTable` sau khi đã có `InsuranceContribution` kỳ cũ | Kết quả kỳ cũ không đổi |
| TC-09 | Điều chỉnh chung cộng/trừ đúng (BR-08) | Unit | `GeneralAdjustmentAmount = -50,000` | Số tiền cuối cùng phản ánh đúng điều chỉnh, tách biệt với amount theo tỷ lệ chuẩn |
| TC-10 | Tỷ lệ = 0% (edge case cấu hình đặc biệt) | Unit (edge) | `UnemploymentEmployeeRate = 0` | `UnemploymentEmployeeAmount = 0`, không lỗi |
| TC-11 | BaseSalaryAmount = 0 (nhân viên không có lương kỳ đó) | Unit (edge) | `BaseSalaryAmount = 0` | Tất cả amount = 0, không chia cho 0 |

### 3.2 Gợi ý chữ ký class/method

```csharp
public interface IInsuranceContributionCalculator
{
    Task<InsuranceContribution> CalculateAsync(
        Guid employeeId, Guid payrollPeriodId, DateOnly calculationDate, CancellationToken ct);
}

public interface IInsuranceEligibilityGate
{
    // BR-01: quyết định nhân viên có bị loại trừ khỏi kỳ tính hay không
    Task<InsuranceEligibilityResult> CheckAsync(
        Guid employeeId, DateOnly calculationDate, CancellationToken ct);
}

public sealed record InsuranceEligibilityResult(bool IsEligible, string? ExclusionReason);

public interface IInsuranceEnrollmentRolloverService
{
    // BR-04/BR-05: kế thừa đăng ký từ kỳ trước, trả về lỗi nếu dữ liệu nguồn không hợp lệ
    Task<InsuranceEnrollmentRolloverResult> RolloverAsync(
        Guid employeeId, int sourceTranMonth, int sourceTranYear,
        int targetTranMonth, int targetTranYear, CancellationToken ct);
}

public interface IInsuranceRateTableRepository
{
    Task<InsuranceRateTable?> GetEffectiveAsync(Guid? divisionId, DateOnly asOfDate, CancellationToken ct);
}
```

### 3.3 Edge case / boundary value cần lưu ý

- Ngày chấm dứt hợp đồng trùng đúng ngày tính đóng (`CalDate`) — cần xác nhận nghiệp vụ có tính hay không (Data Gap, xem TC-03).
- Tỷ lệ đóng = 0% (một số nhóm đối tượng được miễn 1 trong 3 loại bảo hiểm).
- Nhân viên có nhiều `InsuranceRateTable` hiệu lực chồng lấn do lỗi cấu hình — cần ràng buộc unique theo `(DivisionId, EffectiveFrom)` không overlap.
- Kế thừa liên tiếp nhiều kỳ (`SourceEnrollmentId` trỏ chuỗi dài) — cần giới hạn độ sâu truy vết hoặc luôn trỏ về bản gốc đầu tiên để tránh chuỗi quá dài khi truy vấn.
- Làm tròn số tiền đóng bảo hiểm — chưa có quy tắc làm tròn rõ ràng trong code gốc, đánh dấu **Data Gap** cần xác nhận thêm (làm tròn đến đồng, hay giữ nguyên số thập phân).
