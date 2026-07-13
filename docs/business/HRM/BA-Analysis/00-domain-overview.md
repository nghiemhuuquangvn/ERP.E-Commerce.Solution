# HRM Domain Overview — Tổng quan Nghiệp vụ & Kiến trúc Domain

> **Mục đích tài liệu**: Đây là bộ tài liệu phân tích nghiệp vụ (Business Analysis) cho module HRM, được viết lại từ hệ thống ASOFT ERP9 HRM hiện có (legacy, không có tài liệu gốc), theo chuẩn **DDD → BDD → TDD**. Bộ tài liệu này dùng làm **spec nguồn** để AI (Claude) sinh Database schema và code cho một hệ thống HRM mới. Mỗi file module cung cấp đủ 3 lớp thông tin: mô hình domain + business rule (DDD), kịch bản hành vi Gherkin (BDD), và danh sách test case + gợi ý chữ ký code (TDD).

## 1. Cách đọc tài liệu

Bộ tài liệu gồm 12 file trong `docs/BA-Analysis/`:

| File | Bounded Context | Mô tả ngắn |
|---|---|---|
| `00-domain-overview.md` | *(tài liệu này)* | Tổng quan, ubiquitous language, context map, shared kernel |
| `01-employee-profile.md` | Employee Profile | Hồ sơ nhân sự — aggregate trung tâm, mọi module khác tham chiếu qua `EmployeeId` |
| `02-recruitment.md` | Recruitment | Tuyển dụng: kế hoạch, đợt tuyển, ứng viên, phỏng vấn, quyết định tuyển dụng |
| `03-labor-contract.md` | Labor Contract | Hợp đồng lao động, vòng đời hợp đồng, ký số điện tử |
| `04-timekeeping-shift.md` | Timekeeping & Shift | Ca làm việc, chấm công (thiết bị/thủ công), quy đổi giờ công |
| `05-request-approval.md` | Request & Approval | Đơn từ nhân viên (nghỉ phép/OT/ra ngoài/bổ sung chấm công/đổi ca) + **Approval Engine dùng chung** |
| `06-payroll.md` | Payroll | Công thức lương, kỳ lương, tính lương |
| `07-social-insurance.md` | Social Insurance | BHXH/BHYT/BHTN — đăng ký, tính đóng, kế thừa kỳ |
| `08-personal-income-tax.md` | Personal Income Tax | Thuế TNCN — tạm khấu trừ, quyết toán |
| `09-training.md` | Training | Đào tạo: kế hoạch, khoá học, kết quả |
| `10-maternity-benefits.md` | Maternity Benefits | Chế độ thai sản theo luật lao động VN |
| `11-personnel-decisions.md` | Personnel Decisions | Quyết định bổ nhiệm/miễn nhiệm/điều chuyển/thôi việc |

Mỗi file `01`–`11` theo cấu trúc 3 phần cố định:
1. **DDD** — Bounded Context, Ubiquitous Language, Aggregate/Entity/Value Object, Domain Events, Business Rules (BR-xx), đề xuất DB Schema, quan hệ liên module.
2. **BDD** — User Story + kịch bản Gherkin (mỗi BR-xx có ít nhất 1 scenario).
3. **TDD** — Test case ánh xạ scenario, gợi ý chữ ký service/repository/validator.

## 2. Bối cảnh nghiệp vụ

Hệ thống HRM phục vụ doanh nghiệp sản xuất quy mô vừa/lớn (khảo sát từ code cho thấy có tuỳ biến riêng cho khách hàng "Meiko" — công ty sản xuất linh kiện điện tử, ký hiệu `MKVC`/`MKAC` trong lịch sử thay đổi code), với đặc điểm:

- Số lượng nhân sự lớn, có chấm công qua thiết bị vân tay/thẻ (HikVision, BioStar, Cosec).
- Có tích hợp với hệ thống MES (Manufacturing Execution System) ở xưởng sản xuất — nhân viên vừa là user ERP vừa là user MES.
- Quy trình duyệt đa cấp (multi-level approval) áp dụng cho hầu hết các chứng từ: đơn nghỉ phép, tăng ca, kế hoạch tuyển dụng, kết quả thử việc, hợp đồng lao động...
- Tuân thủ luật lao động Việt Nam: giới hạn giờ tăng ca (40h/tháng, có thể chia trần theo tuần/ngày), quy tắc nghỉ phép năm và mượn phép, chế độ thai sản, thuế TNCN luỹ tiến/khấu trừ 10%, BHXH/BHYT/BHTN theo tỷ lệ đóng NLĐ/doanh nghiệp.
- Có luồng ký hợp đồng điện tử (tích hợp e-signature bên thứ 3 kiểu FPT eSign).

## 3. Context Map — Bản đồ Bounded Context

```
                         ┌─────────────────────────┐
                         │   Employee Profile (01)  │◄────────────────────────┐
                         │   (Aggregate trung tâm)   │                          │
                         └───────────┬───────────────┘                          │
                 ┌───────────────────┼────────────────────┬─────────────────┐   │
                 │                   │                    │                 │   │
                 ▼                   ▼                    ▼                 ▼   │
     ┌───────────────────┐ ┌──────────────────┐ ┌──────────────────┐ ┌─────────────────┐
     │ Labor Contract (03)│ │ Timekeeping (04)  │ │ Request &        │ │ Personnel        │
     │                    │ │                    │ │ Approval (05)    │ │ Decisions (11)   │
     └─────────┬──────────┘ └─────────┬──────────┘ └─────────┬────────┘ └────────┬─────────┘
               │                      │                      │                    │
               │                      ▼                      │                    │
               │            ┌──────────────────┐             │                    │
               │            │  Payroll (06)     │◄────────────┘                    │
               │            └─────────┬──────────┘                                 │
               │                      │                                            │
               ├──────────────────────┼───────────────────┬────────────────────────┘
               ▼                      ▼                   ▼
     ┌───────────────────┐ ┌──────────────────┐ ┌──────────────────────┐
     │ Social Insurance   │ │ Personal Income   │ │ Maternity Benefits    │
     │ (07)               │ │ Tax (08)          │ │ (10)                  │
     └────────────────────┘ └──────────────────┘ └──────────────────────┘

     ┌────────────────────┐        ┌──────────────────┐
     │ Recruitment (02)    │───────►│ Training (09)     │
     │ (sinh ra Employee)  │        │                   │
     └────────────────────┘        └──────────────────┘

     Shared Kernel: APPROVAL ENGINE (chi tiết trong 05-request-approval.md)
     — được Recruitment(02), Request&Approval(05), Training(09), Personnel Decisions(11)
       cùng sử dụng làm cơ chế duyệt đa cấp chung.
```

**Quan hệ chính** (Upstream → Downstream, theo hướng phụ thuộc dữ liệu):
- `Employee Profile` là **Core Domain / Aggregate Root** — mọi context khác tham chiếu `EmployeeId`.
- `Recruitment` → tạo ra `Employee Profile` (candidate trúng tuyển → nhân viên chính thức).
- `Labor Contract` gắn với `Employee Profile` (1 nhân viên có nhiều hợp đồng theo thời gian).
- `Timekeeping & Shift` cung cấp dữ liệu công cho `Payroll`.
- `Request & Approval` (nghỉ phép/OT) ảnh hưởng `Timekeeping` (điều chỉnh công) và gián tiếp `Payroll`.
- `Payroll` phụ thuộc `Employee Profile` + `Timekeeping` + `Labor Contract` (hệ số/mức lương) và là upstream của `Social Insurance` + `Personal Income Tax` (cùng dùng lương làm cơ sở tính).
- `Personnel Decisions` (bổ nhiệm/điều chuyển/thôi việc) thay đổi trạng thái `Employee Profile` (chức vụ, phòng ban, trạng thái làm việc).
- `Maternity Benefits` phụ thuộc `Employee Profile` (giới tính, hợp đồng) và tương tác `Social Insurance` (chế độ thai sản do BHXH chi trả) + `Request & Approval` (đơn xin nghỉ thai sản).
- `Training` có thể phát sinh từ `Recruitment` (đào tạo hội nhập) hoặc độc lập (đào tạo định kỳ), dùng chung Approval Engine để duyệt kế hoạch/kết quả.

## 4. Shared Kernel — Approval Engine

Toàn hệ thống dùng **một cơ chế duyệt đa cấp chung** (trong code cũ là bảng `OOT9000`/`OOT9001` + class `OOF2050BL`), áp dụng cho: đơn từ nhân viên, kế hoạch tuyển dụng, kết quả thử việc, kế hoạch/kết quả đào tạo, quyết định nhân sự, hợp đồng lao động (ký số). Chi tiết mô hình hoá đầy đủ (Aggregate, Domain Event, Business Rule, Gherkin, Test case) nằm trong **`05-request-approval.md`** — các module khác chỉ tham chiếu tới nó (không định nghĩa lại).

Đặc điểm cốt lõi cần nhớ khi đọc các module khác:
- Một chứng từ (voucher) có N cấp duyệt (`ApprovingLevel`), mỗi cấp gắn với 1 chức danh/người duyệt cụ thể.
- Trạng thái mỗi cấp: `Pending` (chưa duyệt) → `Approved` (đã duyệt, chuyển cấp kế) hoặc `Rejected` (từ chối, dừng luồng).
- Chứng từ đã duyệt xong (hoặc đã duyệt ở cấp bất kỳ) thì **không được sửa/xoá** trực tiếp — phải qua nghiệp vụ huỷ duyệt.
- Có cơ chế tự động bỏ qua cấp duyệt trùng người duyệt liên tiếp (tránh tự duyệt cho chính mình).
- Có thông báo (notification/email) tới người duyệt kế tiếp và người tạo đơn khi có kết quả duyệt.

## 5. Ubiquitous Language — Bảng thuật ngữ tổng hợp

Quy ước: cột **Mã cũ** ghi lại mã hệ thống ASOFT gốc chỉ để truy vết nguồn gốc nghiệp vụ khi cần đối chiếu code cũ; **tài liệu và code mới không dùng các mã này**, thay bằng tên miền (domain name) tiếng Anh ở cột **Domain Term**.

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ (tham khảo) | Giải thích |
|---|---|---|---|
| Nhân viên | Employee | `HRMF2211`, `HRMT2211/2212` | Hồ sơ nhân sự chính thức |
| Ứng viên | Candidate | `HRMT1030/1031` | Người ứng tuyển, chưa là nhân viên |
| Phòng ban | Department | `DepartmentID` | Đơn vị tổ chức cấp 1 |
| Tổ/Nhóm | Team | `TeamID`, `CIP10101` | Đơn vị tổ chức cấp 2 |
| Chức danh | Title / Duty | `TitleID`, `DutyID` | Vị trí công việc |
| Hợp đồng lao động | Labor Contract | `HRMF2180`, `HT1360` | Văn bản pháp lý xác lập quan hệ lao động |
| Ký số điện tử | E-Signature | `HT1360StatusRecieve` | Ký hợp đồng qua nền tảng số |
| Ca làm việc | Work Shift | `HT1020` | Khung giờ làm việc chuẩn |
| Chấm công | Timekeeping / Attendance | `HT2406/2407/HRMF2264` | Ghi nhận giờ vào/ra thực tế |
| Bảng chấm công tháng | Monthly Timesheet | `HRMF2243`, `OOP2003` | Tổng hợp công theo tháng |
| Đơn xin phép (nghỉ phép) | Leave Request | `DXP`, `OOT2010`, `HRMT2360` | Đơn xin nghỉ có/không lương |
| Đơn xin làm thêm giờ | Overtime Request | `DXLTG` | Đơn xin tăng ca |
| Đơn xin ra ngoài | Out-of-Office Request | `DXRN` | Đơn xin ra ngoài trong giờ làm |
| Đơn bổ sung quẹt thẻ | Punch Correction Request | `DXBSQT` | Đơn xin bổ sung/sửa dữ liệu chấm công |
| Đơn xin đổi ca | Shift Swap Request | `DXDC` | Đơn xin đổi ca làm việc |
| Nghỉ bù | Compensatory Leave | `IsCompen` | Nghỉ bù cho giờ làm thêm |
| Mượn phép | Leave Borrowing | `MaxBorrowLeaveYear` | Xin nghỉ phép vượt số ngày phép đã tích luỹ |
| Cấp duyệt | Approval Level | `ApprovingLevel` | Thứ tự các bước duyệt trong luồng |
| Người duyệt | Approver | `ApprovePersonID` | Người có thẩm quyền duyệt ở 1 cấp |
| Kỳ lương | Payroll Period | `TranMonth/TranYear` | Chu kỳ tính lương (thường theo tháng) |
| Công thức lương | Payroll Formula | `HRMF1210` | Quy tắc tính từng khoản thu nhập |
| Hệ số cấp bậc | Grade Coefficient | `HSC` | Hệ số theo bậc/ngạch |
| Lương cơ bản | Base Salary | `LCB` | Mức lương nền tính các khoản khác |
| Phụ cấp | Allowance | `PB` | Khoản thu nhập ngoài lương cơ bản |
| Bảo hiểm xã hội | Social Insurance (SI) | `BHXH` | Bảo hiểm bắt buộc theo luật |
| Bảo hiểm y tế | Health Insurance (HI) | `BHYT` | Bảo hiểm y tế bắt buộc |
| Bảo hiểm thất nghiệp | Unemployment Insurance (UI) | `BHTN` | Bảo hiểm thất nghiệp bắt buộc |
| Thuế thu nhập cá nhân | Personal Income Tax (PIT) | `TNCN` | Thuế khấu trừ trên thu nhập |
| Chế độ thai sản | Maternity Benefit | `HRMT2340` | Quyền lợi nghỉ thai sản theo luật |
| Quyết định nhân sự | Personnel Decision | `HRMF2510/2610/2700` | Văn bản bổ nhiệm/điều chuyển/thôi việc |
| Thử việc | Probation | `HRMF2200/2201` | Giai đoạn đánh giá trước ký chính thức |
| Đào tạo | Training | `HRMF2070-2130` | Hoạt động nâng cao năng lực nhân viên |
| Trạng thái làm việc | Working Status | `WorkingStatus` | Đang làm / nghỉ việc / tạm ngưng... |
| Trạng thái hợp đồng | Contract Status | `StatusContract` | Còn hiệu lực / hết hạn / đã ký / chưa ký |

## 6. Quy ước đặt tên đề xuất cho hệ thống mới

Để loại bỏ quy ước mã màn hình khó đọc (`HRMFxxxx`, `OOTxxxx`...) của hệ thống cũ, tài liệu và schema đề xuất dùng quy ước sau khi generate DB/code mới:

- **Bảng DB**: `PascalCase` số nhiều theo domain, có prefix theo bounded context viết tắt 2-3 ký tự, ví dụ: `Emp_Employee`, `Emp_FamilyMember`, `Att_Shift`, `Att_TimeLog`, `Req_LeaveRequest`, `App_ApprovalStep`, `Pay_PayrollPeriod`, `Ins_SocialInsuranceEnrollment`, `Tax_PitDeclaration`, `Rec_Candidate`, `Trn_TrainingCourse`, `Mat_MaternityCase`, `Dec_PersonnelDecision`.
- **Khoá chính**: `Id` (kiểu `UNIQUEIDENTIFIER` hoặc `BIGINT IDENTITY`, thống nhất chọn 1 trong 2 khi generate — tài liệu dùng ký hiệu `Id` chung, không cố định kiểu).
- **Khoá ngoại**: `<TênBảngSốÍt>Id`, ví dụ `EmployeeId`, `DepartmentId`, `ShiftId`.
- **Cột trạng thái**: dùng `enum`/`lookup table` tường minh thay cho mã chuỗi (`Status` kiểu string cũ) — mỗi module định nghĩa enum riêng trong phần DDD của nó.
- **Soft delete**: cột `IsDeleted BIT` + `DeletedAtUtc` thay cho `DeleteFlg` kiểu cũ.
- **Audit**: `CreatedAtUtc`, `CreatedBy`, `LastModifiedAtUtc`, `LastModifiedBy` thống nhất trên mọi bảng nghiệp vụ.

## 7. Ghi chú về khoảng trống dữ liệu (Data Gaps)

Một số giá trị nghiệp vụ cụ thể (VD: mức trần OT theo tuần/ngày chính xác, bậc thuế TNCN hiện hành, tỷ lệ đóng BHXH/BHYT/BHTN từng thời kỳ) **không cố định trong code** mà được cấu hình qua bảng tham số (`HF0390`, `HT0332`...). Các file module liên quan sẽ mô hình hoá các bảng này như **Configuration/Policy Aggregate** thay vì hard-code giá trị, để hệ thống mới giữ được tính linh hoạt tương đương.
