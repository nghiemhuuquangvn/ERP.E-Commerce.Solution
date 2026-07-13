# 06 — Payroll (Tính lương)

> Bounded Context: **Payroll**. Prefix bảng DB: `Pay_`. Tài liệu tuân thủ quy ước đặt tên và Ubiquitous Language định nghĩa tại [`00-domain-overview.md`](./00-domain-overview.md). Nguồn khảo sát: `HRMF1210` (cấu hình công thức lương), `HRMF108x` (danh mục khoản thu nhập), `HF0390` (cấu hình kỳ lương/chấm công theo Division), `HP1407` (cửa sổ hiệu lực trạng thái nhân viên), `HT0005` (danh mục thành phần lương), `HRMF2290/2291` (xử lý/huỷ tính lương), `HRMF2160` (tính hoa hồng), `HRMF30xx` (báo cáo — Read Model).

## Phần 1 — DDD (Domain-Driven Design)

### 1.1 Bounded Context

Payroll chịu trách nhiệm **tính toán và lưu vết kết quả lương** của nhân viên theo từng kỳ (thường theo tháng), dựa trên: dữ liệu công từ Timekeeping (04), trạng thái hợp đồng từ Labor Contract (03), trạng thái làm việc từ Employee Profile (01), và **công thức lương được cấu hình động** (không hard-code). Payroll là **upstream** của Social Insurance (07) và Personal Income Tax (08) — cả hai đều dùng một khoản lương do Payroll tính ra làm cơ sở tính đóng bảo hiểm/thuế.

Điểm cốt lõi phân biệt Payroll với các hệ thống lương đơn giản: **công thức tính từng khoản thu nhập là dữ liệu cấu hình (`Pay_PayrollFormula`), không phải cột cứng trong code**. Khi luật thay đổi hoặc doanh nghiệp thêm khoản phụ cấp mới, chỉ cần khai báo công thức mới, không cần sửa code/schema.

### 1.2 Ubiquitous Language (bổ sung riêng cho Payroll)

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ | Giải thích |
|---|---|---|---|
| Phương pháp tính lương | Payroll Method | `PayrollMethodID` | Nhóm quy tắc tính lương áp dụng cho 1 nhóm nhân viên (VD: lương sản xuất, lương văn phòng) |
| Khoản thu nhập | Income Type | `HT0005 IncomeID/SubID` | Một dòng thành phần trong phiếu lương (lương cơ bản, phụ cấp, thưởng...) |
| Công thức lương | Payroll Formula | `HRMF1210.UpdateConditionCode` | Biểu thức tính giá trị 1 Income Type, xây dựng động từ các biến |
| Biến công thức | Formula Variable | `NC, HSC, LCB, PB, HD, GT, TC, TS, SC` | Ký hiệu biến dùng trong biểu thức (xem 1.4) |
| Kỳ lương | Payroll Period | `TranMonth/TranYear` | Chu kỳ tính lương, gắn với 1 Division |
| Phiếu lương | Payroll Slip | *(không có mã riêng, dữ liệu tổng hợp)* | Kết quả tính lương của 1 nhân viên trong 1 kỳ |
| Cấu hình kỳ lương/chấm công | Payroll Policy Configuration | `HF0390` | Cấu hình theo Division: phương pháp lương, tài khoản kế toán, lương tối thiểu/tối đa, độ chính xác số — **chỉ phần liên quan tính lương**, phần hạn mức đơn/OT xem `05-request-approval.md` |
| Hoa hồng | Commission | `HRMF2160` | Khoản thu nhập tính riêng, bị khoá khi lương kỳ đã tính |
| Xử lý tính lương | Payroll Calculation (batch) | `HRMF2290` | Tác vụ batch tính lương hàng loạt cho 1 kỳ |
| Huỷ tính lương | Payroll Cancellation | `HRMF2291` | Huỷ kết quả tính để tính lại |
| Cửa sổ hiệu lực trạng thái NV | Employee Status Period | `HP1407` | Khoảng thời gian nhân viên có 1 trạng thái làm việc cụ thể, dùng để tính lương đúng theo trạng thái tại từng thời điểm |

### 1.3 Aggregate / Entity / Value Object

#### Aggregate Root: `PayrollMethod`

Nhóm quy tắc lương áp dụng cho một nhóm nhân viên/Division.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| Code | string(20) | Có | Mã phương pháp lương (mã cũ `PayrollMethodID`) |
| Name | string(200) | Có | Tên phương pháp lương |
| Description | string(500) | Không | Mô tả |
| IsActive | bool | Có | Đang sử dụng |
| IsDeleted | bool | Có | Soft delete |
| DeletedAtUtc | datetime | Không | Thời điểm xoá |
| CreatedAtUtc / CreatedBy | datetime / string | Có | Audit |
| LastModifiedAtUtc / LastModifiedBy | datetime / string | Không | Audit |

#### Aggregate Root: `IncomeType` (danh mục khoản thu nhập)

Master data khoản thu nhập/phụ cấp — nguồn cấp dữ liệu cho công thức lương (mã cũ `HT0005`, series `HRMF108x`).

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| Code | string(20) | Có | Mã khoản thu nhập (mã cũ `IncomeID`) |
| SubCode | string(20) | Không | Mã phụ (mã cũ `SubID`, dùng khi 1 khoản có nhiều biến thể) |
| Caption | string(200) | Có | Tên hiển thị (mã cũ `Caption`) |
| IsTax | bool | Có | Khoản này có tính vào thu nhập chịu thuế TNCN hay không (mã cũ `IsTax`) — dùng bởi bounded context `PersonalIncomeTax` (08) |
| IsTransfer | bool | Có | Khoản này có chi trả qua chuyển khoản hay không (mã cũ `IsTranfer`) |
| IsUsed | bool | Có | Đang sử dụng (mã cũ `IsUsed`) |
| DisplayOrder | int | Không | Thứ tự hiển thị trên phiếu lương |
| IsDeleted / DeletedAtUtc | bool / datetime | Có/Không | Soft delete |
| CreatedAtUtc / CreatedBy / LastModifiedAtUtc / LastModifiedBy | | Có/Không | Audit |

#### Value Object (reference lookup): `PayrollFormulaVariable`

Danh sách biến hợp lệ dùng trong biểu thức công thức — **không hard-code trong code**, khai báo tường minh để engine kiểm tra biểu thức hợp lệ (BR-09).

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| Code | string(10) | Có | Ký hiệu biến — VD `NC`, `HSC`, `LCB`, `PB`, `HD`, `GT`, `TC`, `TS`, `SC` |
| Name | string(200) | Có | Tên đầy đủ, VD "Ngày công", "Hệ số cấp bậc" |
| SourceDescription | string(500) | Không | Mô tả nguồn dữ liệu cấp giá trị biến (VD: NC lấy từ Timekeeping context, HSC lấy từ Labor Contract/Employee) |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Ý nghĩa các biến chuẩn (từ khảo sát `HRMF1210`):

| Mã biến | Ý nghĩa | Nguồn dữ liệu (bounded context) |
|---|---|---|
| `NC` | Ngày công (working days) | Timekeeping & Shift (04) |
| `HSC` | Hệ số cấp bậc (grade coefficient) | Labor Contract (03) / Employee Profile (01) |
| `LCB` | Lương cơ bản (base salary) | Labor Contract (03) |
| `PB` | Phụ cấp (allowance) | Labor Contract (03) / `IncomeType` liên quan |
| `HD` | Yếu tố liên quan hợp đồng (contract-related factor) | Labor Contract (03) |
| `GT` | Hệ số/tỷ lệ theo giới tính | Employee Profile (01) |
| `TC` | Hệ số/tỷ lệ liên quan tuổi con | Employee Profile (01) — thông tin thân nhân |
| `TS` | Hệ số/tỷ lệ liên quan thai sản | Maternity Benefits (10) |
| `SC` | Hệ số/tỷ lệ liên quan số con | Employee Profile (01) — thông tin thân nhân |

#### Aggregate Root: `PayrollFormula`

Công thức tính động cho **từng khoản thu nhập, theo từng phương pháp lương** (mã cũ `HRMF1210.UpdateConditionCode`). Đây là entity thiết kế quan trọng nhất của bounded context này.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| PayrollMethodId | Guid (FK) | Có | Tham chiếu `PayrollMethod` |
| IncomeTypeId | Guid (FK) | Có | Tham chiếu `IncomeType` — công thức áp dụng cho khoản nào |
| ConditionCode | string(4000) | Không | Điều kiện áp dụng công thức (mã cũ tham số `ConditionCode` của `UpdateConditionCode`) — VD: chỉ áp dụng khi nhân viên thuộc nhóm hợp đồng X |
| Expression | string(4000) | Có | Biểu thức tính, chỉ dùng các biến khai báo trong `PayrollFormulaVariable` (BR-01, BR-09) — VD `"NC * HSC * LCB / 26 + PB"` |
| EffectiveFrom | date | Có | Ngày công thức có hiệu lực |
| EffectiveTo | date | Không | Ngày hết hiệu lực (null = còn hiệu lực) |
| DisplayOrder | int | Không | Thứ tự tính (một số khoản phụ thuộc kết quả khoản khác) |
| IsActive | bool | Có | Đang áp dụng |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index đề xuất: `IX_Pay_PayrollFormula_MethodIncome (PayrollMethodId, IncomeTypeId, EffectiveFrom)`.

#### Aggregate Root: `PayrollPolicyConfiguration` (subset của `HF0390` liên quan tính lương)

> **Lưu ý quan trọng**: `HF0390` là 1 Aggregate cấu hình lớn dùng chung cho cả module 05 (Request quota) và module 06 (Payroll). Tài liệu này **chỉ mô hình phần field liên quan tính lương**; các field `MaxRequestDXP/DXDC/DXRN/DXBSQT/DXLTG`, `MaxBorrowLeaveYear`, `MonthAllowBorrowLeave`, `HMaxPerMonth/HMaxPerTime/TMaxPerMonth` đã được mô hình hoá tại `05-request-approval.md` — **không định nghĩa lại ở đây**, Payroll chỉ tham chiếu qua cùng `DivisionId + EffectiveFrom/EffectiveTo` khi cần biết ngày công/OT đã được duyệt.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| DivisionId | Guid (FK) | Có | Đơn vị áp dụng (mã cũ ngầm định theo Division của `HF0390`) |
| EffectiveFrom | date | Có | Hiệu lực từ |
| EffectiveTo | date | Không | Hiệu lực đến |
| PayrollMethodId | Guid (FK) | Có | Phương pháp lương mặc định của Division (mã cũ `PayrollMethodID`) |
| BankAccountId | Guid (FK) | Không | Tài khoản ngân hàng chi lương (mã cũ `BankAccountID`) |
| PayableAccountId | string(20) | Không | Tài khoản kế toán phải trả lương (mã cũ `PayableAccountID`) |
| ExpenseAccountId | string(20) | Không | Tài khoản kế toán chi phí lương (mã cũ `ExpenseAccountID`) |
| AdvanceAccountId | string(20) | Không | Tài khoản kế toán tạm ứng lương (mã cũ `AdvanceAccountID`) |
| PerInTaxAccountId | string(20) | Không | Tài khoản kế toán thuế TNCN phải nộp (mã cũ `PerInTaxID`) |
| MinSalary | decimal(18,2) | Có | Mức lương tối thiểu theo luật (mã cũ `MinSalary`) |
| MinAreaSalary | decimal(18,2) | Có | Mức lương tối thiểu vùng (mã cũ `MinAreaSalary`) |
| MaxSalary1 | decimal(18,2) | Không | Mức lương tối đa loại 1 theo luật (mã cũ `MaxSalary1`) |
| MaxSalary2 | decimal(18,2) | Không | Mức lương tối đa loại 2 theo luật (mã cũ `MaxSalary2`) |
| CoefficientDecimalPlaces | int | Có | Số chữ số thập phân làm tròn cho hệ số (mã cũ độ chính xác hệ số) |
| WorkdayDecimalPlaces | int | Có | Số chữ số thập phân làm tròn cho ngày công |
| UnitPriceDecimalPlaces | int | Có | Số chữ số thập phân làm tròn cho đơn giá |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `UX_Pay_PayrollPolicyConfiguration_Division (DivisionId, EffectiveFrom)`.

#### Aggregate Root: `PayrollPeriod` (batch xử lý tính lương, mã cũ `HRMF2290/2291`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| DivisionId | Guid (FK) | Có | Đơn vị tính lương |
| TranMonth | int | Có | Tháng của kỳ lương |
| TranYear | int | Có | Năm của kỳ lương |
| PayrollMethodId | Guid (FK) | Có | Phương pháp lương áp dụng cho kỳ |
| PayrollPolicyConfigurationId | Guid (FK) | Có | Cấu hình áp dụng (snapshot tham chiếu) |
| Status | enum | Có | `Open` \| `Calculating` \| `Calculated` \| `Locked` \| `Cancelled` |
| CalculatedAtUtc | datetime | Không | Thời điểm tính lương thành công |
| CalculatedBy | string | Không | Người thực hiện |
| CancelledAtUtc / CancelledBy / CancelReason | datetime / string / string | Không | Thông tin huỷ (BR-05) |
| LockedAtUtc | datetime | Không | Thời điểm khoá kỳ (đã trả lương/đã ghi sổ kế toán — không cho huỷ nữa) |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `UX_Pay_PayrollPeriod_DivisionPeriod (DivisionId, TranMonth, TranYear)`.

#### Aggregate Root: `PayrollSlip` (phiếu lương từng nhân viên trong kỳ)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| PayrollPeriodId | Guid (FK) | Có | Kỳ lương |
| EmployeeId | Guid (FK) | Có | Tham chiếu Employee Profile (01) |
| DepartmentId | Guid (FK) | Có | Phòng ban tại thời điểm tính |
| TeamId | Guid (FK) | Không | Tổ/nhóm tại thời điểm tính |
| WorkingStatusSnapshot | string(50) | Có | Trạng thái làm việc tại thời điểm tính (snapshot từ `Emp_EmployeeStatusPeriod`, xem 1.5) |
| StatusContractSnapshot | string(50) | Có | Trạng thái hợp đồng tại thời điểm tính (snapshot từ Labor Contract 03) |
| WorkDays (NC) | decimal(9,x) | Có | Ngày công dùng làm biến `NC`, độ chính xác theo `WorkdayDecimalPlaces` |
| GradeCoefficient (HSC) | decimal(9,x) | Không | Hệ số cấp bậc dùng làm biến `HSC` |
| BaseSalary (LCB) | decimal(18,2) | Có | Lương cơ bản dùng làm biến `LCB` |
| TotalIncome | decimal(18,2) | Có | Tổng thu nhập (tổng các `PayrollSlipLine.Amount`) |
| NetPay | decimal(18,2) | Có | Thực lãnh (sau trừ bảo hiểm/thuế — tham chiếu kết quả từ Social Insurance 07 và PIT 08) |
| Status | enum | Có | `Draft` \| `Calculated` \| `Cancelled` |
| CalculatedAtUtc | datetime | Không | Thời điểm tính |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

Index: `UX_Pay_PayrollSlip_PeriodEmployee (PayrollPeriodId, EmployeeId)`.

#### Entity con của `PayrollSlip`: `PayrollSlipLine`

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| PayrollSlipId | Guid (FK) | Có | Phiếu lương cha |
| IncomeTypeId | Guid (FK) | Có | Khoản thu nhập |
| PayrollFormulaId | Guid (FK) | Không | Công thức đã dùng để tính dòng này (audit trace) |
| Amount | decimal(18,2) | Có | Giá trị đã tính |
| IsTaxSnapshot | bool | Có | Snapshot `IncomeType.IsTax` tại thời điểm tính (đảm bảo tính đúng dù danh mục thay đổi sau đó) |
| IsTransferSnapshot | bool | Có | Snapshot `IncomeType.IsTransfer` |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

#### Aggregate Root: `Commission` (mã cũ `HRMF2160`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên hưởng hoa hồng |
| PayrollPeriodId | Guid (FK) | Có | Kỳ lương liên quan |
| Amount | decimal(18,2) | Có | Số tiền hoa hồng |
| IsLocked | bool | Có | Bị khoá khi `PayrollPeriod` liên quan chuyển `Calculated` (BR-06) |
| LockedAtUtc | datetime | Không | Thời điểm khoá |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

#### Entity: `PayrollCancellationLog`

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| PayrollPeriodId | Guid (FK) | Có | Kỳ lương bị huỷ |
| CancelledAtUtc / CancelledBy | datetime / string | Có | Người/thời điểm huỷ |
| Reason | string(500) | Có | Lý do huỷ |
| IsDeleted / DeletedAtUtc / Audit | | | Soft delete + audit |

### 1.4 Value Object bổ sung: `EmployeeStatusPeriod` (tham chiếu, không định nghĩa lại)

Mã cũ `HP1407` (`Status, EmployeeID, BeginDate/EndDate`) mô hình hoá **cửa sổ hiệu lực trạng thái nhân viên**. Về bản chất đây là dữ liệu vòng đời của Employee Profile (01) — tài liệu này **chỉ tham chiếu**, không định nghĩa bảng mới trong `Pay_`. Bảng dự kiến `Emp_EmployeeStatusPeriod (Id, EmployeeId, Status, BeginDate, EndDate, ...)` sẽ được author đầy đủ tại `01-employee-profile.md`. Payroll dùng bảng này để xác định: nhân viên có trạng thái làm việc hợp lệ vào ngày nào trong kỳ lương (BR-07).

### 1.5 Domain Events

| Domain Event | Khi nào phát sinh | Payload chính |
|---|---|---|
| `PayrollFormulaConfigured` | Khai báo/sửa công thức cho 1 Income Type | `PayrollFormulaId, PayrollMethodId, IncomeTypeId, Expression` |
| `PayrollPolicyConfigured` | Khai báo/sửa `PayrollPolicyConfiguration` cho Division | `PayrollPolicyConfigurationId, DivisionId, EffectiveFrom` |
| `PayrollPeriodOpened` | Mở kỳ lương mới | `PayrollPeriodId, DivisionId, TranMonth, TranYear` |
| `PayrollCalculationStarted` | Bắt đầu batch tính lương | `PayrollPeriodId, StartedAtUtc` |
| `PayrollSlipGenerated` | Tạo xong phiếu lương 1 nhân viên | `PayrollSlipId, PayrollPeriodId, EmployeeId, NetPay` |
| `PayrollPeriodCalculated` | Batch hoàn tất cho toàn kỳ | `PayrollPeriodId, EmployeeCount, CalculatedAtUtc` |
| `PayrollPeriodCancelled` | Huỷ kết quả tính của kỳ | `PayrollPeriodId, Reason, CancelledAtUtc` |
| `PayrollPeriodLocked` | Kỳ lương bị khoá do đã trả lương/ghi sổ | `PayrollPeriodId, LockedAtUtc` |
| `CommissionLockedByPayroll` | Hoa hồng bị khoá vì kỳ lương liên quan đã tính | `CommissionId, PayrollPeriodId` |

### 1.6 Business Rules & Invariants

- **BR-01**: Công thức tính mỗi khoản thu nhập là **dữ liệu cấu hình** (`Pay_PayrollFormula.Expression`), không hard-code cột cứng theo từng khoản lương trong schema/code.
- **BR-02**: Mỗi `IncomeType` có cờ `IsTax`/`IsTransfer` quyết định khoản đó có tính vào thu nhập chịu thuế (dùng ở bounded context Personal Income Tax) và có chi trả qua chuyển khoản hay không.
- **BR-03**: Khi tính lương, `BaseSalary` (LCB) áp dụng cho từng nhân viên không được thấp hơn `MinSalary`/`MinAreaSalary` và (nếu có cấu hình) không vượt `MaxSalary1`/`MaxSalary2` của `PayrollPolicyConfiguration` đang hiệu lực cho Division/kỳ đó.
- **BR-04**: Các hạn mức giờ OT (`HMaxPerMonth/HMaxPerTime/TMaxPerMonth`), quy tắc mượn phép, hạn mức số đơn theo loại là dữ liệu **thuộc sở hữu của Request & Approval (05)** qua `HF0390` dùng chung — Payroll chỉ tiêu thụ số ngày công/giờ OT đã được duyệt, **không định nghĩa lại các hạn mức này**.
- **BR-05**: Một `PayrollPeriod` ở trạng thái `Calculated` có thể được `Cancelled` rồi tính lại (`Recalculated`), **trừ khi** kỳ đó đã có bước xử lý sau (đã trả lương/đã ghi sổ kế toán → `Status = Locked`) — khi đó thao tác huỷ bị từ chối.
- **BR-06**: Ngay khi `PayrollPeriod` chuyển trạng thái `Calculated`, mọi bản ghi `Commission` của nhân viên thuộc kỳ đó tự động chuyển `IsLocked = true` và không được sửa/xoá cho đến khi kỳ lương bị huỷ (`Cancelled`).
- **BR-07**: Việc tính lương cho một nhân viên chỉ tính trên những ngày nhân viên có trạng thái làm việc hợp lệ (`Emp_EmployeeStatusPeriod` giao với khoảng thời gian của kỳ lương) — không tính lương cho các ngày ngoài cửa sổ hiệu lực trạng thái.
- **BR-08**: Độ chính xác thập phân (`CoefficientDecimalPlaces`, `WorkdayDecimalPlaces`, `UnitPriceDecimalPlaces`) được cấu hình theo Division và phải áp dụng **nhất quán** cho mọi lần evaluate công thức trong Division/kỳ đó.
- **BR-09**: Biểu thức `PayrollFormula.Expression`/`ConditionCode` chỉ được tham chiếu tập biến đã khai báo trong `PayrollFormulaVariable` (`NC, HSC, LCB, PB, HD, GT, TC, TS, SC`, có thể mở rộng qua cấu hình) — biểu thức tham chiếu biến không tồn tại phải bị từ chối khi lưu.
- **BR-10**: Việc tính lương phải **idempotent**: tính lại một `PayrollPeriod` với cùng dữ liệu đầu vào (công, hệ số, cấu hình) phải cho ra cùng kết quả `PayrollSlipLine.Amount` — điều kiện tiên quyết để BR-05 (huỷ + tính lại) an toàn.
- **BR-11**: Hạn mức phép năm phân biệt theo giới tính (`FirstLoaDays`/`FirstLoaDaysWoman`) thuộc sở hữu Request & Approval (05); nếu công thức lương có khoản trừ liên quan nghỉ phép vượt hạn mức, Payroll chỉ tiêu thụ kết quả đã tính sẵn, không tự suy diễn lại hạn mức.

### 1.7 Đề xuất DB Schema (tổng hợp)

```
Pay_PayrollMethod                (Id PK, Code, Name, IsActive, ...)
Pay_IncomeType                   (Id PK, Code, SubCode, Caption, IsTax, IsTransfer, IsUsed, ...)
Pay_PayrollFormulaVariable       (Id PK, Code, Name, SourceDescription, ...)
Pay_PayrollFormula               (Id PK, PayrollMethodId FK, IncomeTypeId FK, ConditionCode,
                                   Expression, EffectiveFrom, EffectiveTo, DisplayOrder, IsActive, ...)
Pay_PayrollPolicyConfiguration   (Id PK, DivisionId FK, EffectiveFrom, EffectiveTo, PayrollMethodId FK,
                                   BankAccountId, PayableAccountId, ExpenseAccountId, AdvanceAccountId,
                                   PerInTaxAccountId, MinSalary, MinAreaSalary, MaxSalary1, MaxSalary2,
                                   CoefficientDecimalPlaces, WorkdayDecimalPlaces, UnitPriceDecimalPlaces, ...)
Pay_PayrollPeriod                (Id PK, DivisionId FK, TranMonth, TranYear, PayrollMethodId FK,
                                   PayrollPolicyConfigurationId FK, Status, CalculatedAtUtc, CalculatedBy,
                                   CancelledAtUtc, CancelledBy, CancelReason, LockedAtUtc, ...)
Pay_PayrollSlip                  (Id PK, PayrollPeriodId FK, EmployeeId FK, DepartmentId FK, TeamId FK,
                                   WorkingStatusSnapshot, StatusContractSnapshot, WorkDays, GradeCoefficient,
                                   BaseSalary, TotalIncome, NetPay, Status, CalculatedAtUtc, ...)
Pay_PayrollSlipLine              (Id PK, PayrollSlipId FK, IncomeTypeId FK, PayrollFormulaId FK,
                                   Amount, IsTaxSnapshot, IsTransferSnapshot, ...)
Pay_Commission                   (Id PK, EmployeeId FK, PayrollPeriodId FK, Amount, IsLocked, LockedAtUtc, ...)
Pay_PayrollCancellationLog       (Id PK, PayrollPeriodId FK, CancelledAtUtc, CancelledBy, Reason, ...)
```

Mọi bảng nghiệp vụ đều có `IsDeleted, DeletedAtUtc, CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy` theo quy ước chung.

### 1.8 Quan hệ với Bounded Context khác

| Bounded Context | Quan hệ |
|---|---|
| Employee Profile (01) | `EmployeeId`, `DepartmentId`, `TeamId` — tham chiếu hồ sơ nhân viên; `Emp_EmployeeStatusPeriod` (mã cũ `HP1407`) cấp dữ liệu gate BR-07; biến `GT/TC/SC` lấy từ thông tin nhân thân |
| Labor Contract (03) | Cấp `StatusContract`, `HSC` (hệ số cấp bậc), `LCB` (lương cơ bản), `HD` (yếu tố hợp đồng) làm input biến công thức |
| Timekeeping & Shift (04) | Cấp `NC` (ngày công) và số giờ OT đã quy đổi làm input biến công thức |
| Request & Approval (05) | Sở hữu phần hạn mức quota (`MaxRequestDXP/DXDC/DXRN/DXBSQT/DXLTG`, `MaxBorrowLeaveYear`, `MonthAllowBorrowLeave`, `HMaxPerMonth/HMaxPerTime/TMaxPerMonth`) trong `HF0390` dùng chung — Payroll chỉ dùng phần `PayrollPolicyConfiguration` liên quan tính lương, tránh lặp field |
| Social Insurance (07) | Downstream — đọc `Pay_PayrollSlipLine.Amount` của khoản được cấu hình làm cơ sở đóng bảo hiểm (`BaseSalaryFieldId` trỏ tới `Pay_IncomeType.Id`) |
| Personal Income Tax (08) | Downstream — đọc `Pay_PayrollSlipLine` có `IsTaxSnapshot = true` để tính thu nhập chịu thuế theo kỳ |
| Maternity Benefits (10) | Cấp biến `TS` (hệ số/tỷ lệ liên quan thai sản) khi công thức có áp dụng |

## Phần 2 — BDD (Behavior-Driven Development)

### User Stories

- **US-01**: Là kế toán tiền lương, tôi muốn cấu hình công thức tính động cho từng khoản thu nhập theo phương pháp lương, để hệ thống tự tính lương mà không cần sửa code khi công thức thay đổi.
- **US-02**: Là kế toán tiền lương, tôi muốn chạy tính lương hàng loạt (batch) cho toàn bộ nhân viên hợp lệ của 1 Division trong 1 kỳ.
- **US-03**: Là kế toán tiền lương, tôi muốn huỷ và tính lại một kỳ lương khi phát hiện sai sót, miễn là kỳ đó chưa phát sinh nghiệp vụ trả lương/ghi sổ.
- **US-04**: Là kế toán hoa hồng, tôi muốn hệ thống tự động khoá số liệu hoa hồng khi kỳ lương liên quan đã tính xong, để tránh sai lệch giữa lương và hoa hồng đã trả.

```gherkin
Feature: Cấu hình công thức tính lương động
  Là kế toán tiền lương
  Tôi muốn khai báo công thức tính cho từng khoản thu nhập bằng biểu thức cấu hình được
  Để hệ thống tính lương không cần sửa code khi công thức nghiệp vụ thay đổi

  Background:
    Given phương pháp lương "PM-SX-01" đã tồn tại
    And danh sách biến công thức hợp lệ gồm "NC, HSC, LCB, PB, HD, GT, TC, TS, SC"

  @BR-01
  Scenario: Cấu hình thành công công thức cho khoản Phụ cấp ăn trưa
    Given khoản thu nhập "Phụ cấp ăn trưa" chưa có công thức thuộc phương pháp "PM-SX-01"
    When kế toán tiền lương lưu công thức "NC * 20000" cho khoản "Phụ cấp ăn trưa" thuộc "PM-SX-01"
    Then hệ thống lưu bản ghi Pay_PayrollFormula với Expression = "NC * 20000"
    And công thức được áp dụng khi tính lương của kỳ tiếp theo mà không cần thay đổi code

  @BR-09
  Scenario: Từ chối lưu công thức dùng biến không hợp lệ
    Given khoản thu nhập "Phụ cấp ăn trưa" thuộc phương pháp "PM-SX-01"
    When kế toán tiền lương lưu công thức "NC * XYZ" cho khoản "Phụ cấp ăn trưa"
    Then hệ thống từ chối lưu công thức
    And thông báo lỗi "Biến XYZ không thuộc tập biến hợp lệ"

  @BR-02
  Scenario Outline: Đánh dấu cờ chịu thuế/chuyển khoản cho khoản thu nhập
    Given khoản thu nhập "<IncomeType>" được khai báo
    When kế toán tiền lương đặt IsTax = <IsTax> và IsTransfer = <IsTransfer>
    Then Pay_IncomeType lưu đúng cờ IsTax = <IsTax> và IsTransfer = <IsTransfer>
    And bounded context Personal Income Tax sẽ dùng cờ IsTax này khi tổng hợp thu nhập chịu thuế

    Examples:
      | IncomeType        | IsTax | IsTransfer |
      | Lương cơ bản       | true  | true       |
      | Tiền ăn ca          | false | true       |
      | Phụ cấp điện thoại | false | false      |


Feature: Tính lương theo kỳ (batch process)
  Là kế toán tiền lương
  Tôi muốn chạy tính lương hàng loạt cho một kỳ lương
  Để tạo phiếu lương cho toàn bộ nhân viên hợp lệ trong Division

  Background:
    Given kỳ lương "07/2026" của Division "MKVC-01" đang ở trạng thái "Open"
    And cấu hình Pay_PayrollPolicyConfiguration của "MKVC-01" có MinAreaSalary = 4,680,000 và độ chính xác ngày công 2 chữ số thập phân

  @BR-07
  Scenario: Chỉ tính lương cho những ngày nhân viên có trạng thái làm việc hợp lệ
    Given nhân viên "NV001" có Emp_EmployeeStatusPeriod trạng thái "Đang làm việc" từ 01/07/2026 đến 20/07/2026
    And nhân viên "NV001" có Emp_EmployeeStatusPeriod trạng thái "Nghỉ việc" từ 21/07/2026
    When hệ thống chạy tính lương kỳ "07/2026"
    Then Pay_PayrollSlip của "NV001" chỉ tính công/lương trong khoảng 01/07/2026-20/07/2026

  @BR-03
  Scenario: Áp dụng mức lương tối thiểu vùng khi lương cơ bản cấu hình thấp hơn quy định
    Given nhân viên "NV002" có LCB khai báo trên hợp đồng là 4,000,000
    When hệ thống chạy tính lương kỳ "07/2026"
    Then hệ thống dùng MinAreaSalary = 4,680,000 làm LCB tối thiểu khi tính công thức cho "NV002"

  @BR-08
  Scenario: Làm tròn kết quả theo độ chính xác thập phân cấu hình của Division
    Given công thức khoản "Lương theo ngày công" là "NC * LCB / 26"
    And WorkdayDecimalPlaces của "MKVC-01" là 2
    When hệ thống tính lương cho nhân viên có NC = 22.333
    Then giá trị NC dùng trong công thức được làm tròn còn 22.33

  @BR-10
  Scenario: Tính lại kỳ lương với dữ liệu đầu vào không đổi cho ra kết quả giống hệt lần trước
    Given kỳ lương "07/2026" đã tính xong lần 1 với NetPay của "NV001" là 15,200,000
    And dữ liệu công/hệ số/cấu hình không thay đổi
    When kế toán huỷ và tính lại kỳ lương "07/2026"
    Then NetPay của "NV001" sau khi tính lại vẫn là 15,200,000


Feature: Huỷ và tính lại kỳ lương
  Là kế toán tiền lương
  Tôi muốn huỷ kết quả tính lương của một kỳ để sửa và tính lại
  Nhưng phải đảm bảo không phá vỡ dữ liệu đã có bước xử lý sau

  @BR-05
  Scenario: Huỷ thành công kỳ lương chưa trả lương và chưa ghi sổ kế toán
    Given kỳ lương "07/2026" ở trạng thái "Calculated"
    And kỳ lương "07/2026" chưa có bút toán chi trả lương và chưa ghi sổ kế toán
    When kế toán tiền lương huỷ tính lương kỳ "07/2026" với lý do "Sai công thức phụ cấp"
    Then kỳ lương "07/2026" chuyển trạng thái "Cancelled"
    And bản ghi Pay_PayrollCancellationLog được tạo với Reason = "Sai công thức phụ cấp"

  @BR-05
  Scenario: Từ chối huỷ kỳ lương đã ghi sổ kế toán
    Given kỳ lương "06/2026" ở trạng thái "Locked" vì đã ghi sổ kế toán
    When kế toán tiền lương huỷ tính lương kỳ "06/2026"
    Then hệ thống từ chối thao tác huỷ
    And thông báo lỗi "Kỳ lương đã ghi sổ kế toán, không thể huỷ"


Feature: Khoá hoa hồng khi lương kỳ tương ứng đã tính
  Là kế toán hoa hồng
  Tôi muốn hoa hồng bị khoá tự động khi lương kỳ liên quan đã tính xong
  Để tránh sai lệch giữa lương đã trả và hoa hồng

  @BR-06
  Scenario: Khoá bản ghi hoa hồng khi kỳ lương chuyển trạng thái Calculated
    Given nhân viên "NV003" có bản ghi Pay_Commission chưa khoá cho kỳ "07/2026"
    When kỳ lương "07/2026" hoàn tất tính lương (chuyển trạng thái "Calculated")
    Then bản ghi Pay_Commission của "NV003" cho kỳ "07/2026" chuyển IsLocked = true

  @BR-06
  Scenario: Từ chối sửa hoa hồng của kỳ đã khoá
    Given bản ghi Pay_Commission của "NV003" cho kỳ "07/2026" có IsLocked = true
    When kế toán hoa hồng cố sửa Amount của bản ghi đó
    Then hệ thống từ chối thao tác sửa
    And thông báo lỗi "Hoa hồng của kỳ lương đã tính, không thể sửa"


Feature: Không lặp lại cấu hình hạn mức OT/mượn phép giữa Payroll và Request & Approval
  Là kiến trúc sư dữ liệu
  Tôi muốn Payroll chỉ tiêu thụ dữ liệu hạn mức từ Request & Approval
  Để tránh sai lệch khi 2 nơi cùng lưu 1 cấu hình

  @BR-04
  Scenario: Payroll đọc số giờ OT đã duyệt thay vì tự tính hạn mức
    Given Request & Approval đã duyệt 18 giờ OT cho nhân viên "NV004" trong kỳ "07/2026" (không vượt HMaxPerMonth cấu hình tại HF0390)
    When hệ thống tính lương kỳ "07/2026" cho "NV004"
    Then Payroll dùng 18 giờ OT đã duyệt làm input cho công thức lương OT
    And Payroll không tự tính lại hoặc ghi đè hạn mức HMaxPerMonth


Feature: Áp dụng hạn mức phép theo giới tính khi tính khoản trừ lương liên quan nghỉ phép
  Là kế toán tiền lương
  Tôi muốn khoản trừ lương do nghỉ vượt hạn mức phép phản ánh đúng hạn mức riêng theo giới tính

  @BR-11
  Scenario: Áp dụng FirstLoaDaysWoman cho nhân viên nữ
    Given nhân viên nữ "NV005" có hạn mức phép năm riêng FirstLoaDaysWoman = 14 ngày (cấu hình tại Request & Approval)
    And "NV005" đã nghỉ phép 15 ngày trong năm, vượt 1 ngày so với hạn mức
    When hệ thống tính lương kỳ có phát sinh khoản trừ nghỉ vượt hạn mức
    Then Payroll dùng kết quả "vượt 1 ngày" đã được Request & Approval tính sẵn để trừ lương, không tự suy diễn lại hạn mức
```

## Phần 3 — TDD (Test-Driven Development)

### 3.1 Bảng Test Case

| TC | Scenario tham chiếu | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | Cấu hình công thức hợp lệ (BR-01) | Unit | Expression = `"NC * 20000"`, biến hợp lệ | Lưu thành công, `Pay_PayrollFormula.Expression = "NC * 20000"` |
| TC-02 | Từ chối biến không hợp lệ (BR-09) | Unit | Expression = `"NC * XYZ"` | `ValidationResult.IsValid = false`, lỗi "Biến XYZ không hợp lệ" |
| TC-03 | Biểu thức rỗng | Unit (edge) | Expression = `""` | `ValidationResult.IsValid = false`, lỗi "Expression bắt buộc" |
| TC-04 | Cờ IsTax/IsTransfer lưu đúng (BR-02) | Unit | IsTax=false, IsTransfer=true | `Pay_IncomeType` lưu đúng cờ |
| TC-05 | Gate theo cửa sổ trạng thái nhân viên (BR-07) | Integration | NV001: Đang làm việc 01/07-20/07, Nghỉ việc từ 21/07 | `Pay_PayrollSlip.WorkDays` chỉ tính trong 01/07-20/07 |
| TC-06 | Gate biên đúng ngày cuối cùng hợp lệ | Integration (boundary) | Employee status kết thúc đúng 23:59:59 ngày 20/07 | Ngày 20/07 được tính, ngày 21/07 không được tính |
| TC-07 | Áp dụng MinAreaSalary khi LCB thấp hơn (BR-03) | Unit | LCB hợp đồng = 4,000,000; MinAreaSalary = 4,680,000 | LCB dùng trong công thức = 4,680,000 |
| TC-08 | Không áp dụng MinAreaSalary khi LCB đã cao hơn (boundary) | Unit | LCB hợp đồng = 4,680,000 (bằng đúng MinAreaSalary) | LCB dùng trong công thức = 4,680,000 (không thay đổi) |
| TC-09 | Áp dụng MaxSalary1 khi LCB vượt trần | Unit (boundary) | LCB = 50,000,000; MaxSalary1 = 46,800,000 | LCB dùng trong công thức bị chặn ở MaxSalary1 |
| TC-10 | Làm tròn theo độ chính xác Division (BR-08) | Unit | NC = 22.333, WorkdayDecimalPlaces = 2 | NC dùng trong công thức = 22.33 |
| TC-11 | Tính lại cho kết quả idempotent (BR-10) | Integration (regression) | Tính kỳ 07/2026 lần 1 rồi huỷ, tính lại lần 2, dữ liệu input không đổi | `NetPay` lần 1 == `NetPay` lần 2 |
| TC-12 | Huỷ kỳ lương chưa khoá (BR-05) | Integration | `PayrollPeriod.Status = Calculated`, chưa `Locked` | `Status` chuyển `Cancelled`, tạo `Pay_PayrollCancellationLog` |
| TC-13 | Từ chối huỷ kỳ lương đã khoá (BR-05) | Integration | `PayrollPeriod.Status = Locked` | Ném lỗi nghiệp vụ, `Status` không đổi |
| TC-14 | Khoá hoa hồng khi lương kỳ tính xong (BR-06) | Integration | `Pay_Commission.IsLocked = false` trước khi kỳ tính | Sau `PayrollPeriodCalculated`, `IsLocked = true` |
| TC-15 | Từ chối sửa hoa hồng đã khoá (BR-06) | Unit | `Pay_Commission.IsLocked = true`, gọi `UpdateAmount()` | Ném `InvalidOperationException`/lỗi nghiệp vụ |
| TC-16 | Payroll không tự tính lại hạn mức OT (BR-04) | Integration | Số giờ OT đã duyệt từ Request & Approval = 18h | Payroll dùng đúng 18h, không gọi lại logic tính hạn mức HMaxPerMonth |
| TC-17 | Áp dụng FirstLoaDaysWoman cho nhân viên nữ (BR-11) | Integration | Kết quả "vượt 1 ngày" từ Request & Approval | Khoản trừ lương tương ứng 1 ngày công |
| TC-18 | Batch tính lương cho toàn Division | Integration | 500 nhân viên hợp lệ trong Division | Sinh đủ 500 `Pay_PayrollSlip`, `PayrollPeriod.Status = Calculated` |
| TC-19 | Bỏ qua nhân viên không có cửa sổ trạng thái hợp lệ trong kỳ | Integration (edge) | Nhân viên có `Emp_EmployeeStatusPeriod` kết thúc trước ngày đầu kỳ | Không sinh `Pay_PayrollSlip` cho nhân viên đó |

### 3.2 Gợi ý chữ ký class/method

```csharp
// Domain service tính công thức
public interface IPayrollFormulaEngine
{
    decimal Evaluate(string expression, PayrollFormulaContext context);
    ValidationResult ValidateExpression(string expression, IReadOnlyCollection<string> allowedVariableCodes);
}

public sealed class PayrollFormulaContext
{
    public decimal NC { get; init; }
    public decimal HSC { get; init; }
    public decimal LCB { get; init; }
    public decimal PB { get; init; }
    public decimal HD { get; init; }
    public decimal GT { get; init; }
    public decimal TC { get; init; }
    public decimal TS { get; init; }
    public decimal SC { get; init; }
}

// Domain service điều phối batch tính lương
public interface IPayrollCalculationService
{
    Task<PayrollPeriodCalculationResult> CalculateAsync(Guid payrollPeriodId, CancellationToken ct);
    Task CancelAsync(Guid payrollPeriodId, string reason, string cancelledBy, CancellationToken ct);
}

// Repository / provider
public interface IPayrollPolicyConfigurationRepository
{
    Task<PayrollPolicyConfiguration?> GetEffectiveAsync(Guid divisionId, DateOnly periodDate, CancellationToken ct);
}

public interface IEmployeeStatusPeriodProvider
{
    // Đọc cửa sổ hiệu lực trạng thái nhân viên từ bounded context Employee Profile (01)
    Task<IReadOnlyList<EmployeeStatusPeriod>> GetOverlappingAsync(
        Guid employeeId, DateOnly periodFrom, DateOnly periodTo, CancellationToken ct);
}

public interface IApprovedWorkDataProvider
{
    // Đọc dữ liệu công/OT đã duyệt từ Timekeeping (04) & Request & Approval (05) — BR-04
    Task<ApprovedWorkData> GetApprovedWorkDataAsync(Guid employeeId, Guid payrollPeriodId, CancellationToken ct);
}

// Validator
public sealed class PayrollFormulaValidator : AbstractValidator<PayrollFormula>
{
    // Kiểm tra Expression chỉ dùng biến trong PayrollFormulaVariable — BR-09
}

// Domain service khoá hoa hồng
public interface ICommissionLockService
{
    Task LockByPayrollPeriodAsync(Guid payrollPeriodId, CancellationToken ct); // BR-06
}
```

### 3.3 Edge case / boundary value cần lưu ý

- Biểu thức rỗng, biểu thức tham chiếu biến trùng lặp, biểu thức có lỗi cú pháp (chia cho 0 tiềm ẩn khi `NC = 0`).
- Nhân viên có nhiều `Emp_EmployeeStatusPeriod` gối nhau/có khoảng trống trong cùng kỳ lương (nghỉ việc rồi được nhận lại trong cùng tháng).
- `LCB` đúng bằng biên `MinAreaSalary`/`MaxSalary1` (dùng `>=`/`<=`, không dùng `>`/`<` để tránh sai lệch 1 đơn vị).
- Làm tròn số thập phân: cần thống nhất chính sách làm tròn (round-half-up vs round-half-to-even) khi generate code, vì `HRMF1210` gốc không ghi rõ — đánh dấu là **Data Gap** cần xác nhận thêm với nghiệp vụ.
- Huỷ kỳ lương khi đang có tiến trình `Calculating` khác chạy song song (race condition) — cần khoá pessimistic ở mức `PayrollPeriod`.
- Tính lại kỳ lương sau khi `PayrollFormula` đã bị sửa hiệu lực (`EffectiveFrom/EffectiveTo` thay đổi) giữa 2 lần tính — cần dùng snapshot công thức tại lần tính đầu để đảm bảo BR-10, hoặc ghi rõ nghiệp vụ chấp nhận sai lệch khi công thức thay đổi.
