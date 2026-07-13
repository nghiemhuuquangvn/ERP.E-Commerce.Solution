# 08 — Personal Income Tax (Thuế Thu nhập Cá nhân — TNCN)

> Bounded Context: **Personal Income Tax (PIT)**. Prefix bảng DB: `Tax_`. Tài liệu tuân thủ quy ước đặt tên và Ubiquitous Language định nghĩa tại [`00-domain-overview.md`](./00-domain-overview.md). Nguồn khảo sát: `HRMF2270BL.CaculatorTax` (tính thuế theo kỳ), `HT0332` (biểu thuế luỹ tiến), `HT0005.IsTax` (khoản thu nhập chịu thuế), field thuế/người phụ thuộc trên Employee (`PersonalTaxID`, `TaxID/TaxDate/TaxAddress`, `Dependents`).

## Phần 1 — DDD (Domain-Driven Design)

### 1.1 Bounded Context

Personal Income Tax chịu trách nhiệm **tính thuế thu nhập cá nhân phải khấu trừ** cho từng nhân viên theo từng kỳ (tháng), dựa trên thu nhập chịu thuế (từ Payroll 06), giảm trừ gia cảnh (người phụ thuộc, từ Employee Profile 01), và **phương pháp tính khác nhau tuỳ trạng thái hợp đồng lao động** — đúng theo quy định thuế TNCN Việt Nam: hợp đồng dưới 3 tháng chịu khấu trừ cố định 10%, hợp đồng từ 3 tháng trở lên chịu thuế luỹ tiến từng phần theo biểu thuế 7 bậc.

Bounded context này là **sibling** của Social Insurance (07) — cả hai đều downstream của Payroll (06) nhưng tính độc lập với nhau; thứ tự tính (khấu trừ bảo hiểm trước khi tính thu nhập chịu thuế) là điểm tích hợp cần thống nhất khi generate code.

### 1.2 Ubiquitous Language (bổ sung riêng cho Personal Income Tax)

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ | Giải thích |
|---|---|---|---|
| Thuế thu nhập cá nhân | Personal Income Tax (PIT) | `TNCN` | Thuế khấu trừ trên thu nhập của người lao động |
| Phương pháp tính thuế | Tax Calculation Method | `MethodID` | Cách tính thuế áp dụng — khấu trừ cố định hay luỹ tiến |
| Khấu trừ cố định | Fixed Withholding | *(suy luận từ luật, 10%)* | Áp dụng cho hợp đồng dưới 3 tháng hoặc không xác định trước thời hạn |
| Thuế luỹ tiến từng phần | Progressive Tax | `HT0332` | Áp dụng cho hợp đồng từ 3 tháng trở lên, tính theo bậc thu nhập |
| Bậc thuế | Tax Bracket / Tax Step | `TaxStepID/StepID` | Một khoảng thu nhập ứng với 1 thuế suất trong biểu thuế luỹ tiến |
| Thu nhập chịu thuế | Taxable Income | `HT0005.IsTax` | Tổng các khoản thu nhập được đánh dấu chịu thuế |
| Giảm trừ gia cảnh | Personal/Dependent Deduction | `Dependents` | Mức giảm trừ cho bản thân người nộp thuế và người phụ thuộc |
| Người phụ thuộc | Dependent | `Dependents` (trên Employee) | Người được người lao động đăng ký giảm trừ gia cảnh |
| Mã số thuế cá nhân | Personal Tax Code | `PersonalTaxID`, `TaxID` | Mã số thuế do cơ quan thuế cấp |
| Kỳ tính thuế | Tax Period | `tranMonth/tranYear` | Chu kỳ khấu trừ thuế, thường theo tháng |
| Quyết toán thuế | Tax Finalization | *(không có mã cụ thể trong khảo sát, suy luận theo luật)* | Tổng hợp/điều chỉnh thuế đã khấu trừ vào cuối năm |

### 1.3 Aggregate / Entity / Value Object

#### Aggregate Root: `PitTaxBracket` (biểu thuế luỹ tiến, mã cũ `HT0332`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính (mã cũ `TaxStepID`) |
| StepNumber | int | Có | Thứ tự bậc thuế (mã cũ `StepID`/`Orders`) |
| FromAmount | decimal(18,2) | Có | Cận dưới khoảng thu nhập tính thuế (mã cũ `FromSalary`) |
| ToAmount | decimal(18,2) | Không | Cận trên khoảng thu nhập tính thuế (mã cũ `ToSalary`, null = không giới hạn — bậc cao nhất) |
| Rate | decimal(5,2) | Có | Thuế suất áp dụng cho bậc này (mã cũ `Rate`) |
| EffectiveFrom | date | Có | Ngày biểu thuế có hiệu lực |
| EffectiveTo | date | Không | Ngày hết hiệu lực |
| DefinedBy | string(100) | Không | Người/cơ quan định nghĩa bậc thuế (mã cũ `DefinedUserID`) |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `IX_Tax_PitTaxBracket_Effective (EffectiveFrom, StepNumber)`.

> **Data Gap**: giá trị cụ thể 7 bậc thuế hiện hành (5%/10%/15%/20%/25%/30%/35% và các mốc thu nhập tương ứng) không cố định trong code mà là dữ liệu cấu hình `HT0332` — khi generate seed data, cần lấy đúng biểu thuế hiện hành theo luật tại thời điểm triển khai, không hard-code trong tài liệu này.

#### Aggregate Root: `FixedWithholdingPolicy` (khấu trừ cố định cho hợp đồng ngắn hạn)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| Rate | decimal(5,2) | Có | Tỷ lệ khấu trừ cố định (theo luật hiện hành: 10%) |
| MinContractMonthsThreshold | int | Có | Ngưỡng số tháng hợp đồng để chuyển từ khấu trừ cố định sang luỹ tiến (theo luật: 3 tháng) |
| EffectiveFrom | date | Có | Ngày chính sách có hiệu lực |
| EffectiveTo | date | Không | Ngày hết hiệu lực |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

#### Aggregate Root: `PersonalDeductionPolicy` (mức giảm trừ gia cảnh)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| SelfDeductionAmount | decimal(18,2) | Có | Mức giảm trừ cho bản thân người nộp thuế |
| DependentDeductionAmount | decimal(18,2) | Có | Mức giảm trừ cho mỗi người phụ thuộc |
| EffectiveFrom | date | Có | Ngày chính sách có hiệu lực |
| EffectiveTo | date | Không | Ngày hết hiệu lực |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

#### Aggregate Root: `PitCalculation` (kết quả tính thuế, mã cũ output của `CaculatorTax`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên |
| PayrollPeriodId | Guid (FK → `Pay_PayrollPeriod`) | Có | Kỳ lương liên quan |
| TranMonth | int | Có | Tháng kỳ tính (mã cũ `tranMonth`) |
| TranYear | int | Có | Năm kỳ tính (mã cũ `tranYear`) |
| Method | enum | Có | `FixedWithholding` \| `Progressive` (mã cũ `MethodID`) |
| WorkingStatusSnapshot | string(50) | Có | Trạng thái làm việc tại thời điểm tính (BR-01) |
| ContractStatusSnapshot | string(50) | Có | Trạng thái hợp đồng tại thời điểm tính (BR-01) |
| ContractMonthsAtCalculation | int | Có | Số tháng hợp đồng tại thời điểm tính — dùng để chọn `Method` (BR-02) |
| GrossTaxableIncome | decimal(18,2) | Có | Tổng thu nhập chịu thuế trước giảm trừ (tổng hợp từ `Pay_PayrollSlipLine.IsTaxSnapshot = true`) |
| InsuranceDeductionAmount | decimal(18,2) | Có | Khoản BHXH/BHYT/BHTN do NLĐ đóng được trừ trước khi tính thuế (tích hợp module 07) |
| SelfDeductionAmount | decimal(18,2) | Có | Giảm trừ bản thân áp dụng (snapshot `PersonalDeductionPolicy`) |
| DependentCount | int | Có | Số người phụ thuộc được tính giảm trừ tại thời điểm tính (snapshot từ Employee Profile 01) |
| DependentDeductionAmount | decimal(18,2) | Có | Tổng giảm trừ người phụ thuộc = `DependentCount × DependentDeductionAmount` |
| NetTaxableIncome | decimal(18,2) | Có | Thu nhập tính thuế sau giảm trừ (= `GrossTaxableIncome - InsuranceDeductionAmount - SelfDeductionAmount - DependentDeductionAmount`, tối thiểu 0) |
| PitTaxBracketBreakdownJson | string(max) | Không | Chi tiết số thuế theo từng bậc áp dụng (chỉ có khi `Method = Progressive`) — audit trace |
| TaxAmount | decimal(18,2) | Có | Số thuế TNCN phải khấu trừ trong kỳ |
| CalculatedAtUtc | datetime | Không | Thời điểm tính |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `UX_Tax_PitCalculation_EmployeePeriod (EmployeeId, TranMonth, TranYear)`.

#### Entity: `PitFinalization` (quyết toán thuế cuối năm — suy luận bổ sung theo luật, không có mã cũ trực tiếp)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên |
| TaxYear | int | Có | Năm quyết toán |
| TotalWithheldAmount | decimal(18,2) | Có | Tổng thuế đã khấu trừ trong năm (tổng các `PitCalculation.TaxAmount`) |
| TotalPayableAmount | decimal(18,2) | Có | Tổng thuế phải nộp theo quyết toán cuối năm |
| AdjustmentAmount | decimal(18,2) | Có | Chênh lệch (âm = hoàn thuế, dương = nộp thêm) |
| Status | enum | Có | `Draft` \| `Finalized` \| `Submitted` |
| FinalizedAtUtc | datetime | Không | Thời điểm chốt quyết toán |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

### 1.4 Domain Events

| Domain Event | Khi nào phát sinh | Payload chính |
|---|---|---|
| `PitTaxBracketConfigured` | Khai báo/sửa biểu thuế luỹ tiến | `PitTaxBracketId, StepNumber, EffectiveFrom` |
| `PersonalDeductionPolicyConfigured` | Khai báo/sửa mức giảm trừ gia cảnh | `PersonalDeductionPolicyId, EffectiveFrom` |
| `PitCalculated` | Tính xong thuế TNCN 1 nhân viên trong kỳ | `PitCalculationId, EmployeeId, PayrollPeriodId, Method, TaxAmount` |
| `PitCalculationMethodSelected` | Hệ thống chọn xong phương pháp tính (fixed/progressive) | `EmployeeId, ContractMonthsAtCalculation, Method` |
| `PitFinalized` | Chốt quyết toán thuế cuối năm | `PitFinalizationId, EmployeeId, TaxYear, AdjustmentAmount` |

### 1.5 Business Rules & Invariants

- **BR-01**: Việc tính thuế TNCN của một nhân viên trong kỳ **phải căn cứ** `WorkingStatus` và `StatusContract` (snapshot) tại thời điểm tính — tương tự BR-01 của Social Insurance (07).
- **BR-02**: Phương pháp tính thuế (`Method`) được **tự động chọn** dựa trên `ContractMonthsAtCalculation` so với `FixedWithholdingPolicy.MinContractMonthsThreshold`: nếu số tháng hợp đồng **dưới** ngưỡng (mặc định 3 tháng) → `FixedWithholding`; nếu **từ** ngưỡng trở lên → `Progressive`. Không được để người dùng chọn tuỳ ý trái với quy tắc này (trừ khi có override được ghi nhận rõ ràng qua audit).
- **BR-03**: Khi `Method = FixedWithholding`, `TaxAmount = GrossTaxableIncome × FixedWithholdingPolicy.Rate` (không áp dụng giảm trừ gia cảnh, đúng theo luật TNCN Việt Nam).
- **BR-04**: Khi `Method = Progressive`, `TaxAmount` được tính bằng cách phân bổ `NetTaxableIncome` qua từng bậc của `PitTaxBracket` theo thứ tự `StepNumber` tăng dần, mỗi phần thu nhập trong 1 bậc chịu đúng `Rate` của bậc đó (thuế luỹ tiến **từng phần**, không phải áp 1 mức thuế suất cho toàn bộ thu nhập).
- **BR-05**: `NetTaxableIncome` không được âm — nếu `GrossTaxableIncome` sau khi trừ bảo hiểm và giảm trừ gia cảnh nhỏ hơn 0 thì `NetTaxableIncome = 0` và `TaxAmount = 0`.
- **BR-06**: Chỉ những khoản thu nhập có `Pay_IncomeType.IsTax = true` (từ bounded context Payroll 06) mới được cộng vào `GrossTaxableIncome`.
- **BR-07**: `DependentCount` dùng khi tính `DependentDeductionAmount` phải là snapshot người phụ thuộc **đã được đăng ký hợp lệ** trên Employee Profile (01) tại thời điểm tính, không tính người phụ thuộc đăng ký sau kỳ tính.
- **BR-08**: Khoản BHXH/BHYT/BHTN do người lao động đóng (`InsuranceDeductionAmount`, lấy từ `Ins_InsuranceContribution` — bounded context 07) **phải được trừ trước** khi tính `NetTaxableIncome` — thứ tự tính: `Gross → trừ bảo hiểm NLĐ → trừ giảm trừ gia cảnh → NetTaxableIncome`.
- **BR-09**: `PitCalculation` phải lưu snapshot `PitTaxBracketBreakdownJson` (khi luỹ tiến) để đảm bảo có thể audit/tái tạo lại đúng số thuế đã tính, ngay cả khi biểu thuế thay đổi sau đó (tương tự nguyên tắc snapshot ở BR-06 của module 07).
- **BR-10**: `PitFinalization` cuối năm phải tổng hợp đúng tổng `TaxAmount` đã khấu trừ từng kỳ trong năm (`TotalWithheldAmount`) trước khi so sánh với số thuế phải nộp theo quyết toán chính thức.

### 1.6 Đề xuất DB Schema (tổng hợp)

```
Tax_PitTaxBracket            (Id PK, StepNumber, FromAmount, ToAmount, Rate,
                               EffectiveFrom, EffectiveTo, DefinedBy, ...)
Tax_FixedWithholdingPolicy   (Id PK, Rate, MinContractMonthsThreshold, EffectiveFrom, EffectiveTo, ...)
Tax_PersonalDeductionPolicy  (Id PK, SelfDeductionAmount, DependentDeductionAmount,
                               EffectiveFrom, EffectiveTo, ...)
Tax_PitCalculation            (Id PK, EmployeeId FK, PayrollPeriodId FK, TranMonth, TranYear, Method,
                               WorkingStatusSnapshot, ContractStatusSnapshot, ContractMonthsAtCalculation,
                               GrossTaxableIncome, InsuranceDeductionAmount, SelfDeductionAmount,
                               DependentCount, DependentDeductionAmount, NetTaxableIncome,
                               PitTaxBracketBreakdownJson, TaxAmount, CalculatedAtUtc, ...)
Tax_PitFinalization           (Id PK, EmployeeId FK, TaxYear, TotalWithheldAmount, TotalPayableAmount,
                               AdjustmentAmount, Status, FinalizedAtUtc, ...)
```

Mọi bảng nghiệp vụ đều có `IsDeleted, DeletedAtUtc, CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy` theo quy ước chung.

### 1.7 Quan hệ với Bounded Context khác

| Bounded Context | Quan hệ |
|---|---|
| Employee Profile (01) | `EmployeeId`; snapshot `PersonalTaxID`, `TaxID/TaxDate/TaxAddress`, danh sách `Dependents` |
| Labor Contract (03) | Cấp `StatusContract` và số tháng hợp đồng (`ContractMonthsAtCalculation`) — yếu tố quyết định BR-02 |
| Payroll (06) | Upstream — cấp `GrossTaxableIncome` qua `Pay_PayrollSlipLine` có `IsTaxSnapshot = true`; `PayrollPeriodId` neo kỳ tính |
| Social Insurance (07) | Sibling — cấp `InsuranceDeductionAmount` (phần NLĐ đóng) để trừ trước khi tính thuế (BR-08) |

## Phần 2 — BDD (Behavior-Driven Development)

### User Stories

- **US-01**: Là kế toán thuế, tôi muốn hệ thống tự động chọn đúng phương pháp tính thuế (khấu trừ cố định hoặc luỹ tiến) dựa trên thời hạn hợp đồng của từng nhân viên.
- **US-02**: Là kế toán thuế, tôi muốn hệ thống tính đúng số thuế luỹ tiến từng phần theo biểu thuế cấu hình, có áp dụng giảm trừ gia cảnh theo số người phụ thuộc.
- **US-03**: Là kế toán thuế, tôi muốn quyết toán thuế cuối năm tổng hợp đúng số thuế đã khấu trừ từng kỳ để xác định số thuế còn phải nộp hoặc được hoàn.

```gherkin
Feature: Chọn phương pháp tính thuế theo thời hạn hợp đồng
  Là kế toán thuế
  Tôi muốn hệ thống tự động chọn đúng phương pháp tính thuế
  Để tuân thủ đúng quy định pháp luật về khấu trừ thuế TNCN

  Background:
    Given chính sách khấu trừ cố định có Rate = 10% và MinContractMonthsThreshold = 3 tháng

  @BR-02
  Scenario Outline: Chọn phương pháp tính theo số tháng hợp đồng
    Given nhân viên "<NhanVien>" có hợp đồng lao động thời hạn <SoThang> tháng
    When hệ thống tính thuế TNCN kỳ "07/2026" cho "<NhanVien>"
    Then phương pháp tính được chọn là "<PhuongPhap>"

    Examples:
      | NhanVien | SoThang | PhuongPhap        |
      | NV001    | 1       | FixedWithholding  |
      | NV002    | 2       | FixedWithholding  |
      | NV003    | 3       | Progressive       |
      | NV004    | 12      | Progressive       |

  @BR-03
  Scenario: Tính thuế khấu trừ cố định 10% không áp dụng giảm trừ gia cảnh
    Given nhân viên "NV001" có hợp đồng 1 tháng, thuộc phương pháp "FixedWithholding"
    And GrossTaxableIncome kỳ "07/2026" của "NV001" là 8,000,000
    When hệ thống tính thuế TNCN kỳ "07/2026" cho "NV001"
    Then TaxAmount = 800,000
    And DependentDeductionAmount không được áp dụng


Feature: Tính thuế luỹ tiến từng phần
  Là kế toán thuế
  Tôi muốn hệ thống tính đúng thuế theo biểu thuế luỹ tiến 7 bậc
  Để đảm bảo mỗi phần thu nhập chịu đúng thuế suất theo bậc tương ứng

  Background:
    Given nhân viên "NV003" có hợp đồng 12 tháng, thuộc phương pháp "Progressive"
    And SelfDeductionAmount = 11,000,000 và DependentDeductionAmount mỗi người = 4,400,000
    And "NV003" đăng ký 1 người phụ thuộc hợp lệ

  @BR-04
  Scenario: Phân bổ thu nhập qua nhiều bậc thuế đúng thuế suất từng bậc
    Given GrossTaxableIncome kỳ "07/2026" của "NV003" là 30,000,000
    And InsuranceDeductionAmount (phần NLĐ đóng) của "NV003" là 1,050,000
    When hệ thống tính thuế TNCN kỳ "07/2026" cho "NV003"
    Then NetTaxableIncome = 30,000,000 - 1,050,000 - 11,000,000 - 4,400,000 = 13,550,000
    And TaxAmount được tính bằng cách phân bổ 13,550,000 qua các bậc thuế luỹ tiến theo đúng thứ tự StepNumber

  @BR-05
  Scenario: Thu nhập tính thuế âm sau giảm trừ thì thuế bằng 0
    Given GrossTaxableIncome kỳ "07/2026" của "NV003" là 12,000,000
    And InsuranceDeductionAmount của "NV003" là 500,000
    When hệ thống tính thuế TNCN kỳ "07/2026" cho "NV003"
    Then NetTaxableIncome được tính không nhỏ hơn 0
    And TaxAmount = 0

  @BR-06
  Scenario: Chỉ cộng khoản thu nhập có đánh dấu chịu thuế vào Gross
    Given "NV003" có khoản "Lương cơ bản" IsTax=true trị giá 20,000,000
    And "NV003" có khoản "Tiền ăn ca" IsTax=false trị giá 730,000
    When hệ thống tổng hợp GrossTaxableIncome kỳ "07/2026" cho "NV003"
    Then GrossTaxableIncome chỉ gồm 20,000,000, không cộng khoản "Tiền ăn ca"

  @BR-08
  Scenario: Trừ bảo hiểm NLĐ trước khi trừ giảm trừ gia cảnh
    Given GrossTaxableIncome = 30,000,000, InsuranceDeductionAmount = 1,050,000
    And SelfDeductionAmount = 11,000,000, DependentDeductionAmount = 4,400,000
    When hệ thống tính NetTaxableIncome
    Then thứ tự trừ áp dụng đúng: Gross trừ bảo hiểm trước, sau đó mới trừ giảm trừ gia cảnh
    And NetTaxableIncome = 13,550,000

  @BR-07
  Scenario: Không tính giảm trừ cho người phụ thuộc đăng ký sau kỳ tính
    Given "NV003" đăng ký thêm 1 người phụ thuộc mới có hiệu lực từ 15/07/2026
    And kỳ tính thuế là "07/2026" với ngày tính là 05/07/2026
    When hệ thống tính thuế TNCN kỳ "07/2026" cho "NV003"
    Then DependentCount dùng để tính không bao gồm người phụ thuộc mới đăng ký từ 15/07/2026


Feature: Quyết toán thuế cuối năm
  Là kế toán thuế
  Tôi muốn tổng hợp đúng số thuế đã khấu trừ trong năm để xác định số thuế còn phải nộp hoặc được hoàn

  @BR-10
  Scenario: Xác định số thuế phải nộp thêm khi quyết toán cao hơn số đã khấu trừ
    Given tổng TaxAmount đã khấu trừ 12 kỳ của "NV003" trong năm 2026 là 15,000,000
    And số thuế phải nộp theo quyết toán chính thức là 15,800,000
    When kế toán thuế chốt quyết toán năm 2026 cho "NV003"
    Then AdjustmentAmount = 800,000 (phải nộp thêm)
    And PitFinalization chuyển trạng thái "Finalized"

  @BR-10
  Scenario: Xác định số thuế được hoàn khi quyết toán thấp hơn số đã khấu trừ
    Given tổng TaxAmount đã khấu trừ 12 kỳ của "NV004" trong năm 2026 là 20,000,000
    And số thuế phải nộp theo quyết toán chính thức là 18,500,000
    When kế toán thuế chốt quyết toán năm 2026 cho "NV004"
    Then AdjustmentAmount = -1,500,000 (được hoàn thuế)
```

## Phần 3 — TDD (Test-Driven Development)

### 3.1 Bảng Test Case

| TC | Scenario tham chiếu | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | Chọn phương pháp theo số tháng hợp đồng, biên dưới ngưỡng (BR-02) | Unit (boundary) | `ContractMonths = 2` | `Method = FixedWithholding` |
| TC-02 | Chọn phương pháp đúng biên ngưỡng (BR-02) | Unit (boundary) | `ContractMonths = 3` | `Method = Progressive` |
| TC-03 | Khấu trừ cố định 10% (BR-03) | Unit | `GrossTaxableIncome = 8,000,000`, `Method = FixedWithholding` | `TaxAmount = 800,000` |
| TC-04 | Khấu trừ cố định không áp dụng giảm trừ gia cảnh (BR-03) | Unit | `Method = FixedWithholding`, `DependentCount = 2` | `DependentDeductionAmount` không ảnh hưởng `TaxAmount` |
| TC-05 | Phân bổ thuế luỹ tiến qua nhiều bậc (BR-04) | Unit | `NetTaxableIncome = 13,550,000`, biểu thuế 7 bậc chuẩn | `TaxAmount` khớp tổng thuế từng bậc theo công thức luỹ tiến |
| TC-06 | Thu nhập nằm trọn 1 bậc thấp nhất (BR-04, edge) | Unit (edge) | `NetTaxableIncome = 3,000,000`, bậc 1 (0-5tr, 5%) | `TaxAmount = 150,000` |
| TC-07 | Thu nhập đúng biên giữa 2 bậc thuế (BR-04, boundary) | Unit (boundary) | `NetTaxableIncome` đúng bằng `ToAmount` của bậc N | Phần thu nhập tại biên tính đúng thuế suất của bậc N, không lấn sang bậc N+1 |
| TC-08 | NetTaxableIncome âm sau giảm trừ → thuế = 0 (BR-05) | Unit (edge) | `Gross = 12,000,000`, giảm trừ tổng = 12,500,000 | `NetTaxableIncome = 0`, `TaxAmount = 0` |
| TC-09 | Chỉ cộng khoản IsTax=true vào Gross (BR-06) | Unit | 1 khoản IsTax=true, 1 khoản IsTax=false | `GrossTaxableIncome` chỉ gồm khoản IsTax=true |
| TC-10 | Snapshot người phụ thuộc đúng thời điểm tính (BR-07) | Integration | Người phụ thuộc mới có hiệu lực sau ngày tính | `DependentCount` không gồm người phụ thuộc mới |
| TC-11 | Thứ tự trừ bảo hiểm trước giảm trừ gia cảnh (BR-08) | Unit | `Gross`, `InsuranceDeductionAmount`, giảm trừ gia cảnh | `NetTaxableIncome` tính đúng thứ tự trừ |
| TC-12 | Snapshot breakdown bậc thuế khi tính luỹ tiến (BR-09) | Integration | `Method = Progressive` | `PitTaxBracketBreakdownJson` khác null, chứa đủ các bậc đã áp dụng |
| TC-13 | Biểu thuế thay đổi không ảnh hưởng kết quả kỳ cũ (BR-09, regression) | Integration | Đổi `PitTaxBracket` sau khi đã có `PitCalculation` kỳ cũ | Kết quả kỳ cũ không đổi |
| TC-14 | Quyết toán phải nộp thêm (BR-10) | Integration | `TotalWithheldAmount=15,000,000`, số phải nộp=15,800,000 | `AdjustmentAmount = 800,000` |
| TC-15 | Quyết toán được hoàn thuế (BR-10) | Integration | `TotalWithheldAmount=20,000,000`, số phải nộp=18,500,000 | `AdjustmentAmount = -1,500,000` |

### 3.2 Gợi ý chữ ký class/method

```csharp
public interface IPitCalculationMethodSelector
{
    // BR-02: chọn FixedWithholding hay Progressive dựa trên số tháng hợp đồng
    PitCalculationMethod Select(int contractMonthsAtCalculation, FixedWithholdingPolicy policy);
}

public interface IPitCalculator
{
    Task<PitCalculation> CalculateAsync(
        Guid employeeId, Guid payrollPeriodId, int tranMonth, int tranYear, CancellationToken ct);
}

public interface IProgressiveTaxEngine
{
    // BR-04: phân bổ NetTaxableIncome qua các bậc thuế, trả về tổng thuế + breakdown từng bậc
    ProgressiveTaxResult Calculate(decimal netTaxableIncome, IReadOnlyList<PitTaxBracket> brackets);
}

public sealed record ProgressiveTaxResult(decimal TotalTax, IReadOnlyList<TaxBracketPortion> Breakdown);
public sealed record TaxBracketPortion(int StepNumber, decimal TaxableAmountInStep, decimal Rate, decimal TaxAmount);

public interface IDependentDeductionProvider
{
    // BR-07: đọc số người phụ thuộc hợp lệ tại 1 thời điểm từ Employee Profile (01)
    Task<int> GetEligibleDependentCountAsync(Guid employeeId, DateOnly asOfDate, CancellationToken ct);
}

public interface IPitFinalizationService
{
    Task<PitFinalization> FinalizeAsync(Guid employeeId, int taxYear, decimal officialPayableAmount, CancellationToken ct);
}
```

### 3.3 Edge case / boundary value cần lưu ý

- Số tháng hợp đồng đúng bằng `MinContractMonthsThreshold` (biên 3 tháng) — phải rơi vào `Progressive`, không phải `FixedWithholding` (dùng `>=`).
- Thu nhập tính thuế nằm đúng ranh giới giữa 2 bậc thuế (`ToAmount` của bậc N == `FromAmount` của bậc N+1) — tránh tính trùng hoặc bỏ sót 1 đồng tại biên.
- `NetTaxableIncome` âm hoặc bằng 0 sau giảm trừ.
- Nhân viên không có người phụ thuộc nào (`DependentCount = 0`).
- Biểu thuế `HT0332` bị cấu hình thiếu bậc cao nhất (`ToAmount = null`) — engine phải xử lý được bậc mở (không giới hạn trên).
- Làm tròn số thuế — chưa rõ quy tắc làm tròn trong code gốc, đánh dấu **Data Gap** tương tự module 06/07, cần xác nhận thêm với nghiệp vụ trước khi generate code.
- Quyết toán cuối năm cho nhân viên có nhiều phương pháp tính khác nhau trong cùng năm (VD: đầu năm hợp đồng &lt;3 tháng dùng `FixedWithholding`, sau đó ký hợp đồng dài hạn chuyển `Progressive`) — cần đảm bảo `PitFinalization` tổng hợp đúng từ nhiều `PitCalculation` có `Method` khác nhau trong cùng `TaxYear`.
