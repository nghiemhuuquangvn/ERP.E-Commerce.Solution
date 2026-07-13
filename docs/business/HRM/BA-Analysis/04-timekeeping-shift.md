# 04 — Timekeeping & Shift Management — Chấm công & Ca làm việc

> Bounded Context: **Timekeeping & Shift Management**. Tham chiếu `00-domain-overview.md` để biết ubiquitous language chung, context map, quy ước đặt tên DB/code, và Shared Kernel Approval Engine (mô hình hoá đầy đủ tại `05-request-approval.md` — tài liệu này chỉ tham chiếu ngắn gọn khi nhắc tới duyệt đăng ký ca, không định nghĩa lại).

## Phần 1 — DDD

### 1.1 Bounded Context

**Timekeeping & Shift Management** quản lý ca làm việc, dữ liệu chấm công thô từ thiết bị (vân tay/thẻ), quy tắc quy đổi giờ chấm công thô thành loại công được trả lương, xử lý các trường hợp bất thường (quẹt thẻ thiếu/sai), và kết chuyển công ngày thành bảng chấm công tháng. Context này là **downstream** của `Employee Profile` (01), tương tác hai chiều với `Request & Approval` (05) (đơn nghỉ phép/OT/đổi ca/bổ sung chấm công điều chỉnh dữ liệu công), và là **upstream** của `Payroll` (06) (bảng chấm công tháng là đầu vào tính lương).

Thiết kế mới **chuẩn hoá các bảng lưới D01–D31/Date01–31 kiểu cũ** (`OOP2003`, `HRMT1260`, `HRMT1270`) thành **bảng theo dòng-ngày** (`EmployeeId + Date` là một dòng dữ liệu) thay vì 31 cột lặp lại — tránh anti-pattern cột lặp, dễ mở rộng/truy vấn/index hoá. Nếu cần hiển thị dạng lưới cho báo cáo kế thừa, dùng view PIVOT ở tầng report, không lưu trữ dạng wide-table.

### 1.2 Ubiquitous Language (bổ sung riêng cho context này)

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ | Giải thích |
|---|---|---|---|
| Ca làm việc | Work Shift | `HT1020` | Khung giờ làm việc chuẩn với các cờ nghiệp vụ |
| Ca qua đêm | Overnight Shift | `IsNextDay` | Ca có giờ kết thúc thuộc ngày lịch kế tiếp |
| Quy tắc quy đổi giờ ca | Shift Bracket Rule | `HT1021` | Bảng quy tắc chuyển khoảng giờ chấm công thô thành loại công |
| Thiết bị chấm công | Time Recorder Device | `HRMT1100` | Thiết bị vân tay/thẻ (HikVision, BioStar, Cosec) |
| Dữ liệu chấm công thô | Raw Punch Log | `HT1407/HT2406/HRMF2264` | Bản ghi quẹt thẻ vào/ra thô từ thiết bị |
| Chấm công ngày | Daily Attendance | *(suy ra từ Raw Punch Log + Shift Bracket Rule)* | Kết quả tính công đã quy đổi cho 1 nhân viên/1 ngày |
| Bảng chấm công tháng | Monthly Timesheet | `HRMF2243`, `OOP2003` | Tổng hợp công theo tháng, đầu vào cho Payroll |
| Kết chuyển công | Timesheet Conversion | `HRMF2240` | Kết chuyển công ngày → công tháng, khoá kỳ |
| Xử lý bất thường chấm công | Unusual Punch Handling | `OOP2063` | Xử lý các trường hợp quẹt thẻ thiếu/sai |
| Đăng ký ca | Shift Registration | `HRMT1260`, `HRMF1260` | Nhân viên đăng ký ca trước, duyệt qua Approval Engine |
| Loại nghỉ/vắng | Absent Type | `AbsentTypeID`, `OOP1000` | Danh mục loại nghỉ/vắng dùng trong Shift Bracket Rule |
| Loại bất thường | Unusual Type | `UnusualTypeID`, `OOP1010` | Phân loại tình huống quẹt thẻ bất thường |
| Phương án xử lý | Handle Method | `HandleMethodID`, `OOP1002` | Phương án xử lý cho từng loại bất thường |

### 1.3 Aggregate / Entity / Value Object

#### 1.3.1 Aggregate Root: `WorkShift`

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| ShiftCode | string(20) | Có | Mã ca, duy nhất |
| ShiftName | string(200) | Có | Tên ca |
| BeginTime | TimeOnly | Có | Giờ bắt đầu ca (mã cũ: `BeginTime`) |
| EndTime | TimeOnly | Có | Giờ kết thúc ca (mã cũ: `EndTime`) |
| WorkingTimeHours | decimal(5,2) | Có | Tổng số giờ làm việc chuẩn của ca (mã cũ: `WorkingTime`) |
| IsNextDay | bool | Có | Ca qua đêm — `EndTime` thuộc ngày lịch kế tiếp so với `BeginTime` |
| IsAbsentShift | bool | Có | Ca đánh dấu là ca nghỉ/vắng mặc định |
| IsAllowLeaveRequest | bool | Có | Cho phép gắn đơn nghỉ phép với ca này (mã cũ: `IsDXP`) |
| IsAllowShiftSwap | bool | Có | Cho phép đổi ca (mã cũ: `IsDXDC`) |
| IsAllowOvertimeRequest | bool | Có | Cho phép đăng ký tăng ca (mã cũ: `IsDXLTG`) |
| IsApprenticeShift | bool | Có | Ca thử việc/học việc |
| IsShiftOff | bool | Có | Ca nghỉ (không làm việc) |
| BreakFromTime | TimeOnly? | Không | Giờ bắt đầu nghỉ giữa ca |
| BreakToTime | TimeOnly? | Không | Giờ kết thúc nghỉ giữa ca |
| IsActive | bool | Có | Còn sử dụng |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.2 Entity: `ShiftBracketRule` (con của `WorkShift`)

Bảng quy tắc tính công — engine chuyển đổi khoảng giờ chấm công thô/nghỉ thành loại công được trả lương.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| ShiftId | Guid (FK) | Có | Ca áp dụng quy tắc |
| AbsentTypeId | Guid (FK) | Có | Loại nghỉ/vắng áp dụng (tham chiếu `AbsentType`) |
| FromHour | decimal(5,2) | Có | Giờ bắt đầu khung quy tắc (tính từ đầu ca) |
| ToHour | decimal(5,2) | Có | Giờ kết thúc khung quy tắc |
| ConvertUnit | decimal(5,2) | Có | Hệ số quy đổi đơn vị (VD: giờ → công) |
| DayCoefficient | decimal(5,2) | Có | Hệ số ngày công (VD: 1.0 công thường, 1.5/2.0/3.0 công tăng ca lễ/tết) |
| IsOvertime | bool | Có | Đánh dấu khung này tính là tăng ca |
| IsMealTime | bool | Có | Đánh dấu khung này là giờ ăn (không tính công) |
| IsActive | bool | Có | Còn áp dụng |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.3 Aggregate Root: `TimeRecorderDevice`

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| DeviceCode | string(50) | Có | Mã thiết bị nội bộ |
| DeviceName | string(200) | Có | Tên thiết bị |
| IPAddress | string(50) | Không | Địa chỉ IP thiết bị (mã cũ: `IPAddress`) |
| Port | int? | Không | Cổng kết nối |
| ExternalDeviceId | string(100) | Không | Mã thiết bị phía nhà cung cấp (mã cũ: `DeviceID`) |
| AccessTokenEncrypted | string(500) | Không | Token truy cập API cloud thiết bị, mã hoá khi lưu (mã cũ: `AccessToken`) |
| Brand | enum (`HikVision`, `BioStar`, `Cosec`, `Other`) | Có | Hãng thiết bị |
| IsActive | bool | Có | Đang sử dụng |
| LastSyncAtUtc | DateTime? | Không | Lần đồng bộ gần nhất |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.4 Entity: `RawPunchLog`

Dữ liệu chấm công thô, bất biến (immutable) sau khi import (BR-06).

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| CardNo | string(50) | Có | Mã thẻ/vân tay quẹt (mã cũ: `AbsentCardNo`) |
| PunchDate | DateOnly | Có | Ngày quẹt (mã cũ: `AbsentDate`) |
| PunchTime | TimeOnly | Có | Giờ quẹt (mã cũ: `AbsentTime`) |
| IOCode | enum (`In`, `Out`) | Có | Vào/ra (mã cũ: `IOCode/IOValue`) |
| MachineCode | string(50) | Không | Mã máy chấm công ghi nhận |
| TimeRecorderDeviceId | Guid? (FK) | Không | Thiết bị nguồn (nếu xác định được) |
| ObjectId | Guid? | Không | Đối tượng quẹt thẻ (thường là `EmployeeId`) |
| ObjectType | enum (`Employee`, `Other`) | Có | Loại đối tượng (mã cũ: `ObjectType`) |
| ImportBatchId | Guid | Có | Lô import (phục vụ truy vết `SqlBulkCopy`) |
| IsProcessed | bool | Có | Đã được xử lý thành `DailyAttendance` hay chưa |
| CreatedAtUtc/By | — | — | Audit (không có `LastModifiedAtUtc/By` vì bất biến) |

#### 1.3.5 Aggregate Root: `DailyAttendance`

Kết quả tính công đã quy đổi cho một nhân viên trong một ngày.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên |
| AttendanceDate | DateOnly | Có | Ngày công — với ca qua đêm, là ngày bắt đầu ca (BR-05) |
| ShiftId | Guid (FK) | Có | Ca áp dụng cho ngày này |
| CheckInTime | DateTime? | Không | Thời điểm vào (đã quy đổi từ `RawPunchLog`) |
| CheckOutTime | DateTime? | Không | Thời điểm ra |
| AttendanceStatusCode | enum/lookup | Có | Loại công kết quả (công thường/tăng ca/nghỉ có lương/vắng không phép...) |
| TotalWorkedHours | decimal(5,2) | Có | Tổng giờ công đã quy đổi theo `ShiftBracketRule` |
| SourceShiftBracketRuleId | Guid? (FK) | Không | Quy tắc quyết định kết quả (phục vụ truy vết) |
| IsLocked | bool | Có | Đã thuộc kỳ chấm công tháng đã khoá (BR-08) |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

> Unique: `(EmployeeId, AttendanceDate)`.

#### 1.3.6 Entity: `ShiftAssignment`

Phân ca cho nhân viên theo từng ngày (thay thế lưới `Date01-31` của `HRMT1270`).

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên được phân ca |
| ShiftId | Guid (FK) | Có | Ca được phân |
| AssignedDate | DateOnly | Có | Ngày áp dụng ca |
| MealFromTime | TimeOnly? | Không | Giờ bắt đầu ăn (mã cũ: `FromMealTime01-05`, chuẩn hoá còn 1 khung/dòng; nhiều khung ăn trong ngày = nhiều dòng) |
| MealToTime | TimeOnly? | Không | Giờ kết thúc ăn |
| Source | enum (`Manual`, `AutoSchedule`, `Registration`) | Có | Nguồn gốc phân ca (thủ công/tự động/từ đăng ký ca đã duyệt) |
| ShiftRegistrationId | Guid? (FK) | Không | Nếu `Source = Registration`, tham chiếu `ShiftRegistration` gốc |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

> Unique: `(EmployeeId, AssignedDate)` khi chỉ cho 1 ca chính/ngày; nếu nghiệp vụ cho phép nhiều khung ăn/ngày thì tách bảng `Att_ShiftAssignmentMeal` con.

#### 1.3.7 Aggregate Root: `ShiftRegistration`

Đăng ký ca trước, duyệt qua Approval Engine dùng chung (05).

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên đăng ký |
| ShiftId | Guid (FK) | Có | Ca đăng ký |
| RegisteredDate | DateOnly | Có | Ngày đăng ký áp dụng |
| ApprovalRequestId | Guid (FK, shared kernel) | Có | Tham chiếu `ApprovalRequest` (05-request-approval.md) — không định nghĩa lại trạng thái duyệt tại đây |
| RegistrationStatus | enum (`PendingApproval`, `Approved`, `Rejected`) | Có | Phản chiếu trạng thái tổng của `ApprovalRequest` (đồng bộ, không phải nguồn sự thật) |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.8 Entity: `UnusualPunchCase`

Xử lý trường hợp quẹt thẻ thiếu/sai (mã cũ: `OOP2063`).

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên liên quan |
| TranMonth | int | Có | Tháng phát sinh (mã cũ: `TranMonth`) |
| TranYear | int | Có | Năm phát sinh (mã cũ: `TranYear`) |
| AttendanceDate | DateOnly | Có | Ngày phát sinh bất thường |
| ShiftId | Guid (FK) | Có | Ca liên quan |
| ExpectedBeginTime | DateTime? | Không | Giờ vào ca dự kiến |
| ExpectedEndTime | DateTime? | Không | Giờ ra ca dự kiến |
| UnusualTypeId | Guid (FK) | Có | Loại bất thường (mã cũ: `JugdeUnusualType(ID)`) |
| ActualFact | string(500) | Không | Mô tả thực tế ghi nhận (mã cũ: `Fact`) |
| HandleMethodId | Guid? (FK) | Không | Phương án xử lý được chọn (mã cũ: `HandleMethod`) |
| CaseStatus | enum (`Unhandled`, `Handled`) | Có | Trạng thái xử lý |
| HandledAtUtc | DateTime? | Không | Thời điểm xử lý |
| HandledBy | string(100) | Không | Người xử lý |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.9 Lookup Entities

- **`AbsentType`** (mã cũ `OOP1000`): `Id, Code, Name, IsPaid, IsActive, IsDeleted...`
- **`HandleMethod`** (mã cũ `OOP1002`): `Id, Code, Name, IsActive, IsDeleted...`
- **`UnusualType`** (mã cũ `OOP1010`): `Id, Code, Name, DefaultHandleMethodId (FK), IsActive, IsDeleted...`

#### 1.3.10 Value Object / Config: `WorkTypeConditionMapping`

Bảng mapping điều kiện quyết định loại công (mã cũ: `HRMT2330`) — dùng bởi engine tính công khi tổ hợp nhiều điều kiện.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| AbsentTypeId | Guid? (FK) | Không | Điều kiện: loại nghỉ/vắng |
| DayTypeId | Guid? | Không | Điều kiện: loại ngày (thường/lễ/tết...) |
| ShiftId | Guid? (FK) | Không | Điều kiện: ca áp dụng |
| RequestTypeId | Guid? | Không | Điều kiện: loại đơn từ liên quan (05) |
| LeaveTypeId | Guid? | Không | Điều kiện: loại nghỉ phép liên quan |
| UnusualTypeId | Guid? (FK) | Không | Điều kiện: loại bất thường liên quan |
| ResultWorkTypeId | Guid | Có | Kết quả: loại công được gán khi tổ hợp điều kiện khớp |
| Priority | int | Có | Thứ tự ưu tiên khi nhiều rule cùng khớp |
| IsActive | bool | Có | Còn áp dụng |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

#### 1.3.11 Aggregate Root: `MonthlyTimesheet`

Kết chuyển công ngày → công tháng, khoá kỳ (mã cũ: `HRMF2240` convert, `HRMF2243` danh mục).

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid (FK) | Có | Nhân viên |
| TranMonth | int | Có | Tháng kỳ chấm công |
| TranYear | int | Có | Năm kỳ chấm công |
| IsLocked | bool | Có | Đã kết chuyển/khoá kỳ hay chưa (BR-08) |
| LockedAtUtc | DateTime? | Không | Thời điểm khoá |
| LockedBy | string(100) | Không | Người thực hiện khoá kỳ |
| IsDeleted / DeletedAtUtc | — | — | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | — | — | Audit |

> Unique: `(EmployeeId, TranMonth, TranYear)`.

#### 1.3.12 Entity: `MonthlyTimesheetDetail` (con của `MonthlyTimesheet`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| MonthlyTimesheetId | Guid (FK) | Có | Bảng chấm công tháng chứa dòng này |
| WorkTypeId | Guid | Có | Loại công (công thường/OT/nghỉ có lương...) |
| TotalValue | decimal(7,2) | Có | Tổng giá trị (giờ hoặc công) của loại công này trong tháng |

### 1.4 Domain Events

| Event | Phát sinh khi | Hệ quả |
|---|---|---|
| `RawPunchLogImported` | Hoàn tất một lô import dữ liệu thô từ thiết bị (`SqlBulkCopy`) | Kích hoạt xử lý tính `DailyAttendance` cho các bản ghi liên quan |
| `DailyAttendanceComputed` | Engine tính công quy đổi xong `RawPunchLog` + `ShiftBracketRule` thành `DailyAttendance` | Cập nhật `TotalWorkedHours`/`AttendanceStatusCode` |
| `UnusualPunchCaseDetected` | Phát hiện quẹt thẻ thiếu/sai trong ca | Tạo `UnusualPunchCase` với `CaseStatus = Unhandled` |
| `UnusualPunchCaseHandled` | Người dùng gán `HandleMethodId` và xử lý xong | `CaseStatus = Handled`, có thể trigger tính lại `DailyAttendance` liên quan |
| `ShiftRegistrationSubmitted` | Nhân viên gửi đăng ký ca | Tạo `ApprovalRequest` tương ứng trong Approval Engine (05) |
| `ShiftRegistrationApprovalResolved` | Approval Engine (05) trả kết quả duyệt | `ShiftRegistration.RegistrationStatus` đồng bộ; nếu `Approved` → tạo `ShiftAssignment` (`Source=Registration`) |
| `MonthlyTimesheetLocked` | Kết chuyển/khoá kỳ chấm công tháng | Các `DailyAttendance` thuộc kỳ chuyển `IsLocked=true`, không cho sửa (BR-08); phát sự kiện cho `Payroll` (06) lấy dữ liệu |
| `TimeRecorderDeviceSynced` | Đồng bộ thành công với thiết bị/cloud API (HikVision/BioStar/Cosec) | Cập nhật `LastSyncAtUtc` |

### 1.5 Business Rules & Invariants

- **BR-01**: `WorkingTimeHours` của `WorkShift` phải nhất quán với khoảng `(EndTime − BeginTime)` trừ đi khung nghỉ giữa ca (`BreakFromTime`–`BreakToTime`); khi `IsNextDay = true`, `EndTime` được hiểu là thuộc ngày lịch kế tiếp so với `BeginTime` khi tính khoảng thời gian.
- **BR-02**: Một `WorkShift` chỉ cho phép gắn đơn nghỉ phép nếu `IsAllowLeaveRequest = true`, đổi ca nếu `IsAllowShiftSwap = true`, đăng ký tăng ca nếu `IsAllowOvertimeRequest = true`; các cờ này là điều kiện tiên quyết khi `Request & Approval` (05) tạo đơn tham chiếu `ShiftId`.
- **BR-03**: Mỗi tổ hợp `(ShiftId, AbsentTypeId)` trong `ShiftBracketRule` không được có các khung `FromHour`–`ToHour` chồng lấn nhau; `ConvertUnit` và `DayCoefficient` quyết định cách quy đổi khoảng thời gian chấm công/nghỉ thành loại công.
- **BR-04**: Thuật toán tính tổng thời gian nghỉ/vắng nhiều tháng (`GetTotalTime` cũ) phải duyệt tuần tự **từng ngày** trong khoảng `[FromDate, ToDate]`, với mỗi ngày tra đúng ca làm việc **hiệu lực tại chính ngày đó** của nhân viên (qua `ShiftAssignment`, không dùng ca mặc định cố định cho toàn khoảng), rồi áp dụng `ShiftBracketRule` tương ứng ca đó để làm tròn giờ theo khung thời gian quy định.
- **BR-05**: Khi `ShiftId` có `IsNextDay = true`, `AttendanceDate` của `DailyAttendance` được ghi nhận theo **ngày bắt đầu ca** (ngày vào ca), dù `CheckOutTime` thực tế rơi vào ngày lịch kế tiếp.
- **BR-06**: `RawPunchLog` là dữ liệu **bất biến** sau khi import — không được sửa/xoá trực tiếp; sai lệch phải xử lý qua `UnusualPunchCase` hoặc Punch Correction Request (05).
- **BR-07**: Khi phát hiện một ca chỉ có duy nhất một trong hai lượt quẹt (chỉ vào hoặc chỉ ra), hệ thống phải tự động sinh `UnusualPunchCase` với `CaseStatus = Unhandled`; `DailyAttendance` liên quan **không được tính công chính thức** cho tới khi `HandleMethodId` được gán và case chuyển `Handled`.
- **BR-08**: Khi `MonthlyTimesheet.IsLocked = true` (đã kết chuyển/khoá kỳ), mọi `DailyAttendance` thuộc `(EmployeeId, TranMonth, TranYear)` tương ứng **không được sửa** — invariant tương tự khoá kỳ ở Payroll (06).
- **BR-09**: `ShiftRegistration` phải được duyệt qua Approval Engine (shared kernel, 05) trước khi được dùng làm căn cứ tạo `ShiftAssignment` chính thức (`Source = Registration`); không cho phép tạo `ShiftAssignment` từ đăng ký chưa duyệt (`RegistrationStatus ≠ Approved`).
- **BR-10**: Tổng giờ tăng ca tính được từ `ShiftBracketRule` (`IsOvertime = true`) là dữ liệu đầu vào cho giới hạn trần OT tháng (40h/tháng theo luật lao động VN) do `Request & Approval` (05) quản lý và kiểm tra; context này chỉ cung cấp số liệu, không tự kiểm tra trần OT.

### 1.6 Đề xuất DB Schema (prefix `Att_`)

```
Att_Shift (
    Id                      PK
    ShiftCode               UNIQUE
    ShiftName
    BeginTime
    EndTime
    WorkingTimeHours
    IsNextDay
    IsAbsentShift
    IsAllowLeaveRequest
    IsAllowShiftSwap
    IsAllowOvertimeRequest
    IsApprenticeShift
    IsShiftOff
    BreakFromTime           NULL
    BreakToTime             NULL
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_ShiftBracketRule (
    Id                      PK
    ShiftId                 FK -> Att_Shift.Id             (INDEX IX_Att_ShiftBracketRule_ShiftId)
    AbsentTypeId            FK -> Att_AbsentType.Id         (INDEX IX_Att_ShiftBracketRule_ShiftId_AbsentTypeId composite w/ ShiftId)
    FromHour
    ToHour
    ConvertUnit
    DayCoefficient
    IsOvertime
    IsMealTime
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_TimeRecorderDevice (
    Id                      PK
    DeviceCode              UNIQUE
    DeviceName
    IPAddress               NULL
    Port                    NULL
    ExternalDeviceId        NULL
    AccessTokenEncrypted    NULL
    Brand                   (enum)
    IsActive
    LastSyncAtUtc           NULL
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_RawPunchLog (
    Id                      PK
    CardNo                                                  (INDEX IX_Att_RawPunchLog_CardNo_PunchDate composite w/ PunchDate)
    PunchDate
    PunchTime
    IOCode                  (enum: In, Out)
    MachineCode             NULL
    TimeRecorderDeviceId    FK -> Att_TimeRecorderDevice.Id  NULL
    ObjectId                NULL
    ObjectType               (enum: Employee, Other)
    ImportBatchId                                            (INDEX IX_Att_RawPunchLog_ImportBatchId)
    IsProcessed
    CreatedAtUtc, CreatedBy
)

Att_DailyAttendance (
    Id                      PK
    EmployeeId              FK -> Emp_Employee.Id            (INDEX IX_Att_DailyAttendance_EmployeeId_AttendanceDate composite w/ AttendanceDate, UNIQUE)
    AttendanceDate
    ShiftId                 FK -> Att_Shift.Id
    CheckInTime             NULL
    CheckOutTime            NULL
    AttendanceStatusCode
    TotalWorkedHours
    SourceShiftBracketRuleId FK -> Att_ShiftBracketRule.Id   NULL
    IsLocked
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_ShiftAssignment (
    Id                      PK
    EmployeeId              FK -> Emp_Employee.Id            (INDEX IX_Att_ShiftAssignment_EmployeeId_AssignedDate composite w/ AssignedDate, UNIQUE)
    ShiftId                 FK -> Att_Shift.Id
    AssignedDate
    MealFromTime            NULL
    MealToTime              NULL
    Source                  (enum: Manual, AutoSchedule, Registration)
    ShiftRegistrationId     FK -> Att_ShiftRegistration.Id   NULL
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_ShiftRegistration (
    Id                      PK
    EmployeeId              FK -> Emp_Employee.Id            (INDEX IX_Att_ShiftRegistration_EmployeeId)
    ShiftId                 FK -> Att_Shift.Id
    RegisteredDate
    ApprovalWorkflowId      FK -> App_ApprovalWorkflow.Id     -- shared kernel, 05-request-approval.md
    RegistrationStatus      (enum: PendingApproval, Approved, Rejected)
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_UnusualPunchCase (
    Id                      PK
    EmployeeId              FK -> Emp_Employee.Id            (INDEX IX_Att_UnusualPunchCase_EmployeeId_TranYear_TranMonth composite)
    TranMonth
    TranYear
    AttendanceDate
    ShiftId                 FK -> Att_Shift.Id
    ExpectedBeginTime       NULL
    ExpectedEndTime         NULL
    UnusualTypeId           FK -> Att_UnusualType.Id
    ActualFact              NULL
    HandleMethodId          FK -> Att_HandleMethod.Id         NULL
    CaseStatus               (enum: Unhandled, Handled, INDEX IX_Att_UnusualPunchCase_CaseStatus)
    HandledAtUtc            NULL
    HandledBy                NULL
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_AbsentType (
    Id                      PK
    Code                    UNIQUE
    Name
    IsPaid
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_HandleMethod (
    Id                      PK
    Code                    UNIQUE
    Name
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_UnusualType (
    Id                      PK
    Code                    UNIQUE
    Name
    DefaultHandleMethodId   FK -> Att_HandleMethod.Id         NULL
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_WorkTypeConditionMapping (
    Id                      PK
    AbsentTypeId            FK -> Att_AbsentType.Id           NULL
    DayTypeId               NULL
    ShiftId                 FK -> Att_Shift.Id                NULL
    RequestTypeId           NULL
    LeaveTypeId             NULL
    UnusualTypeId           FK -> Att_UnusualType.Id          NULL
    ResultWorkTypeId
    Priority
    IsActive
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_MonthlyTimesheet (
    Id                      PK
    EmployeeId              FK -> Emp_Employee.Id            (INDEX UX_Att_MonthlyTimesheet_EmployeeId_TranYear_TranMonth composite, UNIQUE)
    TranMonth
    TranYear
    IsLocked
    LockedAtUtc             NULL
    LockedBy                NULL
    IsDeleted, DeletedAtUtc
    CreatedAtUtc, CreatedBy, LastModifiedAtUtc, LastModifiedBy
)

Att_MonthlyTimesheetDetail (
    Id                      PK
    MonthlyTimesheetId      FK -> Att_MonthlyTimesheet.Id     (INDEX IX_Att_MonthlyTimesheetDetail_MonthlyTimesheetId)
    WorkTypeId
    TotalValue
)
```

### 1.7 Quan hệ với bounded context khác

| Context | Chiều quan hệ | Nội dung |
|---|---|---|
| `Employee Profile` (01) | N-1 | `Att_DailyAttendance`, `Att_ShiftAssignment`, `Att_MonthlyTimesheet` đều tham chiếu `EmployeeId` |
| `Request & Approval` (05) | Shared Kernel + tương tác 2 chiều | Đăng ký ca (`ShiftRegistration`) dùng Approval Engine chung để duyệt; các đơn nghỉ phép/OT/ra ngoài/bổ sung chấm công/đổi ca (05) điều chỉnh trực tiếp `Att_DailyAttendance`/`Att_UnusualPunchCase`/`Att_ShiftAssignment` — chi tiết đơn từ không định nghĩa lại tại đây |
| `Payroll` (06) | Upstream → Downstream | `Att_MonthlyTimesheet`/`Att_MonthlyTimesheetDetail` (sau khi `IsLocked=true`) là đầu vào bắt buộc cho kỳ tính lương |
| Hệ thống ngoài: thiết bị chấm công (HikVision/BioStar/Cosec) | External System | `Att_TimeRecorderDevice`/`Att_RawPunchLog` phản ánh dữ liệu đồng bộ 2 chiều qua API cloud thiết bị |

## Phần 2 — BDD

### User Stories

- **US-01**: Là quản trị hệ thống, tôi muốn khai báo ca làm việc với đầy đủ thuộc tính giờ và các cờ nghiệp vụ (nghỉ phép/đổi ca/OT/ca qua đêm).
- **US-02**: Là hệ thống, tôi muốn nhập hàng loạt dữ liệu chấm công thô từ thiết bị vân tay/thẻ mà không làm mất/sai lệch dữ liệu gốc.
- **US-03**: Là hệ thống, tôi muốn tự động quy đổi dữ liệu chấm công thô thành loại công theo đúng quy tắc của ca.
- **US-04**: Là nhân viên nhân sự, tôi muốn hệ thống tự phát hiện và cho phép xử lý các trường hợp quẹt thẻ thiếu/sai.
- **US-05**: Là nhân viên, tôi muốn đăng ký ca làm việc trước và được quản lý duyệt qua quy trình chuẩn.
- **US-06**: Là kế toán lương, tôi muốn kết chuyển công ngày sang công tháng và khoá kỳ trước khi tính lương để tránh sai lệch phát sinh sau khi đã tính lương.
- **US-07**: Là hệ thống, tôi cần tính đúng ngày công và giờ công cho ca làm việc qua đêm.
- **US-08**: Là kế toán lương, tôi muốn `GetTotalTime` tính đúng tổng thời gian nghỉ/vắng khi khoảng thời gian vắt qua nhiều tháng, kể cả khi ca làm việc của nhân viên thay đổi giữa các ngày.

### Feature: Khai báo ca làm việc

```gherkin
Feature: Khai báo ca làm việc
  Là quản trị hệ thống, tôi muốn khai báo ca làm việc với các cờ nghiệp vụ chính xác

  Scenario: Khai báo ca qua đêm (BR-01)
    Given ca "Ca Đêm" có "BeginTime" = "22:00" và "EndTime" = "06:00"
    When quản trị hệ thống đánh dấu "IsNextDay" = true cho ca "Ca Đêm"
    Then hệ thống tính "WorkingTimeHours" của ca dựa trên khoảng từ "22:00" ngày N tới "06:00" ngày N+1
    And kết quả "WorkingTimeHours" là 8 giờ (trừ khung nghỉ giữa ca nếu có)

  Scenario: Ca chỉ cho phép loại đơn từ tương ứng với cờ nghiệp vụ (BR-02)
    Given ca "Ca Hành Chính" có "IsAllowShiftSwap" = false
    When nhân viên tạo đơn xin đổi ca (Shift Swap Request, 05) tham chiếu "Ca Hành Chính"
    Then hệ thống từ chối tạo đơn với lỗi "Shift does not allow shift swap request"
```

### Feature: Quy tắc quy đổi giờ công

```gherkin
Feature: Quy tắc quy đổi giờ công theo ca
  Là hệ thống, tôi cần quy đổi đúng khoảng giờ chấm công thô thành loại công được trả lương

  Scenario: Không cho phép hai khung giờ chồng lấn trong cùng ca và loại nghỉ (BR-03)
    Given ca "Ca Hành Chính" đã có "ShiftBracketRule" với "AbsentTypeId" = "OT thường" và khung "17:00"-"19:00"
    When quản trị hệ thống thêm "ShiftBracketRule" mới cùng "ShiftId" và "AbsentTypeId" với khung "18:00"-"20:00"
    Then hệ thống từ chối lưu với lỗi "Overlapping shift bracket rule"

  Scenario: Quy đổi giờ tăng ca theo hệ số ngày (BR-03)
    Given "ShiftBracketRule" của ca "Ca Hành Chính" cho khung "18:00"-"20:00" có "IsOvertime" = true, "DayCoefficient" = 1.5
    When nhân viên có dữ liệu chấm công thô ghi nhận làm việc trong khung "18:00"-"20:00"
    Then hệ thống tính "TotalWorkedHours" cho "DailyAttendance" của khung đó theo hệ số 1.5
```

### Feature: Tính tổng thời gian nghỉ qua ranh giới tháng

```gherkin
Feature: Tính tổng thời gian nghỉ/vắng qua nhiều tháng (GetTotalTime)
  Là kế toán lương, tôi cần tổng thời gian nghỉ được tính đúng khi khoảng ngày vắt qua ranh giới tháng

  Scenario: Duyệt đúng ca hiệu lực từng ngày khi tính tổng thời gian nghỉ (BR-04)
    Given nhân viên "Phạm Văn D" có "ShiftAssignment" ca "Ca Hành Chính" từ "2026-07-28" đến "2026-07-31"
    And nhân viên "Phạm Văn D" có "ShiftAssignment" ca "Ca Đêm" từ "2026-08-01" đến "2026-08-03"
    When hệ thống tính tổng thời gian nghỉ (GetTotalTime) cho loại nghỉ "Nghỉ ốm" từ "2026-07-28" đến "2026-08-03"
    Then hệ thống áp dụng "ShiftBracketRule" của "Ca Hành Chính" cho các ngày từ "2026-07-28" đến "2026-07-31"
    And hệ thống áp dụng "ShiftBracketRule" của "Ca Đêm" cho các ngày từ "2026-08-01" đến "2026-08-03"
    And tổng thời gian nghỉ trả về là tổng đã làm tròn theo đúng quy tắc từng ca của từng ngày, không dùng một quy tắc cố định cho toàn khoảng
```

### Feature: Ca qua đêm

```gherkin
Feature: Xác định ngày công cho ca qua đêm
  Là hệ thống, tôi cần ghi nhận đúng ngày công cho ca làm việc qua đêm

  Scenario: Ngày công tính theo ngày bắt đầu ca khi ca qua đêm (BR-05)
    Given nhân viên "Hoàng Thị E" được phân ca "Ca Đêm" ("IsNextDay" = true) cho ngày "2026-07-13"
    And nhân viên "Hoàng Thị E" quẹt vào lúc "22:05" ngày "2026-07-13" và quẹt ra lúc "06:10" ngày "2026-07-14"
    When hệ thống tính "DailyAttendance" từ dữ liệu chấm công thô trên
    Then "DailyAttendance.AttendanceDate" được ghi nhận là "2026-07-13"
    And "CheckInTime" là "2026-07-13 22:05" và "CheckOutTime" là "2026-07-14 06:10"
```

### Feature: Bảo vệ dữ liệu chấm công thô

```gherkin
Feature: Dữ liệu chấm công thô bất biến
  Là hệ thống, tôi cần đảm bảo dữ liệu chấm công thô không bị chỉnh sửa sau khi import

  Scenario: Từ chối sửa trực tiếp Raw Punch Log (BR-06)
    Given "RawPunchLog" đã được import với "ImportBatchId" = "BATCH-2026-07-13-01"
    When người dùng cố gắng sửa "PunchTime" của một bản ghi "RawPunchLog" đã import
    Then hệ thống từ chối thao tác, chỉ cho phép xử lý qua "UnusualPunchCase" hoặc Punch Correction Request (05)
```

### Feature: Xử lý bất thường chấm công

```gherkin
Feature: Phát hiện và xử lý quẹt thẻ thiếu/sai
  Là nhân viên nhân sự, tôi muốn hệ thống tự phát hiện các ca chỉ có một lượt quẹt

  Scenario: Tự động tạo Unusual Punch Case khi thiếu lượt quẹt ra (BR-07)
    Given nhân viên "Đỗ Văn F" được phân ca "Ca Hành Chính" ngày "2026-07-13"
    And nhân viên "Đỗ Văn F" chỉ có bản ghi quẹt vào ("IOCode" = "In"), không có quẹt ra
    When hệ thống xử lý dữ liệu chấm công thô cuối ngày "2026-07-13"
    Then hệ thống tự động tạo "UnusualPunchCase" với "CaseStatus" = "Unhandled" cho nhân viên "Đỗ Văn F" ngày "2026-07-13"
    And "DailyAttendance" ngày "2026-07-13" của nhân viên "Đỗ Văn F" chưa được tính công chính thức

  Scenario: Tính lại công sau khi xử lý Unusual Punch Case (BR-07)
    Given "UnusualPunchCase" của nhân viên "Đỗ Văn F" ngày "2026-07-13" đang "Unhandled"
    When nhân viên nhân sự gán "HandleMethodId" = "Chấp nhận công đủ ca" và xử lý case
    Then "UnusualPunchCase.CaseStatus" chuyển thành "Handled"
    And "DailyAttendance" ngày "2026-07-13" của nhân viên "Đỗ Văn F" được tính lại theo phương án xử lý đã chọn
```

### Feature: Khoá kỳ chấm công tháng

```gherkin
Feature: Kết chuyển và khoá kỳ chấm công tháng
  Là kế toán lương, tôi cần khoá kỳ chấm công trước khi tính lương

  Scenario: Không cho sửa Daily Attendance sau khi khoá kỳ (BR-08)
    Given "MonthlyTimesheet" tháng "07/2026" của nhân viên "Ngô Thị G" đã "IsLocked" = true
    When người dùng cố gắng sửa "DailyAttendance" ngày "2026-07-15" của nhân viên "Ngô Thị G"
    Then hệ thống từ chối thao tác với lỗi "Timesheet period is locked"
```

### Feature: Đăng ký ca qua Approval Engine

```gherkin
Feature: Đăng ký ca làm việc
  Là nhân viên, tôi muốn đăng ký ca làm việc trước và được quản lý duyệt

  Scenario: Không tạo Shift Assignment khi đăng ký chưa được duyệt (BR-09)
    Given nhân viên "Vũ Văn H" gửi "ShiftRegistration" cho ca "Ca Hành Chính" ngày "2026-08-01"
    And "ApprovalRequest" tương ứng (05) đang ở trạng thái "Pending"
    When hệ thống kiểm tra tạo "ShiftAssignment" từ đăng ký này
    Then hệ thống từ chối tạo "ShiftAssignment" vì "RegistrationStatus" chưa là "Approved"

  Scenario: Tạo Shift Assignment sau khi đăng ký được duyệt (BR-09)
    Given "ShiftRegistration" của nhân viên "Vũ Văn H" có "ApprovalRequest" (05) vừa chuyển "Approved"
    When hệ thống nhận sự kiện "ShiftRegistrationApprovalResolved"
    Then "ShiftRegistration.RegistrationStatus" chuyển thành "Approved"
    And hệ thống tự động tạo "ShiftAssignment" với "Source" = "Registration" cho ngày đăng ký
```
