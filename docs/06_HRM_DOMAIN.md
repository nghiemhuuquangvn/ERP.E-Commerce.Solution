# Phân Hệ Nhân Sự & Chấm Công (HRM Domain)

## 1. Triết Lý Thiết Kế
- **Operational HRM:** Quản lý toàn bộ vòng đời của nhân sự từ lúc onboard đến khi offboard.
- **Decoupled Identity Link:** HRM không chứa logic đăng nhập (như mật khẩu, JWT token), cấu trúc bảo mật đó nằm ở `05_B_REUSABLE_IDENTITY_DOMAIN`. HRM chỉ giữ thông tin vận hành công việc và map với Identity qua `UserId`.

## 2. Các Thực Thể Cốt Lõi (Core Entities & Value Objects)
### 2.1. Aggregate Root: `Employee`
- `Id`: `Guid`
- `EmployeeCode`: `String` (Mã nhân viên, ví dụ: `EMP-0092`)
- `UserId`: `Guid?` (ID tài khoản liên kết với Identity Service để đăng nhập)
- `FirstName`: `String`
- `LastName`: `String`
- `DepartmentId`: `Guid`
- `JobTitle`: `String`
- `Status`: `EmployeeStatus` (`Probation`, `Active`, `Suspended`, `Terminated`)

### 2.2. Aggregate Root: `EmployeeContract` (Hợp đồng lao động)
- `Id`: `Guid`
- `EmployeeId`: `Guid`
- `BaseSalary`: `Decimal` (Lương cơ bản làm căn cứ tính ngày công)
- `Allowances`: `Decimal` (Các khoản phụ cấp cố định)
- `EffectiveDate`: `DateTime`
- `ExpiryDate`: `DateTime?`

### 2.3. Aggregate Root: `TimeSheet` (Bảng chấm công tổng hợp tháng)
- `Id`: `Guid`
- `EmployeeId`: `Guid`
- `Month`: `Int`
- `Year`: `Int`
- `TotalWorkDays`: `Decimal` (Tổng số ngày công đi làm thực tế)
- `TotalPaidLeaveDays`: `Decimal` (Số ngày nghỉ phép được hưởng lương)
- `RawCheckInLogs`: `List<CheckInLogVO>` (Value Object chứa danh sách quét vân tay/khuôn mặt hàng ngày)

### 2.4. Aggregate Root: `Payroll` (Phiếu tính lương tháng)
- `Id`: `Guid`
- `TimeSheetId`: `Guid`
- `FinalGrossSalary`: `Decimal`
- `TaxDeduction`: `Decimal` (Thuế TNCN khấu trừ)
- `InsuranceDeduction`: `Decimal`
- `NetPayout`: `Decimal` (Số tiền thực nhận chuyển khoản)
- `IsPaid`: `Bool`

## 3. Các Sự Kiện Tích Hợp (Integration Events)
- `EmployeeOnboardedEvent`: Phát ra khi nhân sự mới ký hợp đồng thành công $\rightarrow$ Identity Service nghe sự kiện này để tự động cấp tài khoản, email công ty, và gán các Role mặc định theo phòng ban (`DepartmentId`).
- `LeaveRequestApprovedEvent`: Phát ra khi đơn xin nghỉ phép được duyệt qua Work Management Service $\rightarrow$ Tự động ghi nhận ngày nghỉ hợp lệ vào `TimeSheet` của nhân viên đó.