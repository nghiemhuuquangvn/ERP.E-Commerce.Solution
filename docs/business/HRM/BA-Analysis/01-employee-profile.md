# 01 — Employee Profile (Hồ sơ nhân sự)

> Bounded Context: **Employee Profile** — Core Domain / Aggregate trung tâm của toàn hệ thống HRM. Tài liệu này tuân thủ ubiquitous language, context map và quy ước đặt tên đã định nghĩa tại `00-domain-overview.md`. Nguồn khảo sát: `HRMF2211` (274 dòng, DTO hồ sơ nhân viên theo tab UI), `HRMT2211/2212`, `HRMT2214` (tab bảo hiểm/ngân hàng/thuế), `HRMT2218` (tab gia đình + lưới quan hệ nhân thân), `HRMT2223`, `HT1403` (bảng denormalize dùng cho nghiệp vụ + kế toán), `HRMF2211MES` (đồng bộ MES), `CIP10101` (Phòng/Tổ), `HRMT2182` (attachment dùng chung), `HRMF2211Controller.cs` (business logic tầng API).

---

## PHẦN 1 — DDD (Domain-Driven Design)

### 1.1 Bounded Context

**Mục đích**: Quản lý dữ liệu gốc (master data) của nhân viên — từ định danh cá nhân, thông tin nghề nghiệp hiện tại, thông tin gia đình/người phụ thuộc, đến cấu trúc tổ chức (Phòng ban/Tổ) mà nhân viên trực thuộc. Đây là **Core Domain**: mọi bounded context khác (Labor Contract, Timekeeping, Request & Approval, Payroll, Social Insurance, Personal Income Tax, Training, Maternity Benefits, Personnel Decisions, Recruitment) đều tham chiếu tới `Employee` qua `EmployeeId` và không được sao chép/định nghĩa lại dữ liệu nhân sự gốc.

**Ranh giới (trong phạm vi module này)**:
- Định danh & thông tin cá nhân của nhân viên (CCCD/CMND, hộ chiếu, địa chỉ, dân tộc, tôn giáo, học vấn, ngoại ngữ...).
- Thông tin nghề nghiệp **hiện tại** (chức danh, phòng ban, tổ, hệ số lương tham chiếu, trạng thái làm việc).
- Thông tin gia đình, người thân, người phụ thuộc.
- Thông tin bổ sung tự do theo nhu cầu doanh nghiệp (đi lại, ký túc xá, sức khoẻ...).
- Tệp đính kèm hồ sơ (ảnh đại diện, văn bằng, chứng chỉ...).
- Cấu trúc tổ chức **Department/Team** (Organization Unit) — đặt tại module này vì đây là nơi hợp lý nhất để định nghĩa, các module khác chỉ tham chiếu `DepartmentId`/`TeamId`.

**Ngoài ranh giới (do module khác sở hữu, Employee Profile chỉ tham chiếu `EmployeeId`)**:
- Chi tiết hợp đồng lao động, lịch sử ký kết → `03-labor-contract.md`.
- Ca làm việc, dữ liệu chấm công → `04-timekeeping-shift.md`.
- Đơn từ, luồng duyệt đa cấp → `05-request-approval.md` (Approval Engine — shared kernel).
- Công thức lương, kỳ lương, bảng lương → `06-payroll.md`.
- Đăng ký/tính đóng BHXH-BHYT-BHTN → `07-social-insurance.md`.
- Khấu trừ/quyết toán thuế TNCN → `08-personal-income-tax.md`.
- Kế hoạch/kết quả đào tạo → `09-training.md`.
- Chế độ thai sản → `10-maternity-benefits.md`.
- Quyết định bổ nhiệm/điều chuyển/thôi việc (văn bản pháp lý làm thay đổi trạng thái Employee) → `11-personnel-decisions.md`. Employee Profile chỉ phản ánh **trạng thái hiện tại** sau khi Personnel Decision được thực thi, không lưu lại quy trình ra quyết định.
- Ứng viên chưa trúng tuyển → `02-recruitment.md` (khi trúng tuyển, `Candidate` được chuyển hoá thành `Employee` mới thông qua sự kiện tích hợp, không kế thừa trực tiếp).

### 1.2 Ubiquitous Language — Bảng thuật ngữ riêng module

> Bổ sung/chi tiết hoá cho các thuật ngữ đã có ở bảng tổng hợp mục 5 của `00-domain-overview.md`. Không tạo thuật ngữ mâu thuẫn với tài liệu gốc.

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ (tham khảo) | Giải thích |
|---|---|---|---|
| Mã nhân viên | Employee Code | `EmployeeID` | Mã định danh nghiệp vụ, duy nhất trong 1 Division |
| Mã nhân viên thời vụ | Seasonal Employee Code | `EmployeeSeasonalID` | Mã riêng cho lao động thời vụ/mùa vụ |
| Đơn vị hạch toán/Công ty | Division | `DivisionID` | Pháp nhân/chi nhánh quản lý nhân viên |
| Họ / Tên đệm / Tên | Last Name / Middle Name / First Name | `LastName/MiddleName/FirstName` | Cấu thành họ tên đầy đủ |
| Ngày sinh / Nơi sinh | Date Of Birth / Birth Place | `Birthday/BornPlace` | |
| Giới tính | Gender | `IsMale` | Legacy chỉ có cờ nam/nữ, thiết kế mới dùng enum |
| Tình trạng hôn nhân | Marital Status | `IsSingle` | Legacy chỉ có cờ độc thân, thiết kế mới dùng enum |
| Số CMND/CCCD | Identity Card Number | `IdentifyCardNo` | |
| Nơi cấp/Ngày cấp CCCD | Identity Card Issued Place/Date | `IdentifyPlace/IdentifyDate` | |
| Hộ chiếu | Passport | `PassportNo/PassportDate/PassportEnd` | |
| Địa chỉ thường trú/tạm trú | Permanent/Temporary Address | `PermanentAddress/TemporaryAddress` | |
| Dân tộc | Ethnicity | `EthnicID` | |
| Tôn giáo | Religion | `ReligionID` | |
| Quốc tịch | Nationality | `CountryID/NativeCountry` | |
| Thành phần chính trị | Political Status | `PoliticsID` | |
| Trình độ học vấn | Education Level | `EducationLevelID` | |
| Ngoại ngữ / Trình độ | Foreign Language / Language Level | `Language1-4ID/LanguageLevel1-4ID` | |
| Trạng thái làm việc | Employee Working Status | `WorkingStatus`, `EmployeeStatus` | Đề xuất enum tường minh (xem 1.3) |
| Ngày tuyển dụng | Recruit Date | `RecruitDate` | |
| Ngày nghỉ việc | Termination Date | `LeaveDate/LeaveToDate` | |
| Thời gian thử việc | Probation Period | `FromApprenticeTime/ToApprenticeTime` | |
| Chức danh | Duty / Title | `DutyID/TitleID` | |
| Hệ số lương | Salary Coefficient | `SalaryCoefficient` | |
| Hệ số chức danh | Duty Coefficient | `DutyCoefficient` | |
| Hệ số thời gian | Time Coefficient | `TimeCoefficient` | |
| Lương cơ bản | Base Salary | `BaseSalary` | Trường bị giới hạn quyền xem (field-level permission) |
| Lương đóng bảo hiểm | Insurance Salary | `InsuranceSalary` | |
| Bậc lương | Salary Level | `SalaryLevel/SalaryLevelDate` | |
| Số sổ BHXH | Social Insurance Book Number | `SoInsuranceNo` | |
| Số thẻ BHYT | Health Insurance Card Number | `HeInsuranceNo` | |
| Nơi khám BHYT ban đầu | Initial Hospital | `HospitalID` | |
| Tài khoản ngân hàng | Bank Account | `BankID/BankAccountNo` | |
| Mã số thuế cá nhân | Personal Tax Code | `PersonalTaxID/TaxID` | |
| Người phụ thuộc | Dependent | `Dependents`, `HRMT2214` | Người được giảm trừ gia cảnh khi tính thuế TNCN |
| Thành viên gia đình | Family Member | `HRMT2218` (Father/Mother/Spouse + lưới quan hệ) | |
| Quan hệ nhân thân | Relation Type | `RelationID/RelationName` | Cha/Mẹ/Vợ-chồng/Con/Anh-chị-em/Khác |
| Ký túc xá | Dormitory | `IsDorm/DormInfo`, `HT1403.KTXFromDate/KTXToDate` | |
| Phương tiện đi lại | Transport Mode | `TransportMode` | |
| Tuyến đường đi làm / Điểm đón | Commute Route / Pickup Point | `CommuteRoute/PickupPoint` | |
| Thông tin bổ sung | Additional Info | `DP01-27+DPNote` | Lưới dữ liệu mở rộng tự do theo nhu cầu doanh nghiệp |
| Mục tiêu KPI | Employee Target | `Target01-10ID/TargetAmount01-10` | |
| Đơn vị tổ chức | Organization Unit | `CIP10101` | Khái niệm gộp của Department + Team |
| Cảnh báo trùng CCCD | Duplicate Identity Card Warning | `CheckExistIdentifyCardNo` (`HRMF2211Controller`) | Cảnh báo mềm, không chặn cứng |
| Xuất hồ sơ Excel | Export Employee Dossier | — | Use case xuất hồ sơ nhân viên ra file Excel |
| Đồng bộ MES | Employee-MES Sync | `HRMF2211MES` | Anti-Corruption Layer đồng bộ user sang hệ thống MES xưởng sản xuất |
| Nhân viên tuyển lại | Rehired Employee | `MidEmployeeID` (`HT1403`) | Nhân viên nghỉ việc rồi được tuyển lại, tham chiếu hồ sơ cũ |

### 1.3 Aggregate Root, Entities, Value Objects

#### 1.3.1 Aggregate Root: `Employee`

Gộp `HRMF2211` + `HRMT2211/2212` + `HRMT2214` (tab bảo hiểm/ngân hàng/thuế) + phần "hiện tại" của `HT1403` thành **một Aggregate Employee duy nhất**, không tách theo tab UI như thiết kế cũ. Các trường tự do dạng lưới (`DP01-27`, `Target01-10`) và dữ liệu 1-n (gia đình, tài liệu định danh, tệp đính kèm) được tách thành Entity con thuộc cùng Aggregate.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính, định danh nội bộ hệ thống mới |
| EmployeeCode | string(20) | Có | Mã nhân viên nghiệp vụ, duy nhất trong `DivisionId` (nguồn: `EmployeeID`) |
| SeasonalEmployeeCode | string(20) | Không | Mã nhân viên thời vụ (nguồn: `EmployeeSeasonalID`) |
| DivisionId | Guid | Có | Đơn vị hạch toán quản lý nhân viên (nguồn: `DivisionID`) |
| LastName | string(50) | Có | Họ (nguồn: `LastName`) |
| MiddleName | string(50) | Không | Tên đệm (nguồn: `MiddleName`) |
| FirstName | string(50) | Có | Tên (nguồn: `FirstName`) |
| FullName | string(150) | Có (derived) | Họ tên đầy đủ, tính từ 3 field trên |
| DateOfBirth | date | Có | Ngày sinh (nguồn: `Birthday`) |
| BirthPlace | string(200) | Không | Nơi sinh (nguồn: `BornPlace`) |
| Gender | enum `Gender` | Có | Giới tính (nguồn: `IsMale`) |
| MaritalStatus | enum `MaritalStatus` | Không | Tình trạng hôn nhân (nguồn: `IsSingle`) |
| IdentityDocuments | Entity con (1-n) | Có ít nhất 1 | Xem `EmployeeDocument` (nguồn: `IdentifyCardNo/PassportNo/...`) |
| PermanentAddress | Value Object `Address` | Có | Địa chỉ thường trú (nguồn: `PermanentAddress`) |
| TemporaryAddress | Value Object `Address` | Không | Địa chỉ tạm trú (nguồn: `TemporaryAddress`) |
| EthnicityId | Guid | Không | Dân tộc (nguồn: `EthnicID`) |
| ReligionId | Guid | Không | Tôn giáo (nguồn: `ReligionID`) |
| NationalityId | Guid | Có | Quốc tịch (nguồn: `CountryID`) |
| NativeCountry | string(100) | Không | Nguyên quán (nguồn: `NativeCountry`) |
| PoliticalStatusId | Guid | Không | Thành phần chính trị (nguồn: `PoliticsID`) |
| EducationLevelId | Guid | Không | Trình độ học vấn (nguồn: `EducationLevelID`) |
| SpecialEducationId | Guid | Không | Trình độ chuyên môn đặc biệt (nguồn: `SpecialID`) |
| InformaticsLevel | string(50) | Không | Trình độ tin học (nguồn: `InformaticsLevel`) |
| Language1Id..Language4Id | Guid | Không | Ngoại ngữ 1-4 (nguồn: `Language1-4ID`) |
| LanguageLevel1Id..LanguageLevel4Id | Guid | Không | Trình độ ngoại ngữ tương ứng (nguồn: `LanguageLevel1-4ID`) |
| AvatarImageUrl | string(500) | Không | Ảnh đại diện (nguồn: `ImageID/ImageUrl`) |
| Email | string(150) | Không | Email cá nhân (nguồn: `Email`) |
| HomePhone | string(20) | Không | Điện thoại nhà (nguồn: `HomePhone`) |
| HomeFax | string(20) | Không | Fax (nguồn: `HomeFax`) |
| Height, Weight | decimal | Không | Chiều cao/cân nặng (nguồn: `Height/Weight`) |
| BloodGroup | string(5) | Không | Nhóm máu (nguồn: `BloodGroup`) |
| LeftEyeVision, RightEyeVision | string(10) | Không | Thị lực mắt trái/phải (nguồn: `LeftEye/RightEye`) |
| HealthStatus | string(200) | Không | Tình trạng sức khoẻ (nguồn: `HealthStatus`) |
| SmokingStatus | string(20) | Không | Tình trạng hút thuốc (nguồn: `SmokingStatus`) |
| Hobby, Strength, Weakness, PersonalAim | string | Không | Sở thích/điểm mạnh/điểm yếu/mục tiêu cá nhân |
| EmployeeWorkingStatus | enum `EmployeeWorkingStatus` | Có | Trạng thái làm việc hiện tại (nguồn: `WorkingStatus/EmployeeStatus`, đề xuất mới) |
| RecruitDate | date | Có | Ngày tuyển dụng (nguồn: `RecruitDate`) |
| ProbationStartDate | date | Không | Bắt đầu thử việc (nguồn: `FromApprenticeTime`) |
| ProbationEndDate | date | Không | Kết thúc thử việc (nguồn: `ToApprenticeTime`) |
| TerminationDate | date | Không | Ngày bắt đầu nghỉ việc (nguồn: `LeaveDate`) |
| TerminationEffectiveToDate | date | Không | Ngày nghỉ việc hiệu lực đến (nguồn: `LeaveToDate`) |
| QuitReasonId | Guid | Không | Lý do nghỉ việc (nguồn: `HT1403.QuitJobID`) |
| AbsentReason | string(200) | Không | Lý do vắng/tạm ngưng (nguồn: `AbsentReason`) |
| StatusNotes | string(200) | Không | Ghi chú trạng thái (nguồn: `StatusNotes`) |
| DutyId | Guid | Không | Chức danh (nguồn: `DutyID`) |
| TitleId | Guid | Không | Vị trí/Title (nguồn: `TitleID`) |
| DepartmentId | Guid | Có | Phòng ban hiện tại (nguồn: `DepartmentID`) |
| TeamId | Guid | Không | Tổ/Nhóm hiện tại (nguồn: `TeamID`) |
| SalaryCoefficient | decimal(18,4) | Không | Hệ số lương (nguồn: `SalaryCoefficient`) |
| DutyCoefficient | decimal(18,4) | Không | Hệ số chức danh (nguồn: `DutyCoefficient`) |
| TimeCoefficient | decimal(18,4) | Không | Hệ số thời gian (nguồn: `TimeCoefficient`) |
| BaseSalary | decimal(18,2) | Không | Lương cơ bản — **field-level permission** (nguồn: `BaseSalary`) |
| InsuranceSalary | decimal(18,2) | Không | Lương đóng bảo hiểm — **field-level permission** (nguồn: `InsuranceSalary`) |
| Salary01, Salary02, Salary03 | decimal(18,2) | Không | Các mức lương phụ trợ — **field-level permission** (nguồn: `Salary01-03`) |
| SalaryLevel | string(20) | Không | Bậc lương (nguồn: `SalaryLevel`) |
| SalaryLevelDate | date | Không | Ngày áp dụng bậc lương (nguồn: `SalaryLevelDate`) |
| SocialInsuranceBookNo | string(20) | Không | Số sổ BHXH (nguồn: `SoInsuranceNo`) |
| SocialInsuranceStartDate | date | Không | Ngày bắt đầu tham gia BHXH (nguồn: `SoInsurBeginDate`) |
| HealthInsuranceCardNo | string(20) | Không | Số thẻ BHYT (nguồn: `HeInsuranceNo`) |
| HealthInsuranceFromDate/ToDate | date | Không | Hiệu lực thẻ BHYT (nguồn: `HFromDate/HToDate`) |
| InitialHospitalId | Guid | Không | Nơi khám BHYT ban đầu (nguồn: `HospitalID`) |
| BankAccount | Value Object `BankAccount` | Không | Tài khoản ngân hàng nhận lương (nguồn: `BankID/BankAccountNo/BankAddress`) |
| PersonalTaxCode | string(20) | Không | Mã số thuế TNCN (nguồn: `PersonalTaxID/TaxID`) |
| TaxRegisteredDate | date | Không | Ngày đăng ký MST (nguồn: `TaxDate`) |
| TaxRegisteredAddress | string(200) | Không | Địa chỉ đăng ký MST (nguồn: `TaxAddress`) |
| DependentsCountCache | int | Không | Số người phụ thuộc — cache đếm từ `FamilyMembers` (nguồn: `Dependents`) |
| IsDormitory | bool | Không | Có ở ký túc xá (nguồn: `IsDorm`) |
| DormitoryInfo | string(200) | Không | Thông tin ký túc xá (nguồn: `DormInfo`) |
| DormitoryFromDate/ToDate | date | Không | Thời gian ở KTX (nguồn: `HT1403.KTXFromDate/KTXToDate`) |
| TransportMode | string(50) | Không | Phương tiện đi lại (nguồn: `TransportMode`) |
| LicensePlateNo | string(20) | Không | Biển số xe (nguồn: `LicensePlateNo`) |
| CommuteRoute | string(200) | Không | Tuyến đường đi làm (nguồn: `CommuteRoute`) |
| PickupPoint | string(200) | Không | Điểm đón (nguồn: `PickupPoint`) |
| IsDrivingLicense | bool | Không | Có bằng lái xe (nguồn: `IsDrivingLicense`) |
| DrivingLicenseNo | string(20) | Không | Số bằng lái (nguồn: `DrivingLicenseID`) |
| ExpenseAccountId | Guid | Không | Tài khoản chi phí lương liên kết kế toán (nguồn: `HT1403.ExpenseAccountID`) |
| PayableAccountId | Guid | Không | Tài khoản phải trả lương liên kết kế toán (nguồn: `HT1403.PayableAccountID`) |
| PreviousEmployeeId | Guid (self-FK) | Không | Liên kết hồ sơ cũ khi tuyển lại (nguồn: `MidEmployeeID`) |
| IsUserMes | bool | Không | Có tạo tài khoản MES hay không (nguồn: `IsUserMES`) |
| IsAutoCreateUser | bool | Không | Tự động tạo user hệ thống (nguồn: `IsAutoCreateUser`) |
| IsActive | bool | Có | Cờ hoạt động tổng quát (nguồn: `IsActive`) |
| Notes | string(500) | Không | Ghi chú chung (nguồn: `Notes`) |
| IsDeleted | bool | Có | Soft delete |
| DeletedAtUtc | datetime2 | Không | Thời điểm xoá mềm |
| CreatedAtUtc / CreatedBy | datetime2 / Guid | Có | Audit |
| LastModifiedAtUtc / LastModifiedBy | datetime2 / Guid | Không | Audit |

> **Ghi chú thiết kế**: Lịch sử hợp đồng (`LaborContractDate`, `LaborContractStart1-3/End1-3`) và lịch sử công ty cũ (`CompanyName1/2`, `TitleIDOld1/2`, `CompanyFromDate/ToDate1/2`) **không** đưa vào Aggregate `Employee` của thiết kế mới. Hợp đồng lao động được chuẩn hoá thành bảng 1-n riêng ở `03-labor-contract.md`; lịch sử công ty cũ (kinh nghiệm làm việc trước khi vào công ty) được đưa vào `EmployeeWorkHistory` (entity con tuỳ chọn, xem 1.3.4) thay vì hard-code 2 cột. Employee Profile chỉ cần biết "nhân viên có N hợp đồng" qua truy vấn liên module bằng `EmployeeId`, không lặp lại chi tiết.

#### 1.3.2 Entity con: `EmployeeDocument` (Value Object hoá dạng bảng 1-n)

Chuẩn hoá `IdentifyCardNo/Date/Place/CityID`, `PassportNo/Date/End/Place`, `DrivingLicenseID`, `TemporaryResidenceCard` thành một danh sách tài liệu định danh, cho phép lưu lịch sử cấp lại CCCD/hộ chiếu thay vì ghi đè.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid | Có | FK tới `Employee` |
| DocumentType | enum `IdentityDocumentType` | Có | `NationalId` / `Passport` / `DriverLicense` / `TemporaryResidenceCard` |
| DocumentNo | string(30) | Có | Số giấy tờ (nguồn: `IdentifyCardNo`/`PassportNo`/...) |
| IssuedDate | date | Không | Ngày cấp (nguồn: `IdentifyDate`/`PassportDate`) |
| IssuedPlace | string(200) | Không | Nơi cấp (nguồn: `IdentifyPlace`/`PassportPlace`) |
| IssuedCityId | Guid | Không | Tỉnh/thành cấp (nguồn: `IdentifyCityID`) |
| ExpiryDate | date | Không | Ngày hết hạn (nguồn: `PassportEnd`) |
| IsCurrent | bool | Có | Đánh dấu giấy tờ đang hiệu lực (mới nhất) |

#### 1.3.3 Entity con: `FamilyMember`

Gộp các field cố định `FatherName/MotherName/SpouseName...` và lưới quan hệ (`RelationID/RelationName`) của `HRMT2218` thành **một entity duy nhất, dạng danh sách 1-n**, thay vì vừa có field cứng vừa có lưới như thiết kế cũ.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid | Có | FK tới `Employee` |
| RelationType | enum `FamilyRelationType` | Có | `Father`/`Mother`/`Spouse`/`Child`/`Sibling`/`Other` (nguồn: `RelationID/RelationName`, hợp nhất `Father*/Mother*/Spouse*`) |
| FullName | string(150) | Có | Họ tên người thân (nguồn: `FatherName/MotherName/SpouseName/RelationName`) |
| YearOfBirth | int | Không | Năm sinh (nguồn: `FatherYear/MotherYear/SpouseYear`) |
| Occupation | string(100) | Không | Nghề nghiệp (nguồn: `FatherJob/MotherJob/SpouseJob`) |
| Address | string(200) | Không | Địa chỉ (nguồn: `FatherAddress/MotherAddress/SpouseAddress/RelationAddress`) |
| PhoneNumber | string(20) | Không | Điện thoại (nguồn: `FatherPhone/MotherPhone/SpousePhone/RelationPhone`) |
| IsDeceased | bool | Có | Đã mất (nguồn: `IsFatherDeath/IsMotherDeath/IsSpouseDeath`) |
| IsDependentForTax | bool | Có | Có phải người phụ thuộc tính giảm trừ thuế TNCN hay không |
| DependentRegisteredDate | date | Không | Ngày đăng ký người phụ thuộc (nguồn: `RelationDate`) |
| Notes | string(200) | Không | Ghi chú (nguồn: `FatherNote/MotherNote/SpouseNote/Notes`) |
| CreatedAtUtc / CreatedBy | datetime2 / Guid | Có | Audit |
| LastModifiedAtUtc / LastModifiedBy | datetime2 / Guid | Không | Audit |

> `HouseholdOwner` (chủ hộ khẩu) của `HRMT2218` được giữ ở mức `Employee.PermanentAddress` (VO Address có field `HouseholdOwnerName` tuỳ chọn) vì đây là thuộc tính của hộ khẩu nhân viên, không phải của từng người thân.

#### 1.3.4 Entity con: `EmployeeTarget` (KPI/Mục tiêu — Tab05)

Chuẩn hoá `Target01-10ID/TargetAmount01-10` (10 cột hard-code) thành danh sách 1-n.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid | Có | FK tới `Employee` |
| TargetId | Guid | Có | Mã mục tiêu KPI (lookup, nguồn: `TargetNNID`) |
| TargetAmount | decimal(18,2) | Không | Giá trị mục tiêu (nguồn: `TargetAmountNN`) |
| PeriodYear | int | Không | Năm áp dụng mục tiêu |
| Notes | string(200) | Không | Ghi chú |

#### 1.3.5 Entity con: `EmployeeAdditionalInfo` (Tab09 — thông tin bổ sung tự do)

Chuẩn hoá `DP01-27 + DPNote` (27 cột chuỗi tự do không có ngữ nghĩa cố định trong code cũ) thành mô hình EAV (Entity-Attribute-Value) để mở rộng không giới hạn số cột.

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid | Có | FK tới `Employee` |
| FieldCode | string(20) | Có | Mã trường tự định nghĩa (nguồn: `DP01`...`DP27`) |
| FieldLabel | string(100) | Không | Nhãn hiển thị (cấu hình tại danh mục field động) |
| FieldValue | string(500) | Không | Giá trị |
| DisplayOrder | int | Không | Thứ tự hiển thị |

#### 1.3.6 Entity con: `EmployeeWorkHistory` (tuỳ chọn — lịch sử làm việc trước khi vào công ty)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid | Có | FK tới `Employee` |
| CompanyName | string(200) | Có | Tên công ty cũ (nguồn: `CompanyName1/2`) |
| TitleName | string(100) | Không | Chức danh tại công ty cũ (nguồn: `TitleIDOld1/2`) |
| FromDate / ToDate | date | Không | Thời gian làm việc (nguồn: `CompanyFromDate/ToDate1/2`) |

#### 1.3.7 Entity con: `EmployeeAttachment`

Dùng chung cơ chế attachment (nguồn `HRMT2182`), áp dụng cho hồ sơ nhân viên (ảnh đại diện, bằng cấp, chứng chỉ, bản scan CCCD...).

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| EmployeeId | Guid | Có | FK tới `Employee` |
| Category | enum `AttachmentCategory` | Có | `Avatar`/`IdentityDocument`/`Certificate`/`Contract`/`Other` |
| FileName | string(255) | Có | Tên tệp gốc |
| FileUrl | string(500) | Có | Đường dẫn lưu trữ |
| FileType | string(20) | Không | Loại tệp (pdf/jpg/png/docx...) |
| FileSizeBytes | bigint | Không | Dung lượng tệp |
| UploadedAtUtc | datetime2 | Có | Thời điểm tải lên |
| UploadedBy | Guid | Có | Người tải lên |

#### 1.3.8 Entity phụ trợ: `Department` / `Team` (Organization Unit)

Nguồn `CIP10101` chỉ có `DepartmentID/TeamID/TeamName` (không có bảng tên Phòng ban độc lập trong dữ liệu khảo sát) — thiết kế mới bổ sung `Department` như entity độc lập, `Team` tham chiếu `DepartmentId` để đảm bảo tính nhất quán tổ chức.

**Department**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| DepartmentCode | string(20) | Có | Mã phòng ban, duy nhất trong `DivisionId` |
| DepartmentName | string(200) | Có | Tên phòng ban |
| DivisionId | Guid | Có | Đơn vị hạch toán |
| ParentDepartmentId | Guid | Không | Phòng ban cha (hỗ trợ cây tổ chức đa cấp) |
| IsActive | bool | Có | Đang hoạt động |
| IsDeleted / DeletedAtUtc | bool / datetime2 | Có/Không | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | | Có/Không | Audit |

**Team**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| TeamCode | string(20) | Có | Mã tổ/nhóm (nguồn: `TeamID`) |
| TeamName | string(200) | Có | Tên tổ/nhóm (nguồn: `TeamName`) |
| DepartmentId | Guid | Có | FK tới `Department` |
| DivisionId | Guid | Có | Đơn vị hạch toán (nguồn: `DivisionID`) |
| IsActive | bool | Có | Đang hoạt động (nguồn: `Disabled` — đảo nghĩa) |
| Notes | string(200) | Không | Ghi chú (nguồn: `Notes/Notes01`) |
| IsDeleted / DeletedAtUtc | bool / datetime2 | Có/Không | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | | Có/Không | Audit |

**Duty** (Chức danh — mã cũ `DutyID`, được các cấp duyệt của Approval Engine (`05-request-approval.md`) dùng làm điều kiện resolve người duyệt qua `ApprovalMatrixLevel.DutyId`)

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| Id | Guid | Có | Khoá chính |
| DutyCode | string(20) | Có | Mã chức danh, duy nhất trong `DivisionId` |
| DutyName | string(200) | Có | Tên chức danh (VD "Tổ trưởng", "Trưởng phòng", "Giám đốc") |
| DivisionId | Guid | Có | Đơn vị hạch toán |
| IsActive | bool | Có | Đang sử dụng |
| IsDeleted / DeletedAtUtc | bool / datetime2 | Có/Không | Soft delete |
| CreatedAtUtc/By, LastModifiedAtUtc/By | | Có/Không | Audit |

> **Ghi chú đối chiếu cấp tổ chức (Data Gap đã đóng)**: dữ liệu khảo sát gốc (`HRMT2360`) có 4 cấp tổ chức trên 1 đơn từ — `DepartmentID/SectionID/SubsectionID/ProcessID` — trong khi thiết kế mới ở đây chỉ mô hình 2 cấp chính thức là `Department` và `Team`. Để `05-request-approval.md` (dùng `RequesterSectionId`/`RequesterSubsectionId` làm `OrgScope` khi resolve người duyệt) tham chiếu nhất quán, quy ước: **`Section` tương đương `Emp_Team`** (cấp tổ/nhóm), còn **`Subsection`/`Process`** là cấp chi tiết hơn Team — nếu doanh nghiệp cần mô hình sâu hơn 2 cấp, bổ sung bảng `Emp_Subsection (Id, TeamId FK, SubsectionCode, SubsectionName, ...)` theo cùng khuôn mẫu `Team`; nếu không, `RequesterSubsectionId`/`SubsectionId` tại module 05 có thể để `NULL` và chỉ dùng `Department`/`Team` làm `OrgScope`.

#### 1.3.9 Value Objects (mapped dạng embedded columns trong `Employee`, không tách bảng riêng)

**`Address`**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| FullAddressText | string(300) | Có | Địa chỉ đầy đủ dạng text (tương thích ngược với `PermanentAddress/TemporaryAddress` legacy dạng chuỗi tự do) |
| ProvinceId | Guid | Không | Tỉnh/Thành phố (chuẩn hoá thêm cho thiết kế mới) |
| DistrictId | Guid | Không | Quận/Huyện |
| WardId | Guid | Không | Phường/Xã |
| HouseholdOwnerName | string(150) | Không | Chủ hộ khẩu (nguồn: `HouseholdOwner`, chỉ áp dụng cho `PermanentAddress`) |

**`BankAccount`**

| Tên field | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| BankId | Guid | Không | Ngân hàng (nguồn: `BankID`) |
| AccountNo | string(30) | Không | Số tài khoản (nguồn: `BankAccountNo`) |
| BranchAddress | string(200) | Không | Địa chỉ chi nhánh (nguồn: `BankAddress`) |

**`PersonName`** — dùng làm khuôn mẫu chung, mapped trực tiếp lên `LastName/MiddleName/FirstName/FullName` của `Employee`.

### 1.4 Enum đề xuất

```
enum Gender {
    Male = 1,
    Female = 2,
    Other = 3          // mở rộng so với cờ nhị phân IsMale của legacy
}

enum MaritalStatus {
    Single = 1,
    Married = 2,
    Divorced = 3,
    Widowed = 4
}

enum EmployeeWorkingStatus {
    Probation = 1,      // đang thử việc
    Active = 2,          // đang làm việc chính thức
    OnLeave = 3,          // đang nghỉ (thai sản/nghỉ dài hạn không hưởng lương...)
    Suspended = 4,        // tạm ngưng công tác (kỷ luật/chờ xử lý)
    Terminated = 5         // đã nghỉ việc/chấm dứt hợp đồng
}

enum IdentityDocumentType {
    NationalId = 1,             // CMND/CCCD
    Passport = 2,
    DriverLicense = 3,
    TemporaryResidenceCard = 4  // nguồn: TemporaryResidenceCard (lao động nước ngoài)
}

enum FamilyRelationType {
    Father = 1,
    Mother = 2,
    Spouse = 3,
    Child = 4,
    Sibling = 5,
    Other = 6
}

enum AttachmentCategory {
    Avatar = 1,
    IdentityDocument = 2,
    Certificate = 3,
    Contract = 4,
    Other = 5
}
```

> `EmployeeWorkingStatus` không có enum tường minh trong code cũ (`WorkingStatus`/`EmployeeStatus` là chuỗi/số tự do) — đây là đề xuất chuẩn hoá của tài liệu này dựa trên vòng đời nhân sự phổ biến trong ERP nhân sự Việt Nam, cần rà soát cùng nghiệp vụ khi generate code thật.

### 1.5 Domain Events

| Sự kiện | Kích hoạt khi | Payload chính | Module tiêu thụ (Consumers) |
|---|---|---|---|
| `EmployeeCreated` | Tạo mới hồ sơ nhân viên thành công | `EmployeeId, EmployeeCode, FullName, DepartmentId, RecruitDate` | Labor Contract, Payroll, Social Insurance, Timekeeping, Employee-MES Sync |
| `EmployeeProfileUpdated` | Cập nhật thông tin cá nhân/nghề nghiệp | `EmployeeId, ChangedFields` | Employee-MES Sync |
| `EmployeePositionChanged` | Thay đổi `DepartmentId/TeamId/DutyId/TitleId` | `EmployeeId, OldDepartmentId, NewDepartmentId, OldTitleId, NewTitleId, EffectiveDate` | Payroll (hệ số lương), Request & Approval (người duyệt theo phòng ban mới), Personnel Decisions (đối chiếu quyết định) |
| `EmployeeStatusChanged` | `EmployeeWorkingStatus` chuyển trạng thái | `EmployeeId, OldStatus, NewStatus, EffectiveDate, ReasonId` | Timekeeping, Payroll, Social Insurance, Request & Approval |
| `EmployeeTerminated` | `EmployeeWorkingStatus` chuyển sang `Terminated` (chuyên biệt hoá `EmployeeStatusChanged`) | `EmployeeId, TerminationDate, QuitReasonId` | Payroll (chốt lương cuối), Social Insurance (báo giảm), Labor Contract (kết thúc hợp đồng), Employee-MES Sync (thu hồi tài khoản) |
| `EmployeeDependentRegistered` / `EmployeeDependentRemoved` | Thêm/xoá `FamilyMember` có `IsDependentForTax = true` | `EmployeeId, FamilyMemberId` | Personal Income Tax (giảm trừ gia cảnh) |
| `EmployeeSyncedToMes` | Sau `EmployeeCreated`/`EmployeeProfileUpdated`/`EmployeeStatusChanged` với `IsUserMes = true` | `UserId(=EmployeeCode), FullName, DivisionId, PhoneNumber, Email, IsActive, UserLevel` | Hệ thống MES (Anti-Corruption Layer, đồng bộ 1 chiều ERP → MES) |
| `EmployeeIdentityDocumentUpdated` | Thêm mới/cập nhật `EmployeeDocument` | `EmployeeId, DocumentType, DocumentNo` | Social Insurance, Personal Income Tax (đối chiếu định danh) |

### 1.6 Business Rules & Invariants

| Mã | Nội dung |
|---|---|
| **BR-01** | `EmployeeCode` là bắt buộc, duy nhất trong phạm vi `DivisionId`, và **không được sửa** sau khi hồ sơ đã được tạo (immutable business key). |
| **BR-02** | `LastName`, `FirstName`, `DateOfBirth`, `Gender`, `NationalityId`, `RecruitDate`, `DepartmentId` là các trường bắt buộc khi tạo hồ sơ nhân viên. |
| **BR-03** | Tuổi lao động tối thiểu: `RecruitDate - DateOfBirth >= 15 năm` (theo Bộ luật Lao động VN). Nếu tuổi từ 15 đến dưới 18, hệ thống yêu cầu xác nhận có sự đồng ý của người đại diện theo pháp luật (cảnh báo, không chặn cứng); dưới 15 tuổi thì **chặn cứng**, không cho tạo hồ sơ (trừ một số ngành nghệ thuật/thể thao đặc thù — cấu hình được, xem Data Gap). |
| **BR-04** | Số giấy tờ định danh (`EmployeeDocument.DocumentNo` với `DocumentType = NationalId`) phải đúng định dạng CMND (9 chữ số) hoặc CCCD (12 chữ số); `IssuedDate` không được lớn hơn ngày hiện tại. |
| **BR-05** | Khi tạo/sửa `EmployeeDocument` loại `NationalId`, hệ thống **cảnh báo mềm** nếu `DocumentNo` đã tồn tại ở một nhân viên khác đang `IsActive = true` (nguồn thực tế: `CheckExistIdentifyCardNo`), nhưng **không chặn cứng** việc lưu — người dùng phải xác nhận bỏ qua cảnh báo (`ForceSave = true`) mới lưu tiếp. |
| **BR-06** | `ExpiryDate` (hộ chiếu) hoặc `ExpiryDate` của bất kỳ `EmployeeDocument` nào, nếu có, phải lớn hơn `IssuedDate`. |
| **BR-07** | `TerminationDate` (nếu có) phải lớn hơn hoặc bằng `RecruitDate`; `TerminationEffectiveToDate` (nếu có) phải lớn hơn hoặc bằng `TerminationDate`. |
| **BR-08** | Khi `EmployeeWorkingStatus` chuyển sang `Terminated`, `TerminationDate` là bắt buộc và hệ thống phát sinh sự kiện `EmployeeTerminated`. |
| **BR-09** | `ProbationEndDate` phải lớn hơn hoặc bằng `ProbationStartDate`. Thời gian thử việc tối đa theo luật lao động hiện hành (180/60/30/6 ngày tuỳ vị trí) được cấu hình qua Policy/Configuration (xem mục 7 — Data Gap của `00-domain-overview.md`), hệ thống **cảnh báo** nếu vượt trần cấu hình, không cứng hoá số ngày trong code. |
| **BR-10** | Các trường lương (`BaseSalary`, `InsuranceSalary`, `Salary01-03`) chỉ hiển thị cho người dùng có quyền `Employee.ViewSalary`; người không có quyền nhận giá trị bị che (`null`/masked) ở tầng API, kể cả khi export hồ sơ ra Excel. |
| **BR-11** | `TeamId` (nếu có) phải thuộc `DepartmentId` đã chọn của cùng nhân viên (không cho phép Team và Department không liên kết với nhau). |
| **BR-12** | `BankAccount.AccountNo` và `BankAccount.BankId` phải cùng có giá trị hoặc cùng rỗng (không cho phép chỉ nhập một trong hai). |
| **BR-13** | Hồ sơ nhân viên **không được xoá cứng** (hard delete); chỉ được soft-delete (`IsDeleted = true`) và chỉ khi không còn bị tham chiếu bởi dữ liệu giao dịch đang hoạt động (hợp đồng còn hiệu lực, kỳ lương chưa chốt, chấm công...). |
| **BR-14** | `DependentsCountCache` phải luôn khớp với số lượng `FamilyMember` có `IsDependentForTax = true` của cùng `EmployeeId` (bất biến đồng bộ giữa Aggregate và Entity con). |
| **BR-15** | Chuyển trạng thái `EmployeeWorkingStatus` tuân theo state machine: `Probation → Active`, `Active ↔ OnLeave`, `Active ↔ Suspended`, `(Probation|Active|OnLeave|Suspended) → Terminated`. `Terminated` là trạng thái kết thúc (terminal state) — không tự động quay lại `Active`; muốn tuyển lại phải tạo hồ sơ `Employee` mới có `PreviousEmployeeId` trỏ về hồ sơ cũ. |
| **BR-16** | `SeasonalEmployeeCode` chỉ áp dụng khi nhân viên thuộc loại lao động thời vụ; không được trùng với `EmployeeCode` của bất kỳ nhân viên nào (kể cả thời vụ hay chính thức). |
| **BR-17** | Chức năng "Xuất hồ sơ Excel" phải tôn trọng BR-10 (field-level permission) — các cột lương bị loại khỏi file xuất hoặc để trống nếu người xuất không có quyền `Employee.ViewSalary`. |
| **BR-18** | `AvatarImageUrl`/`EmployeeAttachment` chỉ chấp nhận định dạng ảnh phổ biến (jpg/png/gif) với dung lượng tối đa cấu hình được (đề xuất mặc định 5MB); các loại tài liệu khác (bằng cấp, hợp đồng scan...) chấp nhận thêm pdf/docx. |
| **PersonalTaxCode` (nếu nhập) phải đúng định dạng MST cá nhân Việt Nam (10 số, hoặc 13 số nếu có mã chi nhánh phụ thuộc). |
| **BR-20** | Employee Profile không lưu chi tiết hợp đồng lao động; số lượng và tình trạng hợp đồng của một nhân viên được truy vấn từ `03-labor-contract.md` qua `EmployeeId`, không giới hạn cứng ở 3 chu kỳ như thiết kế cũ. |

> Lưu ý đánh số: dòng "PersonalTaxCode..." ở trên tương ứng **BR-19**.

### 1.7 Đề xuất DB Schema

Quy ước theo `00-domain-overview.md`: PascalCase, prefix `Emp_`, PK `Id`, FK `<Entity>Id`, soft delete `IsDeleted/DeletedAtUtc`, audit `CreatedAtUtc/CreatedBy/LastModifiedAtUtc/LastModifiedBy`.

```sql
-- ============================================================
-- Emp_Department — Phòng ban
-- ============================================================
CREATE TABLE Emp_Department (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    DepartmentCode      NVARCHAR(20)     NOT NULL,
    DepartmentName      NVARCHAR(200)    NOT NULL,
    DivisionId          UNIQUEIDENTIFIER NOT NULL,
    ParentDepartmentId  UNIQUEIDENTIFIER NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    DeletedAtUtc        DATETIME2        NULL,
    CreatedAtUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NOT NULL,
    LastModifiedAtUtc   DATETIME2        NULL,
    LastModifiedBy      UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Emp_Department_Parent FOREIGN KEY (ParentDepartmentId) REFERENCES Emp_Department(Id)
);
CREATE UNIQUE INDEX UQ_Emp_Department_Code ON Emp_Department(DivisionId, DepartmentCode) WHERE IsDeleted = 0;

-- ============================================================
-- Emp_Team — Tổ/Nhóm
-- ============================================================
CREATE TABLE Emp_Team (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    TeamCode            NVARCHAR(20)     NOT NULL,
    TeamName            NVARCHAR(200)    NOT NULL,
    DepartmentId        UNIQUEIDENTIFIER NOT NULL,
    DivisionId          UNIQUEIDENTIFIER NOT NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    Notes               NVARCHAR(200)    NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    DeletedAtUtc        DATETIME2        NULL,
    CreatedAtUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NOT NULL,
    LastModifiedAtUtc   DATETIME2        NULL,
    LastModifiedBy      UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Emp_Team_Department FOREIGN KEY (DepartmentId) REFERENCES Emp_Department(Id)
);
CREATE UNIQUE INDEX UQ_Emp_Team_Code ON Emp_Team(DivisionId, TeamCode) WHERE IsDeleted = 0;
CREATE INDEX IX_Emp_Team_DepartmentId ON Emp_Team(DepartmentId);

-- ============================================================
-- Emp_Duty — Chức danh (dùng bởi Approval Engine, xem 05-request-approval.md)
-- ============================================================
CREATE TABLE Emp_Duty (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    DutyCode            NVARCHAR(20)     NOT NULL,
    DutyName            NVARCHAR(200)    NOT NULL,
    DivisionId          UNIQUEIDENTIFIER NOT NULL,
    IsActive            BIT              NOT NULL DEFAULT 1,
    IsDeleted           BIT              NOT NULL DEFAULT 0,
    DeletedAtUtc        DATETIME2        NULL,
    CreatedAtUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy           UNIQUEIDENTIFIER NOT NULL,
    LastModifiedAtUtc   DATETIME2        NULL,
    LastModifiedBy      UNIQUEIDENTIFIER NULL
);
CREATE UNIQUE INDEX UQ_Emp_Duty_Code ON Emp_Duty(DivisionId, DutyCode) WHERE IsDeleted = 0;

-- ============================================================
-- Emp_Employee — Aggregate Root
-- ============================================================
CREATE TABLE Emp_Employee (
    Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeCode                NVARCHAR(20)     NOT NULL,
    SeasonalEmployeeCode        NVARCHAR(20)     NULL,
    DivisionId                  UNIQUEIDENTIFIER NOT NULL,
    LastName                    NVARCHAR(50)     NOT NULL,
    MiddleName                  NVARCHAR(50)     NULL,
    FirstName                   NVARCHAR(50)     NOT NULL,
    FullName                    NVARCHAR(150)    NOT NULL,
    DateOfBirth                 DATE             NOT NULL,
    BirthPlace                  NVARCHAR(200)    NULL,
    Gender                      TINYINT          NOT NULL,
    MaritalStatus               TINYINT          NULL,
    -- VO: PermanentAddress
    PermanentAddressText        NVARCHAR(300)    NULL,
    PermanentProvinceId         UNIQUEIDENTIFIER NULL,
    PermanentDistrictId         UNIQUEIDENTIFIER NULL,
    PermanentWardId             UNIQUEIDENTIFIER NULL,
    HouseholdOwnerName          NVARCHAR(150)    NULL,
    -- VO: TemporaryAddress
    TemporaryAddressText        NVARCHAR(300)    NULL,
    TemporaryProvinceId         UNIQUEIDENTIFIER NULL,
    TemporaryDistrictId         UNIQUEIDENTIFIER NULL,
    TemporaryWardId             UNIQUEIDENTIFIER NULL,
    EthnicityId                 UNIQUEIDENTIFIER NULL,
    ReligionId                  UNIQUEIDENTIFIER NULL,
    NationalityId                UNIQUEIDENTIFIER NOT NULL,
    NativeCountry                NVARCHAR(100)    NULL,
    PoliticalStatusId            UNIQUEIDENTIFIER NULL,
    EducationLevelId             UNIQUEIDENTIFIER NULL,
    SpecialEducationId           UNIQUEIDENTIFIER NULL,
    InformaticsLevel             NVARCHAR(50)     NULL,
    Language1Id                  UNIQUEIDENTIFIER NULL,
    Language2Id                  UNIQUEIDENTIFIER NULL,
    Language3Id                  UNIQUEIDENTIFIER NULL,
    Language4Id                  UNIQUEIDENTIFIER NULL,
    LanguageLevel1Id              UNIQUEIDENTIFIER NULL,
    LanguageLevel2Id              UNIQUEIDENTIFIER NULL,
    LanguageLevel3Id              UNIQUEIDENTIFIER NULL,
    LanguageLevel4Id              UNIQUEIDENTIFIER NULL,
    AvatarImageUrl                 NVARCHAR(500)    NULL,
    Email                          NVARCHAR(150)    NULL,
    HomePhone                      NVARCHAR(20)     NULL,
    HomeFax                        NVARCHAR(20)     NULL,
    Height                         DECIMAL(5,2)     NULL,
    Weight                         DECIMAL(5,2)     NULL,
    BloodGroup                     NVARCHAR(5)      NULL,
    LeftEyeVision                  NVARCHAR(10)     NULL,
    RightEyeVision                 NVARCHAR(10)     NULL,
    HealthStatus                   NVARCHAR(200)    NULL,
    SmokingStatus                  NVARCHAR(20)     NULL,
    Hobby                          NVARCHAR(500)    NULL,
    Strength                       NVARCHAR(500)    NULL,
    Weakness                       NVARCHAR(500)    NULL,
    PersonalAim                    NVARCHAR(500)    NULL,
    EmployeeWorkingStatus          TINYINT          NOT NULL,
    RecruitDate                    DATE             NOT NULL,
    ProbationStartDate             DATE             NULL,
    ProbationEndDate               DATE             NULL,
    TerminationDate                DATE             NULL,
    TerminationEffectiveToDate     DATE             NULL,
    QuitReasonId                   UNIQUEIDENTIFIER NULL,
    AbsentReason                   NVARCHAR(200)    NULL,
    StatusNotes                    NVARCHAR(200)    NULL,
    DutyId                         UNIQUEIDENTIFIER NULL,
    TitleId                        UNIQUEIDENTIFIER NULL,
    DepartmentId                   UNIQUEIDENTIFIER NOT NULL,
    TeamId                         UNIQUEIDENTIFIER NULL,
    SalaryCoefficient              DECIMAL(18,4)    NULL,
    DutyCoefficient                DECIMAL(18,4)    NULL,
    TimeCoefficient                DECIMAL(18,4)    NULL,
    BaseSalary                     DECIMAL(18,2)    NULL,
    InsuranceSalary                DECIMAL(18,2)    NULL,
    Salary01                       DECIMAL(18,2)    NULL,
    Salary02                       DECIMAL(18,2)    NULL,
    Salary03                       DECIMAL(18,2)    NULL,
    SalaryLevel                    NVARCHAR(20)     NULL,
    SalaryLevelDate                DATE             NULL,
    SocialInsuranceBookNo          NVARCHAR(20)     NULL,
    SocialInsuranceStartDate       DATE             NULL,
    HealthInsuranceCardNo          NVARCHAR(20)     NULL,
    HealthInsuranceFromDate        DATE             NULL,
    HealthInsuranceToDate          DATE             NULL,
    InitialHospitalId              UNIQUEIDENTIFIER NULL,
    -- VO: BankAccount
    BankId                         UNIQUEIDENTIFIER NULL,
    BankAccountNo                  NVARCHAR(30)     NULL,
    BankBranchAddress              NVARCHAR(200)    NULL,
    PersonalTaxCode                NVARCHAR(20)     NULL,
    TaxRegisteredDate              DATE             NULL,
    TaxRegisteredAddress           NVARCHAR(200)    NULL,
    DependentsCountCache           INT              NOT NULL DEFAULT 0,
    IsDormitory                    BIT              NOT NULL DEFAULT 0,
    DormitoryInfo                  NVARCHAR(200)    NULL,
    DormitoryFromDate              DATE             NULL,
    DormitoryToDate                DATE             NULL,
    TransportMode                  NVARCHAR(50)     NULL,
    LicensePlateNo                 NVARCHAR(20)     NULL,
    CommuteRoute                   NVARCHAR(200)    NULL,
    PickupPoint                    NVARCHAR(200)    NULL,
    IsDrivingLicense                BIT              NOT NULL DEFAULT 0,
    DrivingLicenseNo                NVARCHAR(20)     NULL,
    ExpenseAccountId                 UNIQUEIDENTIFIER NULL,
    PayableAccountId                 UNIQUEIDENTIFIER NULL,
    PreviousEmployeeId               UNIQUEIDENTIFIER NULL,
    IsUserMes                        BIT              NOT NULL DEFAULT 0,
    IsAutoCreateUser                 BIT              NOT NULL DEFAULT 0,
    IsActive                         BIT              NOT NULL DEFAULT 1,
    Notes                            NVARCHAR(500)    NULL,
    IsDeleted                        BIT              NOT NULL DEFAULT 0,
    DeletedAtUtc                     DATETIME2        NULL,
    CreatedAtUtc                     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy                        UNIQUEIDENTIFIER NOT NULL,
    LastModifiedAtUtc                DATETIME2        NULL,
    LastModifiedBy                   UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Emp_Employee_Department FOREIGN KEY (DepartmentId) REFERENCES Emp_Department(Id),
    CONSTRAINT FK_Emp_Employee_Team FOREIGN KEY (TeamId) REFERENCES Emp_Team(Id),
    CONSTRAINT FK_Emp_Employee_Duty FOREIGN KEY (DutyId) REFERENCES Emp_Duty(Id),
    CONSTRAINT FK_Emp_Employee_PreviousEmployee FOREIGN KEY (PreviousEmployeeId) REFERENCES Emp_Employee(Id),
    CONSTRAINT CK_Emp_Employee_Termination CHECK (TerminationDate IS NULL OR TerminationDate >= RecruitDate),
    CONSTRAINT CK_Emp_Employee_Probation CHECK (ProbationEndDate IS NULL OR ProbationStartDate IS NULL OR ProbationEndDate >= ProbationStartDate),
    CONSTRAINT CK_Emp_Employee_Bank CHECK ((BankId IS NULL AND BankAccountNo IS NULL) OR (BankId IS NOT NULL AND BankAccountNo IS NOT NULL))
);
CREATE UNIQUE INDEX UQ_Emp_Employee_Code ON Emp_Employee(DivisionId, EmployeeCode) WHERE IsDeleted = 0;
CREATE INDEX IX_Emp_Employee_DepartmentId ON Emp_Employee(DepartmentId);
CREATE INDEX IX_Emp_Employee_TeamId ON Emp_Employee(TeamId);
CREATE INDEX IX_Emp_Employee_WorkingStatus ON Emp_Employee(EmployeeWorkingStatus);
CREATE INDEX IX_Emp_Employee_FullName ON Emp_Employee(FullName);

-- ============================================================
-- Emp_EmployeeDocument — Giấy tờ định danh (CCCD/CMND/Hộ chiếu/GPLX)
-- ============================================================
CREATE TABLE Emp_EmployeeDocument (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeId        UNIQUEIDENTIFIER NOT NULL,
    DocumentType      TINYINT          NOT NULL,
    DocumentNo        NVARCHAR(30)     NOT NULL,
    IssuedDate        DATE             NULL,
    IssuedPlace       NVARCHAR(200)    NULL,
    IssuedCityId      UNIQUEIDENTIFIER NULL,
    ExpiryDate        DATE             NULL,
    IsCurrent         BIT              NOT NULL DEFAULT 1,
    IsDeleted         BIT              NOT NULL DEFAULT 0,
    DeletedAtUtc      DATETIME2        NULL,
    CreatedAtUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy         UNIQUEIDENTIFIER NOT NULL,
    LastModifiedAtUtc DATETIME2        NULL,
    LastModifiedBy    UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Emp_EmployeeDocument_Employee FOREIGN KEY (EmployeeId) REFERENCES Emp_Employee(Id),
    CONSTRAINT CK_Emp_EmployeeDocument_Expiry CHECK (ExpiryDate IS NULL OR IssuedDate IS NULL OR ExpiryDate > IssuedDate)
);
CREATE INDEX IX_Emp_EmployeeDocument_EmployeeId ON Emp_EmployeeDocument(EmployeeId);
CREATE INDEX IX_Emp_EmployeeDocument_DocumentNo ON Emp_EmployeeDocument(DocumentType, DocumentNo) WHERE IsDeleted = 0;

-- ============================================================
-- Emp_FamilyMember — Gia đình / Người phụ thuộc
-- ============================================================
CREATE TABLE Emp_FamilyMember (
    Id                       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeId               UNIQUEIDENTIFIER NOT NULL,
    RelationType              TINYINT          NOT NULL,
    FullName                  NVARCHAR(150)    NOT NULL,
    YearOfBirth                INT              NULL,
    Occupation                 NVARCHAR(100)    NULL,
    Address                    NVARCHAR(200)    NULL,
    PhoneNumber                 NVARCHAR(20)     NULL,
    IsDeceased                  BIT              NOT NULL DEFAULT 0,
    IsDependentForTax           BIT              NOT NULL DEFAULT 0,
    DependentRegisteredDate      DATE             NULL,
    Notes                        NVARCHAR(200)    NULL,
    IsDeleted                    BIT              NOT NULL DEFAULT 0,
    DeletedAtUtc                 DATETIME2        NULL,
    CreatedAtUtc                 DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy                    UNIQUEIDENTIFIER NOT NULL,
    LastModifiedAtUtc            DATETIME2        NULL,
    LastModifiedBy                UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Emp_FamilyMember_Employee FOREIGN KEY (EmployeeId) REFERENCES Emp_Employee(Id)
);
CREATE INDEX IX_Emp_FamilyMember_EmployeeId ON Emp_FamilyMember(EmployeeId);

-- ============================================================
-- Emp_EmployeeTarget — KPI/Mục tiêu
-- ============================================================
CREATE TABLE Emp_EmployeeTarget (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeId    UNIQUEIDENTIFIER NOT NULL,
    TargetId      UNIQUEIDENTIFIER NOT NULL,
    TargetAmount  DECIMAL(18,2)    NULL,
    PeriodYear    INT              NULL,
    Notes         NVARCHAR(200)    NULL,
    CreatedAtUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy     UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT FK_Emp_EmployeeTarget_Employee FOREIGN KEY (EmployeeId) REFERENCES Emp_Employee(Id)
);
CREATE INDEX IX_Emp_EmployeeTarget_EmployeeId ON Emp_EmployeeTarget(EmployeeId);

-- ============================================================
-- Emp_EmployeeAdditionalInfo — Thông tin bổ sung tự do (EAV, thay DP01-27)
-- ============================================================
CREATE TABLE Emp_EmployeeAdditionalInfo (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeId    UNIQUEIDENTIFIER NOT NULL,
    FieldCode     NVARCHAR(20)     NOT NULL,
    FieldLabel    NVARCHAR(100)    NULL,
    FieldValue    NVARCHAR(500)    NULL,
    DisplayOrder  INT              NULL,
    CONSTRAINT FK_Emp_EmployeeAdditionalInfo_Employee FOREIGN KEY (EmployeeId) REFERENCES Emp_Employee(Id)
);
CREATE UNIQUE INDEX UQ_Emp_EmployeeAdditionalInfo ON Emp_EmployeeAdditionalInfo(EmployeeId, FieldCode);

-- ============================================================
-- Emp_EmployeeWorkHistory — Lịch sử làm việc trước khi vào công ty
-- ============================================================
CREATE TABLE Emp_EmployeeWorkHistory (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeId    UNIQUEIDENTIFIER NOT NULL,
    CompanyName   NVARCHAR(200)    NOT NULL,
    TitleName     NVARCHAR(100)    NULL,
    FromDate      DATE             NULL,
    ToDate        DATE             NULL,
    CONSTRAINT FK_Emp_EmployeeWorkHistory_Employee FOREIGN KEY (EmployeeId) REFERENCES Emp_Employee(Id)
);
CREATE INDEX IX_Emp_EmployeeWorkHistory_EmployeeId ON Emp_EmployeeWorkHistory(EmployeeId);

-- ============================================================
-- Emp_Attachment — Tệp đính kèm hồ sơ (thay HRMT2182)
-- ============================================================
CREATE TABLE Emp_Attachment (
    Id             UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    EmployeeId     UNIQUEIDENTIFIER NOT NULL,
    Category       TINYINT          NOT NULL,
    FileName       NVARCHAR(255)    NOT NULL,
    FileUrl        NVARCHAR(500)    NOT NULL,
    FileType       NVARCHAR(20)     NULL,
    FileSizeBytes  BIGINT           NULL,
    UploadedAtUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    UploadedBy     UNIQUEIDENTIFIER NOT NULL,
    IsDeleted      BIT              NOT NULL DEFAULT 0,
    DeletedAtUtc   DATETIME2        NULL,
    CONSTRAINT FK_Emp_Attachment_Employee FOREIGN KEY (EmployeeId) REFERENCES Emp_Employee(Id)
);
CREATE INDEX IX_Emp_Attachment_EmployeeId ON Emp_Attachment(EmployeeId);
```

### 1.8 Quan hệ với bounded context khác

| Module | Chiều phụ thuộc | Mô tả |
|---|---|---|
| `02-recruitment.md` | Recruitment → Employee Profile | Ứng viên trúng tuyển sinh ra `Employee` mới (`EmployeeCreated`), không kế thừa `Candidate` trực tiếp |
| `03-labor-contract.md` | Employee Profile ← Labor Contract | 1 `Employee` có N `LaborContract` theo thời gian; Employee Profile chỉ hiển thị hợp đồng hiện hành qua truy vấn liên module |
| `04-timekeeping-shift.md` | Employee Profile → Timekeeping | Chấm công gắn với `EmployeeId`; `EmployeeStatusChanged` (Terminated) dừng chấm công |
| `05-request-approval.md` | Employee Profile → Request & Approval | Đơn từ có `EmployeeId` là người yêu cầu; cấp duyệt tra cứu qua `DepartmentId/TitleId` của `Employee` |
| `06-payroll.md` | Employee Profile → Payroll | Payroll dùng `SalaryCoefficient/DutyCoefficient/TimeCoefficient/BaseSalary/InsuranceSalary` làm dữ liệu nền tính lương |
| `07-social-insurance.md` | Employee Profile → Social Insurance | Dùng `SocialInsuranceBookNo/HealthInsuranceCardNo/InitialHospitalId` |
| `08-personal-income-tax.md` | Employee Profile → Personal Income Tax | Dùng `PersonalTaxCode` + `FamilyMember.IsDependentForTax` (giảm trừ gia cảnh) |
| `09-training.md` | Employee Profile → Training | Đào tạo gắn `EmployeeId`, có thể phát sinh từ Recruitment (đào tạo hội nhập) |
| `10-maternity-benefits.md` | Employee Profile → Maternity Benefits | Dùng `Gender`, hợp đồng hiện hành (qua Labor Contract) |
| `11-personnel-decisions.md` | Personnel Decisions → Employee Profile | Quyết định bổ nhiệm/điều chuyển/thôi việc làm phát sinh `EmployeePositionChanged`/`EmployeeStatusChanged` |
| MES (external system) | Employee Profile → MES (ACL) | `EmployeeSyncedToMes` đồng bộ 1 chiều user MES từ `HRMF2211MES` legacy |

---

## PHẦN 2 — BDD (Behavior-Driven Development)

### 2.1 User Story chính

- **US-01**: Là nhân viên phòng Nhân sự, tôi muốn tạo mới hồ sơ nhân viên để bắt đầu quản lý thông tin nhân sự trong hệ thống.
- **US-02**: Là nhân viên phòng Nhân sự, tôi muốn cập nhật thông tin hồ sơ nhân viên (cá nhân, nghề nghiệp, gia đình) khi có thay đổi thực tế.
- **US-03**: Là quản lý Nhân sự, tôi muốn thay đổi trạng thái làm việc của nhân viên (thử việc → chính thức → nghỉ việc...) để phản ánh đúng vòng đời lao động.
- **US-04**: Là nhân viên phòng Nhân sự, tôi muốn được cảnh báo khi nhập số CCCD trùng với nhân viên khác để tránh tạo hồ sơ trùng lặp.
- **US-05**: Là người dùng không có quyền xem lương, tôi không muốn nhìn thấy các trường lương của nhân viên khác để bảo vệ dữ liệu nhạy cảm.
- **US-06**: Là nhân viên phòng Nhân sự, tôi muốn xuất hồ sơ nhân viên ra Excel để in ấn/báo cáo.
- **US-07**: Là nhân viên phòng Nhân sự, tôi muốn quản lý thông tin gia đình và người phụ thuộc của nhân viên phục vụ tính giảm trừ gia cảnh thuế TNCN.
- **US-08**: Là hệ thống, tôi muốn tự động đồng bộ thông tin nhân viên đang hoạt động sang hệ thống MES để nhân viên có thể đăng nhập vận hành sản xuất.
- **US-09**: Là quản trị viên tổ chức, tôi muốn quản lý danh mục Phòng ban/Tổ để gán nhân viên đúng cấu trúc tổ chức.

### 2.2 Kịch bản Gherkin

```gherkin
Feature: Employee Profile Management
  Là nhân viên phòng Nhân sự
  Tôi muốn quản lý hồ sơ nhân viên
  Để duy trì dữ liệu nhân sự chính xác, làm nền tảng cho các nghiệp vụ khác

  # BR-01
  Scenario: Tạo hồ sơ nhân viên thành công với mã nhân viên duy nhất (BR-01)
    Given Division "MKVC" chưa có nhân viên nào với mã "NV0001"
    When Nhân viên phòng Nhân sự tạo hồ sơ mới với EmployeeCode "NV0001", họ tên "Nguyễn Văn A", ngày sinh "1995-05-10", giới tính "Male", quốc tịch "Việt Nam", ngày tuyển dụng "2026-07-13", phòng ban "Phòng Sản xuất"
    Then Hồ sơ nhân viên được tạo thành công
    And Sự kiện "EmployeeCreated" được phát sinh

  Scenario: Từ chối tạo hồ sơ khi mã nhân viên đã tồn tại trong cùng Division (BR-01)
    Given Division "MKVC" đã có nhân viên với mã "NV0001"
    When Nhân viên phòng Nhân sự tạo hồ sơ mới với EmployeeCode "NV0001" trong Division "MKVC"
    Then Hệ thống từ chối lưu với lỗi "Mã nhân viên đã tồn tại"

  # BR-02
  Scenario: Từ chối tạo hồ sơ khi thiếu trường bắt buộc (BR-02)
    When Nhân viên phòng Nhân sự tạo hồ sơ mới nhưng bỏ trống "DepartmentId"
    Then Hệ thống từ chối lưu với lỗi "DepartmentId là bắt buộc"

  # BR-03
  Scenario: Chặn tạo hồ sơ khi tuổi lao động dưới 15 tại thời điểm tuyển dụng (BR-03)
    Given Ứng viên có ngày sinh "2015-01-01"
    When Nhân viên phòng Nhân sự tạo hồ sơ với ngày tuyển dụng "2026-07-13"
    Then Hệ thống từ chối lưu với lỗi "Chưa đủ tuổi lao động tối thiểu"

  Scenario: Cảnh báo (không chặn) khi tuổi lao động từ 15 đến dưới 18 (BR-03)
    Given Ứng viên có ngày sinh "2010-01-01"
    When Nhân viên phòng Nhân sự tạo hồ sơ với ngày tuyển dụng "2026-07-13"
    Then Hệ thống hiển thị cảnh báo "Nhân viên chưa đủ 18 tuổi, cần xác nhận đồng ý của người đại diện"
    And Cho phép lưu hồ sơ sau khi xác nhận

  # BR-04
  Scenario: Từ chối khi số CCCD sai định dạng (BR-04)
    When Nhân viên phòng Nhân sự nhập số CCCD "ABC123"
    Then Hệ thống từ chối lưu với lỗi "Số CCCD/CMND không đúng định dạng"

  # BR-05
  Scenario: Cảnh báo trùng số CCCD với nhân viên khác đang hoạt động (BR-05)
    Given Nhân viên "Trần Thị B" (đang hoạt động) có số CCCD "079095001234"
    When Nhân viên phòng Nhân sự tạo hồ sơ mới với số CCCD "079095001234"
    Then Hệ thống hiển thị cảnh báo "Số CCCD đã tồn tại cho nhân viên Trần Thị B"
    And Hồ sơ chưa được lưu cho đến khi người dùng xác nhận bỏ qua cảnh báo

  Scenario: Cho phép lưu sau khi người dùng xác nhận bỏ qua cảnh báo trùng CCCD (BR-05)
    Given Hệ thống đã cảnh báo trùng số CCCD "079095001234"
    When Nhân viên phòng Nhân sự xác nhận "Vẫn lưu" (ForceSave = true)
    Then Hồ sơ nhân viên được lưu thành công

  # BR-06
  Scenario: Từ chối khi ngày hết hạn hộ chiếu không lớn hơn ngày cấp (BR-06)
    When Nhân viên phòng Nhân sự nhập hộ chiếu có ngày cấp "2026-01-01" và ngày hết hạn "2025-01-01"
    Then Hệ thống từ chối lưu với lỗi "Ngày hết hạn phải sau ngày cấp"

  # BR-07
  Scenario: Từ chối khi ngày nghỉ việc trước ngày tuyển dụng (BR-07)
    Given Nhân viên "NV0001" có ngày tuyển dụng "2020-01-01"
    When Nhân viên phòng Nhân sự nhập ngày nghỉ việc "2019-12-31"
    Then Hệ thống từ chối lưu với lỗi "Ngày nghỉ việc phải sau ngày tuyển dụng"

  # BR-08
  Scenario: Chuyển trạng thái nhân viên sang Terminated yêu cầu ngày nghỉ việc (BR-08)
    Given Nhân viên "NV0001" đang ở trạng thái "Active"
    When Quản lý Nhân sự chuyển trạng thái sang "Terminated" mà không nhập ngày nghỉ việc
    Then Hệ thống từ chối với lỗi "Ngày nghỉ việc là bắt buộc khi chấm dứt lao động"

  Scenario: Chuyển trạng thái nhân viên sang Terminated thành công phát sinh sự kiện (BR-08)
    Given Nhân viên "NV0001" đang ở trạng thái "Active"
    When Quản lý Nhân sự chuyển trạng thái sang "Terminated" với ngày nghỉ việc "2026-07-13"
    Then Trạng thái nhân viên được cập nhật thành "Terminated"
    And Sự kiện "EmployeeTerminated" được phát sinh

  # BR-09
  Scenario: Từ chối khi ngày kết thúc thử việc trước ngày bắt đầu (BR-09)
    When Nhân viên phòng Nhân sự nhập thử việc từ "2026-08-01" đến "2026-07-01"
    Then Hệ thống từ chối lưu với lỗi "Ngày kết thúc thử việc phải sau ngày bắt đầu"

  Scenario: Cảnh báo khi thời gian thử việc vượt trần cấu hình (BR-09)
    Given Cấu hình trần thử việc cho vị trí "Công nhân sản xuất" là 6 ngày
    When Nhân viên phòng Nhân sự nhập thử việc từ "2026-07-01" đến "2026-07-20"
    Then Hệ thống hiển thị cảnh báo "Thời gian thử việc vượt quá quy định cho phép"

  # BR-10
  Scenario: Người dùng không có quyền ViewSalary không nhìn thấy lương cơ bản (BR-10)
    Given Người dùng "manager01" không có quyền "Employee.ViewSalary"
    When "manager01" xem hồ sơ nhân viên "NV0001"
    Then Trường "BaseSalary" trả về giá trị rỗng/ẩn
    And Các trường "InsuranceSalary", "Salary01", "Salary02", "Salary03" cũng bị ẩn

  Scenario: Người dùng có quyền ViewSalary xem được lương cơ bản (BR-10)
    Given Người dùng "hr01" có quyền "Employee.ViewSalary"
    When "hr01" xem hồ sơ nhân viên "NV0001"
    Then Trường "BaseSalary" hiển thị đúng giá trị đã lưu

  # BR-11
  Scenario: Từ chối khi Team không thuộc Department đã chọn (BR-11)
    Given Team "Tổ 1" thuộc Department "Phòng Sản xuất"
    When Nhân viên phòng Nhân sự chọn Department "Phòng Kế toán" và Team "Tổ 1"
    Then Hệ thống từ chối lưu với lỗi "Tổ/Nhóm không thuộc phòng ban đã chọn"

  # BR-12
  Scenario: Từ chối khi chỉ nhập số tài khoản mà không chọn ngân hàng (BR-12)
    When Nhân viên phòng Nhân sự nhập "BankAccountNo" mà không chọn "BankId"
    Then Hệ thống từ chối lưu với lỗi "Vui lòng chọn ngân hàng tương ứng với số tài khoản"

  # BR-13
  Scenario: Từ chối xoá cứng hồ sơ nhân viên đang có hợp đồng hiệu lực (BR-13)
    Given Nhân viên "NV0001" đang có hợp đồng lao động còn hiệu lực
    When Quản lý Nhân sự thực hiện xoá hồ sơ "NV0001"
    Then Hệ thống từ chối với lỗi "Không thể xoá nhân viên đang có dữ liệu giao dịch hoạt động"

  Scenario: Xoá mềm hồ sơ nhân viên hợp lệ (BR-13)
    Given Nhân viên "NV0002" không còn dữ liệu giao dịch hoạt động nào tham chiếu
    When Quản lý Nhân sự thực hiện xoá hồ sơ "NV0002"
    Then Hồ sơ được đánh dấu "IsDeleted = true" và "DeletedAtUtc" được ghi nhận
    And Hồ sơ không còn xuất hiện trong danh sách tra cứu mặc định

  # BR-14
  Scenario: Số người phụ thuộc tự động cập nhật khi thêm người phụ thuộc mới (BR-14)
    Given Nhân viên "NV0001" hiện có "DependentsCountCache = 1"
    When Nhân viên phòng Nhân sự thêm 1 "FamilyMember" mới với "IsDependentForTax = true"
    Then "DependentsCountCache" của nhân viên "NV0001" được cập nhật thành 2
    And Sự kiện "EmployeeDependentRegistered" được phát sinh

  # BR-15
  Scenario: Không cho phép chuyển trạng thái từ Terminated về Active (BR-15)
    Given Nhân viên "NV0003" đang ở trạng thái "Terminated"
    When Quản lý Nhân sự cố gắng chuyển trạng thái "NV0003" sang "Active"
    Then Hệ thống từ chối với lỗi "Không thể khôi phục trạng thái từ Terminated, vui lòng tạo hồ sơ tuyển lại"

  Scenario: Tuyển lại nhân viên đã nghỉ việc tạo hồ sơ mới liên kết hồ sơ cũ (BR-15)
    Given Nhân viên "NV0003" đang ở trạng thái "Terminated"
    When Nhân viên phòng Nhân sự tạo hồ sơ mới "NV0099" với "PreviousEmployeeId = NV0003.Id"
    Then Hồ sơ mới "NV0099" được tạo với trạng thái "Probation" hoặc "Active"
    And Hồ sơ mới lưu liên kết tới hồ sơ cũ "NV0003"

  # BR-16
  Scenario: Từ chối khi mã nhân viên thời vụ trùng với mã nhân viên chính thức đã tồn tại (BR-16)
    Given Đã tồn tại nhân viên chính thức với mã "NV0001"
    When Nhân viên phòng Nhân sự tạo hồ sơ thời vụ với "SeasonalEmployeeCode = NV0001"
    Then Hệ thống từ chối lưu với lỗi "Mã nhân viên thời vụ bị trùng"

  # BR-17
  Scenario: Xuất hồ sơ Excel ẩn cột lương với người không có quyền xem lương (BR-17)
    Given Người dùng "manager01" không có quyền "Employee.ViewSalary"
    When "manager01" thực hiện xuất hồ sơ nhân viên "NV0001" ra Excel
    Then Tệp Excel xuất ra không chứa cột "BaseSalary/InsuranceSalary/Salary01-03" hoặc để trống các cột này

  Scenario: Xuất hồ sơ Excel đầy đủ với người có quyền xem lương (BR-17)
    Given Người dùng "hr01" có quyền "Employee.ViewSalary"
    When "hr01" thực hiện xuất hồ sơ nhân viên "NV0001" ra Excel
    Then Tệp Excel xuất ra chứa đầy đủ cột lương với giá trị chính xác

  # BR-18
  Scenario: Từ chối tải lên ảnh đại diện vượt quá dung lượng cho phép (BR-18)
    When Nhân viên phòng Nhân sự tải lên ảnh đại diện dung lượng 8MB
    Then Hệ thống từ chối với lỗi "Dung lượng tệp vượt quá giới hạn cho phép (5MB)"

  # BR-19
  Scenario: Từ chối khi mã số thuế cá nhân sai định dạng (BR-19)
    When Nhân viên phòng Nhân sự nhập "PersonalTaxCode" là "12345"
    Then Hệ thống từ chối lưu với lỗi "Mã số thuế cá nhân không đúng định dạng"

  # BR-20
  Scenario: Hồ sơ nhân viên không lưu chi tiết hợp đồng, chỉ tham chiếu số lượng hợp đồng (BR-20)
    Given Nhân viên "NV0001" có 4 hợp đồng lao động được quản lý tại module Labor Contract
    When Nhân viên phòng Nhân sự xem hồ sơ "NV0001"
    Then Hệ thống hiển thị "Số hợp đồng: 4" bằng cách truy vấn liên module tới Labor Contract
    And Hồ sơ "Employee" không có cột lưu trực tiếp chi tiết từng hợp đồng


Feature: Employee-MES Integration
  Là hệ thống HRM
  Tôi muốn đồng bộ thông tin nhân viên sang hệ thống MES
  Để nhân viên xưởng sản xuất có thể đăng nhập và thao tác trên MES

  Scenario: Đồng bộ nhân viên đang hoạt động sang MES khi tạo hồ sơ mới
    Given Nhân viên "NV0001" được tạo với "IsUserMes = true" và "IsActive = true"
    When Hồ sơ được lưu thành công
    Then Sự kiện "EmployeeSyncedToMes" được phát sinh với payload UserId="NV0001"

  Scenario: Không đồng bộ nhân viên không đánh dấu là user MES
    Given Nhân viên "NV0002" được tạo với "IsUserMes = false"
    When Hồ sơ được lưu thành công
    Then Sự kiện "EmployeeSyncedToMes" không được phát sinh


Feature: Organization Unit Management
  Là quản trị viên tổ chức
  Tôi muốn quản lý Phòng ban/Tổ
  Để gán nhân viên đúng cấu trúc tổ chức

  Scenario: Tạo phòng ban mới thành công
    When Quản trị viên tạo Department với DepartmentCode "PSX", DepartmentName "Phòng Sản xuất"
    Then Department được tạo thành công

  Scenario: Tạo Team gắn với Department hợp lệ
    Given Department "PSX" đã tồn tại
    When Quản trị viên tạo Team "To1" thuộc Department "PSX"
    Then Team được tạo thành công và liên kết đúng DepartmentId
```

---

## PHẦN 3 — TDD (Test-Driven Development)

### 3.1 Bảng Test Case

| TC-xx | Scenario liên quan | Loại test | Input | Expected Output |
|---|---|---|---|---|
| TC-01 | Tạo hồ sơ thành công (BR-01) | Unit | `EmployeeCode="NV0001", DivisionId=X` (chưa tồn tại) | `Employee` được tạo, event `EmployeeCreated` phát sinh |
| TC-02 | Từ chối trùng mã nhân viên (BR-01) | Unit | `EmployeeCode="NV0001"` đã tồn tại trong `DivisionId=X` | `DuplicateEmployeeCodeException` |
| TC-03 | Thiếu trường bắt buộc (BR-02) | Unit | `DepartmentId = null` | `ValidationException("DepartmentId required")` |
| TC-04 | Chặn dưới 15 tuổi (BR-03) | Unit | `DateOfBirth=2015-01-01, RecruitDate=2026-07-13` | `ValidationException("Underage")` |
| TC-05 | Cảnh báo 15-18 tuổi (BR-03) | Unit | `DateOfBirth=2010-01-01, RecruitDate=2026-07-13` | Kết quả validate = `Warning`, không throw |
| TC-06 | Sai định dạng CCCD (BR-04) | Unit | `DocumentNo="ABC123", DocumentType=NationalId` | `ValidationException("InvalidFormat")` |
| TC-07 | CCCD hợp lệ 12 số (BR-04) | Unit | `DocumentNo="079095001234"` | Validate pass |
| TC-08 | Cảnh báo trùng CCCD (BR-05) | Integration | `DocumentNo` đã tồn tại ở employee khác `IsActive=true` | `DuplicateIdentityWarning` trả về, `ForceSave=false` → chưa lưu |
| TC-09 | Bỏ qua cảnh báo trùng CCCD (BR-05) | Integration | Như TC-08 nhưng `ForceSave=true` | Lưu thành công |
| TC-10 | Ngày hết hạn hộ chiếu không hợp lệ (BR-06) | Unit | `IssuedDate=2026-01-01, ExpiryDate=2025-01-01` | `ValidationException` |
| TC-11 | Ngày nghỉ việc trước ngày tuyển dụng (BR-07) | Unit | `RecruitDate=2020-01-01, TerminationDate=2019-12-31` | `ValidationException` |
| TC-12 | Terminate thiếu ngày nghỉ việc (BR-08) | Unit | `NewStatus=Terminated, TerminationDate=null` | `ValidationException` |
| TC-13 | Terminate hợp lệ phát sinh event (BR-08) | Unit | `NewStatus=Terminated, TerminationDate=2026-07-13` | `EmployeeTerminated` event, status cập nhật |
| TC-14 | Thử việc end < start (BR-09) | Unit | `ProbationStartDate=2026-08-01, ProbationEndDate=2026-07-01` | `ValidationException` |
| TC-15 | Thử việc vượt trần cấu hình (BR-09) | Unit | Trần=6 ngày, thực tế=19 ngày | Kết quả `Warning` |
| TC-16 | Ẩn lương khi không có quyền (BR-10) | Unit | `UserPermissions` không có `Employee.ViewSalary` | `BaseSalary=null` trong DTO trả về |
| TC-17 | Hiển thị lương khi có quyền (BR-10) | Unit | `UserPermissions` có `Employee.ViewSalary` | `BaseSalary` = giá trị gốc |
| TC-18 | Team không thuộc Department (BR-11) | Unit | `TeamId` thuộc `DepartmentId != DepartmentId đã chọn` | `ValidationException` |
| TC-19 | Bank account thiếu BankId (BR-12) | Unit | `BankAccountNo="123", BankId=null` | `ValidationException` |
| TC-20 | Xoá cứng khi còn hợp đồng hiệu lực (BR-13) | Integration | `Employee` có `LaborContract` active | `InvalidOperationException` |
| TC-21 | Soft-delete hợp lệ (BR-13) | Unit | `Employee` không có ràng buộc | `IsDeleted=true, DeletedAtUtc` set |
| TC-22 | Đồng bộ số người phụ thuộc (BR-14) | Unit | Thêm `FamilyMember(IsDependentForTax=true)` | `DependentsCountCache += 1`, event `EmployeeDependentRegistered` |
| TC-23 | Chặn Terminated → Active (BR-15) | Unit | `CurrentStatus=Terminated, NewStatus=Active` | `InvalidStateTransitionException` |
| TC-24 | Tuyển lại nhân viên cũ (BR-15) | Integration | Tạo `Employee` mới với `PreviousEmployeeId` | Hồ sơ mới tạo thành công, liên kết đúng |
| TC-25 | Trùng mã nhân viên thời vụ (BR-16) | Unit | `SeasonalEmployeeCode` trùng `EmployeeCode` đã có | `ValidationException` |
| TC-26 | Export Excel ẩn cột lương (BR-17) | Integration | User không có quyền `ViewSalary` | File Excel không chứa dữ liệu lương |
| TC-27 | Export Excel đầy đủ (BR-17) | Integration | User có quyền `ViewSalary` | File Excel chứa đầy đủ dữ liệu lương |
| TC-28 | Upload avatar vượt dung lượng (BR-18) | Unit | `FileSizeBytes = 8_000_000` | `ValidationException("FileTooLarge")` |
| TC-29 | Mã số thuế sai định dạng (BR-19) | Unit | `PersonalTaxCode="12345"` | `ValidationException` |
| TC-30 | Đếm hợp đồng qua liên module (BR-20) | Integration | Gọi `ILaborContractQueryService.CountByEmployeeId` | Trả về số lượng đúng, không có cột chi tiết hợp đồng trong `Emp_Employee` |
| TC-31 | Đồng bộ MES khi IsUserMes=true | Integration | `IsUserMes=true, IsActive=true` | Event `EmployeeSyncedToMes` phát sinh với đúng payload |
| TC-32 | Không đồng bộ MES khi IsUserMes=false | Unit | `IsUserMes=false` | Không phát sinh event |
| TC-33 | Tạo Team hợp lệ | Unit | `DepartmentId` tồn tại | `Team` tạo thành công |
| TC-34 | Tạo Team với Department không tồn tại | Unit | `DepartmentId` không tồn tại | `ValidationException` |

### 3.2 Gợi ý chữ ký class/method

```csharp
// ===== Domain Model =====
public class Employee : AggregateRoot<Guid>
{
    public string EmployeeCode { get; private set; }
    public Guid DivisionId { get; private set; }
    public PersonName Name { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public EmployeeWorkingStatus WorkingStatus { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid? TeamId { get; private set; }
    public int DependentsCountCache { get; private set; }

    private readonly List<EmployeeDocument> _documents = new();
    private readonly List<FamilyMember> _familyMembers = new();

    public static Employee Create(CreateEmployeeCommand command, IEmployeeUniquenessChecker uniquenessChecker);

    public void UpdateProfile(UpdateEmployeeProfileCommand command);

    public void ChangeStatus(EmployeeWorkingStatus newStatus, DateOnly effectiveDate, Guid? reasonId);

    public void ChangePosition(Guid newDepartmentId, Guid? newTeamId, Guid? newDutyId, Guid? newTitleId, DateOnly effectiveDate, ITeamDepartmentValidator validator);

    public void AddDocument(EmployeeDocument document, IDuplicateIdentityChecker duplicateChecker, bool forceSave);

    public void AddFamilyMember(FamilyMember member);

    public void RemoveFamilyMember(Guid familyMemberId);

    public void MarkForDeletion(IEmployeeDeletionGuard deletionGuard);
}

// ===== Value Objects =====
public sealed record PersonName(string LastName, string? MiddleName, string FirstName)
{
    public string FullName => string.Join(" ", new[] { LastName, MiddleName, FirstName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed record Address(string? FullAddressText, Guid? ProvinceId, Guid? DistrictId, Guid? WardId, string? HouseholdOwnerName);

public sealed record BankAccount(Guid? BankId, string? AccountNo, string? BranchAddress)
{
    public bool IsValid() => (BankId is null && AccountNo is null) || (BankId is not null && AccountNo is not null);
}

// ===== Domain Services =====
public interface IEmployeeUniquenessChecker
{
    Task<bool> ExistsByCodeAsync(Guid divisionId, string employeeCode, CancellationToken ct);
    Task<bool> ExistsBySeasonalCodeConflictAsync(Guid divisionId, string seasonalCode, CancellationToken ct);
}

public interface IDuplicateIdentityChecker
{
    Task<Employee?> FindActiveByIdentityNumberAsync(string documentNo, Guid excludeEmployeeId, CancellationToken ct);
}

public interface ITeamDepartmentValidator
{
    Task<bool> IsTeamBelongsToDepartmentAsync(Guid teamId, Guid departmentId, CancellationToken ct);
}

public interface IEmployeeDeletionGuard
{
    Task<bool> HasActiveTransactionalDataAsync(Guid employeeId, CancellationToken ct);
}

public interface ILaborContractQueryService
{
    Task<int> CountByEmployeeIdAsync(Guid employeeId, CancellationToken ct);
}

public class EmployeeSalaryMaskingService
{
    public EmployeeDto ApplySalaryVisibility(EmployeeDto dto, ClaimsPrincipal user, IAuthorizationService authorizationService);
}

public class EmployeeMinimumAgeValidator
{
    public AgeValidationResult Validate(DateOnly dateOfBirth, DateOnly recruitDate);
    // Result: Blocked (<15), Warning (15-18), Ok (>=18)
}

public class EmployeeDossierExcelExporter
{
    public Task<byte[]> ExportAsync(Guid employeeId, ClaimsPrincipal requestingUser, CancellationToken ct);
}

// ===== Repository =====
public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Employee?> GetByCodeAsync(Guid divisionId, string employeeCode, CancellationToken ct);
    Task AddAsync(Employee employee, CancellationToken ct);
    Task UpdateAsync(Employee employee, CancellationToken ct);
}

// ===== Validator (FluentValidation style) =====
public class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(x => x.LastName).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.DateOfBirth).NotEmpty();
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.RecruitDate).NotEmpty();
        RuleFor(x => x.IdentityCardNo).Matches(@"^\d{9}$|^\d{12}$").When(x => !string.IsNullOrEmpty(x.IdentityCardNo));
        RuleFor(x => x.PersonalTaxCode).Matches(@"^\d{10}$|^\d{13}$").When(x => !string.IsNullOrEmpty(x.PersonalTaxCode));
    }
}
```

### 3.3 Edge case / Boundary value

- `DateOfBirth` đúng bằng ranh giới 15 năm trước `RecruitDate` (tính đến ngày) — biên giữa "chặn cứng" và "cảnh báo" (BR-03).
- `DateOfBirth` đúng bằng ranh giới 18 năm trước `RecruitDate` — biên giữa "cảnh báo" và "không cảnh báo" (BR-03).
- `IdentityCardNo` đúng 9 hoặc 12 chữ số (hợp lệ) vs 8/10/13 chữ số (không hợp lệ) (BR-04).
- Hai `EmployeeDocument` loại `NationalId` cùng `DocumentNo` nhưng thuộc 2 `Employee` khác nhau, một bên `IsActive=false` (đã nghỉ việc) — không cảnh báo trùng vì chỉ áp dụng cho nhân viên đang hoạt động (BR-05).
- `TerminationDate` đúng bằng `RecruitDate` (cùng ngày tuyển và nghỉ) — biên hợp lệ (BR-07, dùng `>=`).
- `ProbationEndDate` đúng bằng `ProbationStartDate` — biên hợp lệ (BR-09, dùng `>=`).
- Số ngày thử việc đúng bằng trần cấu hình — không cảnh báo; vượt 1 ngày — cảnh báo (BR-09).
- Người dùng có quyền `Employee.ViewSalary` nhưng đang xem hồ sơ chính mình vs hồ sơ người khác — kiểm tra scope quyền theo `EmployeeId` cụ thể nếu hệ thống áp dụng phân quyền theo phạm vi (row-level), không chỉ theo field (BR-10).
- `BankId` có giá trị nhưng `BankAccountNo` là chuỗi rỗng `""` (khác `null`) — cần chuẩn hoá `""` thành `null` trước khi kiểm tra BR-12.
- Xoá mềm một `Employee` đang là `PreviousEmployeeId` của một hồ sơ tuyển lại khác — cần đảm bảo không phá vỡ liên kết lịch sử (self-FK vẫn trỏ tới bản ghi đã soft-delete).
- `DependentsCountCache` bị lệch do thao tác cập nhật trực tiếp DB (ngoài domain service) — cần job đối soát định kỳ hoặc computed column thay vì cache tại một số triển khai.
- Chuyển trạng thái `Active → Suspended → Active` nhiều lần trong cùng ngày — kiểm tra không phát sinh trùng lặp `EmployeeStatusChanged` gây nhiễu cho Payroll/Timekeeping.
- `SeasonalEmployeeCode` trùng chính `EmployeeCode` của cùng một hồ sơ (tự trùng) — vẫn phải bị chặn theo BR-16.
- Export Excel cho danh sách nhiều nhân viên cùng lúc, trong đó một số nhân viên có quyền xem lương và một số không — áp dụng masking theo từng dòng dựa trên quyền của người xuất, không theo từng nhân viên được xuất (BR-17).
- Upload ảnh đại diện đúng bằng ngưỡng 5MB — biên hợp lệ; 5MB + 1 byte — bị từ chối (BR-18).
- `PersonalTaxCode` 10 số hợp lệ vs 13 số hợp lệ (có mã chi nhánh phụ thuộc) vs 11/12 số không hợp lệ (BR-19).
