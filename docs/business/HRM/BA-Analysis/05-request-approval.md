# 05 — Request & Approval — Đơn từ Nhân viên & Cơ chế Duyệt đa cấp dùng chung

> **Vai trò tài liệu**: Đây là bounded context **quan trọng nhất** trong bộ tài liệu BA-Analysis vì phần **Approval Engine** (mục 1a) là **Shared Kernel** — được TẤT CẢ bounded context khác trong hệ thống HRM (và có thể cả ngoài HRM, theo dấu vết code cũ `OOF2050BL` dùng chung cho cả module báo giá) tham chiếu tới để thực hiện duyệt đa cấp, thay vì mỗi module tự cài đặt luồng duyệt riêng. Các module khác (`02-recruitment.md`, `03-labor-contract.md`, `04-timekeeping-shift.md`, `09-training.md`, `11-personnel-decisions.md`...) khi cần mô tả nghiệp vụ duyệt sẽ **chỉ tham chiếu tới các Aggregate/Entity/Business Rule định nghĩa ở đây**, không định nghĩa lại.
>
> File gồm 2 phần nghiệp vụ độc lập nhưng gắn kết chặt chẽ:
> - **Phần A (1a)**: Approval Engine — cơ chế duyệt đa cấp tổng quát, domain-agnostic (không biết gì về "đơn nghỉ phép" hay "kế hoạch tuyển dụng" — chỉ biết "có 1 workflow N cấp cần duyệt").
> - **Phần B (1b)**: Employee Self-Service Request — 5 loại đơn từ nhân viên cụ thể (nghỉ phép, OT, ra ngoài, bổ sung chấm công, đổi ca) + đơn công tác, là **consumer** đầu tiên và điển hình nhất của Approval Engine.

## 0. Nguyên tắc thiết kế áp dụng trong tài liệu này

1. **Tách Engine khỏi Request**: Trong hệ thống cũ, `OOF2050BL` (generic) bị trộn lẫn với `HRMF2360BL` (nghiệp vụ đơn từ cụ thể) qua nhiều bản vá theo thời gian, gây khó bảo trì. Tài liệu này **tách rõ 2 aggregate độc lập**: `ApprovalWorkflow` (Engine, không phụ thuộc domain nghiệp vụ cụ thể) và `LeaveOrOvertimeRequest`/5 entity con (Request, phụ thuộc Engine qua tham chiếu `ApprovalWorkflowId`, không kế thừa hay biết chi tiết nội bộ Engine).
2. **Tham chiếu đa hình (polymorphic reference), không dùng FK cứng**: `App_ApprovalWorkflow.SourceDocumentId` trỏ tới bản ghi ở NHIỀU bảng khác nhau tuỳ `RequestTypeCode` (có thể là `Req_LeaveRequest.Id`, có thể là `Rec_RecruitmentPlan.Id` ở module Recruitment...). Đây là ranh giới bounded context điển hình — Engine không có FK database cứng tới bảng nghiệp vụ của module khác, chỉ có "liên kết mềm" qua cặp `(SourceBoundedContext, RequestTypeCode, SourceDocumentId)`. Điều này cho phép Engine thực sự là **shared kernel** dùng được cho module chưa tồn tại tại thời điểm thiết kế.
3. **Table Per Concrete Type (TPC) cho 5 loại Request**: Thay vì 1 bảng đa hình `HRMT2360` như hệ thống cũ (nhiều cột chỉ dùng cho 1 loại, để trống ở loại khác), mỗi loại đơn có 1 bảng riêng đầy đủ field của nó + lặp lại các field chung (base) — đổi lại tra cứu/báo cáo/validate theo từng loại đơn đơn giản, không cần filter theo `RequestTypeID` trên 1 bảng khổng lồ. Về mặt code (C#), cả 5 entity vẫn kế thừa 1 abstract base class `LeaveOrOvertimeRequest` để dùng chung logic (validate quota, snapshot tổ chức, liên kết `ApprovalWorkflowId`...).
4. **Chuẩn hoá Status thành enum**: `Status` kiểu string tự do của code cũ (`"0"`, `"2"`, các giá trị khác) được thay bằng 2 enum tường minh: `ApprovalStepStatus` (Pending/Approved/Rejected/Skipped) ở cấp Engine, và `RequestStatus` (Draft/Submitted/InProgress/Approved/Rejected/Cancelled) ở cấp Request.
5. **Data Gap ghi nhận rõ**: Trường `OvertimeHoursNormalDay` (map từ `OverTimeNN` code cũ) chưa xác định rõ ngữ nghĩa chính xác trong quá trình khảo sát — giữ lại trong schema với ghi chú "cần làm rõ với SME nghiệp vụ trước khi generate code chính thức", theo đúng tinh thần mục 7 của `00-domain-overview.md`.

---

# Phần 1 — DDD (Domain-Driven Design)

## 1.1 Bounded Context: Request & Approval

- **Loại**: Generic Subdomain (phần Approval Engine) lồng ghép với Supporting Subdomain (phần Employee Self-Service Request). Approval Engine đóng vai trò **Shared Kernel** theo đúng định nghĩa DDD — 1 model dùng chung, được nhiều bounded context đồng ý tuân theo, thay đổi phải được thống nhất vì ảnh hưởng ngược lên tất cả consumer.
- **Mục đích**:
  - (A) Cung cấp 1 cơ chế duyệt đa cấp (multi-level approval) tổng quát, cấu hình được số cấp và chức danh duyệt theo từng loại chứng từ, dùng chung cho toàn hệ thống HRM.
  - (B) Cho phép nhân viên tự tạo và theo dõi 5 loại đơn từ phổ biến nhất trong vòng đời làm việc hàng ngày: nghỉ phép, làm thêm giờ, ra ngoài, bổ sung chấm công, đổi ca — và đơn đề nghị công tác.
- **Ranh giới (Boundary)**:
  - Approval Engine **không biết** ngữ nghĩa nghiệp vụ của chứng từ đang duyệt (không biết "40 giờ OT" hay "nghỉ phép năm" là gì) — nó chỉ vận hành trên khái niệm trừu tượng "workflow N cấp, mỗi cấp 1 người duyệt, trạng thái Pending/Approved/Rejected".
  - Việc **resolve người duyệt cụ thể** (map DutyId → EmployeeId) phụ thuộc dữ liệu tổ chức (Department/Section/Subsection) thuộc về Employee Profile (`01-employee-profile.md`) — Approval Engine chỉ tiêu thụ (consume) dữ liệu này qua snapshot, không sở hữu.
  - Việc **thực thi kết quả duyệt** (VD: khi Leave Request được Approved thì cập nhật số dư phép, khi Punch Correction được Approved thì ghi vào bảng chấm công) thuộc về bounded context sở hữu chứng từ đó (ở đây là chính module 05 phần B, hoặc `04-timekeeping-shift.md`), không thuộc Approval Engine.
- **Vì sao đây là module quan trọng nhất**: Số lượng bounded context phụ thuộc Approval Engine (xem mục 1.6) lớn hơn bất kỳ shared kernel nào khác trong hệ thống. Thiết kế sai ở đây (thiếu tổng quát, gắn cứng vào 1 loại chứng từ) sẽ buộc các module sau phải tự chế lại cơ chế duyệt — lặp lại đúng vấn đề của hệ thống cũ (`OOF2050BL` đã phải phân nhánh `UpdateAllBEMMeiko`/`UpdateAll_ANGEL` theo khách hàng vì thiết kế gốc không đủ tổng quát).

## 1.2 Ubiquitous Language

| Thuật ngữ (VN) | Domain Term (EN) | Mã cũ (tham khảo) | Giải thích |
|---|---|---|---|
| Luồng duyệt | Approval Workflow | `OOT9000` | 1 thực thể duyệt gắn với đúng 1 chứng từ gốc, chạy qua N cấp |
| Bước duyệt / cấp duyệt | Approval Step | `OOT9001`, `OOP2005` | 1 cấp cụ thể trong luồng duyệt, gắn 1 người duyệt |
| Ma trận duyệt | Approval Matrix | `OOF0010` | Cấu hình số cấp (`RollLevel`) + chức danh (`DutyID`) cho từng loại chứng từ |
| Loại chứng từ (cần duyệt) | Request Type | `RequestTypeID` (`DXP/DXLTG/DXRN/DXBSQT/BPC/DXDC`) | Mã phân loại chứng từ, dùng để tra Approval Matrix |
| Lịch sử duyệt | Approval History / Log | *(mới, không có bảng riêng ở hệ cũ)* | Audit trail ghi mọi quyết định duyệt, kể cả bị bỏ qua/huỷ |
| Bỏ qua cấp duyệt tự động | Auto-skip | `GetNextApprovePersonRecursive` | Tự động bỏ 1 cấp khi người duyệt trùng cấp liền trước |
| Người duyệt | Approver | `ApprovePersonID` | Nhân viên có thẩm quyền duyệt tại 1 cấp |
| Người duyệt kế tiếp | Next Approver | `NextApprovePersonID` | Người duyệt được resolve cho cấp kế tiếp |
| Người tạo đơn / Người yêu cầu | Requester | `CreateUserID` | Nhân viên tạo và nộp chứng từ |
| Đơn xin nghỉ phép | Leave Request | `DXP`, `OOT2010`, `HRMT2360` | Đơn xin nghỉ có/không lương |
| Đơn xin làm thêm giờ | Overtime Request | `DXLTG`, `OOP2007` | Đơn xin tăng ca |
| Đơn xin ra ngoài | Out-of-Office Request | `DXRN` | Đơn xin ra ngoài trong giờ làm |
| Đơn bổ sung quẹt thẻ | Punch Correction Request | `DXBSQT` | Đơn xin bổ sung/sửa dữ liệu chấm công |
| Đơn xin đổi ca | Shift Swap Request | `DXDC`, `OOP2034` | Đơn xin đổi ca làm việc |
| Đề nghị công tác | Business Trip Request | `OOT2020_1` | Đơn xin đi công tác, có thể phát sinh tạm ứng |
| Mượn phép | Leave Borrowing | `MaxBorrowLeaveYear` | Xin nghỉ vượt số ngày phép đã tích luỹ, có trần theo năm |
| Trần mượn phép năm | Max Borrow Leave Per Year | `MaxBorrowLeaveYear` (`HF0390`) | Số ngày phép tối đa được mượn trong 1 năm |
| Tháng được phép mượn | Month Allow Borrow Leave | `MonthAllowBorrowLeave` (`HF0390`) | Danh sách tháng trong năm được phép mượn phép |
| Nghỉ bù | Compensatory Leave | `IsCompen` | Nghỉ bù cho giờ làm thêm đã thực hiện |
| Trần giờ OT tháng | Max Overtime Hours Per Month | `TMaxPerMonth`/`HMaxPerMonth` (`HF0390`) | Giới hạn giờ OT/tháng, mặc định 40h theo luật lao động VN |
| Hạn mức số đơn/tháng | Max Request Per Month | `MaxRequestDXP/DXLTG/DXRN/DXBSQT/DXDC` (`HF0390`) | Số đơn tối đa mỗi loại/tháng/nhân viên |
| Tổ chức tại thời điểm tạo đơn | Org Snapshot | *(mới)* | Department/Section/Subsection/Process được chốt lại lúc submit, dùng để resolve người duyệt |

## 1.3 Phần 1a — Approval Engine (Generic, Shared Kernel)

### 1.3.1 Aggregate Root: `ApprovalWorkflow`

Đại diện cho 1 luồng duyệt gắn với đúng 1 chứng từ gốc (voucher) của bất kỳ bounded context nào. Là **Aggregate Root** — mọi thao tác trên `ApprovalStep`/`ApprovalHistory` con của nó đều phải đi qua `ApprovalWorkflow` (đảm bảo tính nhất quán: không cho duyệt cấp 3 khi cấp 2 còn Pending).

| Field | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Id | PK |
| `RequestTypeCode` | string | Mã loại chứng từ, tham chiếu mềm `App_ApprovalMatrix.RequestTypeCode` |
| `SourceBoundedContext` | string | Tên bounded context sở hữu chứng từ (`"Request"`, `"Recruitment"`, `"Training"`, `"PersonnelDecision"`, `"LaborContract"`, `"Timekeeping"`...) |
| `SourceDocumentId` | Id | Tham chiếu đa hình tới bản ghi chứng từ gốc (VD `Req_LeaveRequest.Id`) — **không phải FK cứng trong DB** |
| `RequesterEmployeeId` | EmployeeId | Người tạo/nộp chứng từ |
| `RequesterDepartmentId` / `RequesterSectionId` / `RequesterSubsectionId` | OrgId | Snapshot tổ chức của người tạo tại thời điểm Start, dùng để resolve approver |
| `TotalLevels` | int | Số cấp duyệt, snapshot từ `ApprovalMatrix` tại thời điểm Start (BR-09) |
| `CurrentLevel` | int | Cấp đang chờ duyệt hiện hành (`ApprovingLevel`) |
| `Status` | `ApprovalWorkflowStatus` | `InProgress` / `Approved` / `Rejected` / `Cancelled` |
| `StartedAtUtc` | datetime | |
| `CompletedAtUtc` | datetime? | Null khi còn `InProgress` |
| `CreatedAtUtc/CreatedBy/LastModifiedAtUtc/LastModifiedBy` | audit | |
| `IsDeleted/DeletedAtUtc` | soft-delete | Workflow không bị xoá cứng — chỉ Cancel |

**Hành vi (methods) trên aggregate**: `Start()`, `ApproveCurrentStep(approverId, comment)`, `RejectCurrentStep(approverId, comment)`, `Cancel(actorId, reason)`, `ResolveAndAssignNextApprover()`.

### 1.3.2 Entity con: `ApprovalStep`

1 bước duyệt cụ thể trong `ApprovalWorkflow` — map trực tiếp từ `OOT9001`/`OOP2005` code cũ.

| Field | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Id | PK |
| `ApprovalWorkflowId` | Id | FK → `ApprovalWorkflow` |
| `Level` | int | Thứ tự cấp, 1..N |
| `ApproverEmployeeId` | EmployeeId? | Người duyệt được resolve; null nếu chưa tới lượt resolve |
| `ApproverDutyId` | DutyId | Chức danh yêu cầu duyệt ở cấp này (lưu lại để audit, độc lập với `ApprovalMatrixLevel` gốc phòng khi matrix bị sửa sau) |
| `Status` | `ApprovalStepStatus` | `Pending` / `Approved` / `Rejected` / `Skipped` |
| `DecisionAtUtc` | datetime? | Thời điểm duyệt/từ chối |
| `Comment` | string? | Ý kiến/lý do từ chối |
| `IsAutoSkipped` | bool | Đánh dấu bước bị auto-skip do trùng người duyệt cấp trước (BR-03) |
| `NotifiedAtUtc` | datetime? | Thời điểm gửi mail thông báo cho người duyệt cấp này |
| `CreatedAtUtc/LastModifiedAtUtc/CreatedBy/LastModifiedBy` | audit | |

### 1.3.3 Configuration Aggregate: `ApprovalMatrix` + `ApprovalMatrixLevel`

Đây là "Approval Matrix Designer" — map từ `OOF0010BL`. Là 1 aggregate **cấu hình** riêng biệt, độc lập vòng đời với `ApprovalWorkflow` (sửa matrix không ảnh hưởng workflow đang chạy, theo BR-09).

**`ApprovalMatrix`** (aggregate root cấu hình):

| Field | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Id | PK |
| `RequestTypeCode` | string | Mã loại chứng từ, unique — đây là "khoá nghiệp vụ" mọi consumer dùng để tra cứu |
| `RequestTypeName` | string | Tên hiển thị (VD "Đơn xin nghỉ phép") |
| `BoundedContext` | string | Module sở hữu loại chứng từ này (để phân quyền cấu hình) |
| `TotalLevels` | int | Denormalized = `COUNT(ApprovalMatrixLevel)`, cập nhật khi thêm/bớt cấp |
| `IsActive` | bool | |
| `EffectiveFromDate` | date | |
| audit + soft-delete | | |

**`ApprovalMatrixLevel`** (entity con):

| Field | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Id | PK |
| `ApprovalMatrixId` | Id | FK |
| `Level` | int | Thứ tự cấp, unique trong phạm vi `ApprovalMatrixId` |
| `DutyId` | DutyId | Chức danh có quyền duyệt ở cấp này |
| `OrgScope` | `OrgScope` enum | `Subsection` / `Section` / `Department` / `Company` — phạm vi tổ chức để tìm người giữ `DutyId` (VD cấp 1 = Tổ trưởng của Tổ người tạo đơn thuộc về) |
| `IsRequired` | bool | Nếu `false`, không tìm được người duyệt thì tự động Skip thay vì chặn luồng |
| audit | | |

### 1.3.4 `ApprovalHistory` — Audit Log (append-only)

Không có ở hệ thống cũ (chỉ suy ra qua log method `WriteApprovalHistory` trong `HRMF2360BL`) — được chính thức hoá thành 1 Entity/bảng riêng trong thiết kế mới, ghi lại **mọi** hành động, kể cả Skip/Cancel, phục vụ audit và truy vết khiếu nại.

| Field | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Id | PK |
| `ApprovalWorkflowId` | Id | FK |
| `ApprovalStepId` | Id? | Null nếu là hành động ở cấp workflow (VD `Cancelled`) |
| `Level` | int? | |
| `ActorEmployeeId` | EmployeeId? | Null nếu hành động do hệ thống tự động thực hiện (VD auto-skip) |
| `Action` | `ApprovalHistoryAction` | `Started` / `Approved` / `Rejected` / `Skipped` / `Cancelled` / `Resubmitted` |
| `ActionAtUtc` | datetime | |
| `Comment` | string? | |
| `SnapshotStatus` | string | Trạng thái workflow ngay sau hành động (phục vụ truy vết không cần join lại) |
| `CreatedAtUtc` | datetime | |

Bảng này **không có** `LastModifiedAtUtc/LastModifiedBy` — vì là append-only, không cho sửa (BR-10).

### 1.3.5 Value Objects & Enums

```
enum ApprovalStepStatus     { Pending, Approved, Rejected, Skipped }
enum ApprovalWorkflowStatus { InProgress, Approved, Rejected, Cancelled }
enum OrgScope               { Subsection, Section, Department, Company }
enum ApprovalHistoryAction  { Started, Approved, Rejected, Skipped, Cancelled, Resubmitted }
```

`RequestTypeCode` **không** phải hard-coded enum — là 1 giá trị string mở rộng được, đăng ký qua bảng `App_ApprovalMatrix`, để module mới trong tương lai (chưa tồn tại hôm nay) có thể "cắm vào" Engine chỉ bằng cách thêm 1 dòng cấu hình, không cần sửa code Engine hay thêm giá trị enum.

### 1.3.6 Domain Events (Approval Engine)

| Event | Khi nào phát sinh | Payload chính | Consumer điển hình |
|---|---|---|---|
| `ApprovalWorkflowStarted` | Chứng từ được submit, workflow khởi tạo đủ N `ApprovalStep` | `ApprovalWorkflowId`, `RequestTypeCode`, `SourceDocumentId`, `RequesterEmployeeId`, `TotalLevels` | Module sở hữu chứng từ (chuyển `RequestStatus` → `InProgress`) |
| `ApprovalStepAssigned` | Người duyệt cho 1 cấp vừa được resolve xong | `ApprovalStepId`, `ApproverEmployeeId`, `Level` | Notification Service (gửi mail cho approver) |
| `ApprovalStepApproved` | 1 cấp được duyệt | `ApprovalStepId`, `Level`, `ApproverEmployeeId`, `Comment` | Engine (tự resolve cấp kế), Notification |
| `ApprovalStepSkipped` | 1 cấp bị auto-skip | `ApprovalStepId`, `Level` | Audit/History |
| `ApprovalStepRejected` | 1 cấp bị từ chối | `ApprovalStepId`, `Level`, `ApproverEmployeeId`, `Comment` | Engine (dừng luồng, set workflow Rejected) |
| `ApprovalWorkflowCompleted` | Cấp cuối cùng Approved | `ApprovalWorkflowId`, `SourceDocumentId` | Module sở hữu chứng từ (thực thi hiệu lực nghiệp vụ, VD trừ phép, ghi công) |
| `ApprovalWorkflowRejected` | Bất kỳ cấp nào bị Rejected | `ApprovalWorkflowId`, `SourceDocumentId`, `RejectedAtLevel` | Module sở hữu chứng từ (chuyển `RequestStatus` → `Rejected`), Notification cho Requester |
| `ApprovalWorkflowCancelled` | Requester/Admin huỷ workflow đang chạy | `ApprovalWorkflowId`, `Reason` | Module sở hữu chứng từ |

### 1.3.7 Business Rules — Approval Engine

- **BR-01 (Data Integrity — bắt buộc đủ số cấp)**: Khi 1 `ApprovalWorkflow` được `Start()`, hệ thống PHẢI tạo đủ **N dòng `ApprovalStep`** tương ứng với `TotalLevels` cấu hình trong `ApprovalMatrix` tại thời điểm đó cho `RequestTypeCode` tương ứng. Thiếu dòng là lỗi toàn vẹn dữ liệu nghiêm trọng (tương đương bug thực tế `HasMissingApprovalRows` trong hệ thống cũ) — phải có cơ chế kiểm tra định kỳ hoặc validate ngay khi Start để chặn triệt để, không để xảy ra ở tầng ứng dụng.
- **BR-02 (Resolve người duyệt theo chức danh + tổ chức)**: Người duyệt của mỗi cấp được resolve tự động dựa trên `DutyId` cấu hình tại `ApprovalMatrixLevel` kết hợp với tổ chức (`Department`/`Section`/`Subsection`) snapshot của người tạo đơn theo đúng `OrgScope` của cấp đó.
- **BR-03 (Auto-skip khi trùng người duyệt)**: Khi resolve người duyệt cho 1 cấp, nếu người được resolve trùng với người đã `Approved` ở cấp liền trước, hệ thống tự động đánh dấu cấp đó `Skipped` (không chờ duyệt) và tiếp tục resolve cấp kế tiếp (đệ quy), tránh tình trạng 1 người phải tự duyệt lại cho chính mình hoặc duyệt lặp vô nghĩa.
- **BR-04 (Giới hạn độ sâu đệ quy)**: Thuật toán resolve/auto-skip đệ quy (`ResolveAndAssignNextApprover`) bị giới hạn tối đa **10 lần gọi liên tiếp**; nếu vượt ngưỡng, workflow dừng lại ở trạng thái lỗi cấu hình thay vì rơi vào vòng lặp vô hạn.
- **BR-05 (Reject dừng toàn bộ luồng)**: Khi 1 cấp bất kỳ chuyển `Rejected`, toàn bộ `ApprovalWorkflow` chuyển ngay `Status = Rejected`; các cấp còn lại (đang `Pending`) không được duyệt tiếp, workflow coi như kết thúc.
- **BR-06 (Khoá sửa/xoá chứng từ đã có tiến trình duyệt)**: Chứng từ có `ApprovalWorkflow` đã có ít nhất 1 `ApprovalStep` ở trạng thái `Approved` (tức đã qua ít nhất 1 cấp), hoặc `Status = Approved`/`Completed`, thì **không được sửa/xoá trực tiếp** bởi người sở hữu — phải thực hiện nghiệp vụ `Cancel` workflow trước (rule dùng chung mọi màn hình, tương đương `HRMCommonController` code cũ, áp dụng cho cả Recruitment Plan, Probation Result, Shift Registration...).
- **BR-07 (Thông báo)**: Khi 1 `ApprovalStep` chuyển `Approved`, hệ thống gửi thông báo (email/notification) cho người duyệt của cấp kế tiếp (`ApprovalStepAssigned`); khi workflow `Completed` hoặc `Rejected`, gửi thông báo cho Requester.
- **BR-08 (Duyệt hàng loạt là tính năng UI, không phải domain rule riêng)**: Bulk approve/reject là thao tác lặp lại `ApproveStep`/`RejectStep` cho nhiều workflow được chọn cùng lúc ở tầng ứng dụng/UI — không sinh ra hành vi domain mới. Các biến thể tuỳ khách hàng của hệ thống cũ (`UpdateAllBEMMeiko`, `UpdateAll_ANGEL`) là **customization theo khách hàng**, không đưa vào domain model chuẩn; nếu cần, triển khai dưới dạng strategy/plugin riêng ngoài core.
- **BR-09 (Snapshot matrix tại thời điểm Start)**: `TotalLevels` và cấu hình từng cấp được **snapshot** vào `ApprovalWorkflow`/`ApprovalStep` ngay khi Start; thay đổi `ApprovalMatrix` sau đó chỉ ảnh hưởng các workflow khởi tạo **sau** thời điểm thay đổi, không ảnh hưởng ngược các workflow đang `InProgress`.
- **BR-10 (History bất biến)**: `ApprovalHistory` là append-only — không entity/service nào được phép `UPDATE`/`DELETE` bản ghi lịch sử đã ghi.

## 1.4 Phần 1b — Employee Self-Service Request

### 1.4.1 Aggregate Root (base): `LeaveOrOvertimeRequest`

> Ghi chú đặt tên: tên `LeaveOrOvertimeRequest` được giữ lại theo truyền thống lớp nghiệp vụ hợp nhất `HRMF2360BL` của hệ thống cũ (vốn đã gộp xử lý nghỉ phép/OT/ca vào 1 lớp) để dễ truy vết, dù về ngữ nghĩa nó là base class trừu tượng cho **cả 5 loại đơn** (không chỉ nghỉ phép/OT). Khi generate code, đây là 1 `abstract class`/`base entity`, không map trực tiếp ra 1 bảng riêng (thiết kế DB dùng Table-Per-Concrete-Type — xem 1.5).

**Field chung (base) — lặp lại ở cả 5 bảng con**:

| Field | Kiểu | Ghi chú |
|---|---|---|
| `Id` | Id | PK |
| `ApplicationNo` | string | Số đơn hiển thị cho người dùng (human-readable, sinh tự động) |
| `EmployeeId` | EmployeeId | Người tạo đơn |
| `DepartmentId`/`SectionId`/`SubsectionId`/`ProcessId` | OrgId | **Org Snapshot** — tổ chức của nhân viên tại thời điểm tạo đơn, dùng để resolve approver qua Approval Engine (BR-16); không đổi theo tổ chức hiện tại nếu nhân viên bị điều chuyển sau đó |
| `Reason` | string | Lý do |
| `Status` | `RequestStatus` | `Draft` / `Submitted` / `InProgress` / `Approved` / `Rejected` / `Cancelled` |
| `ApprovalWorkflowId` | Id? | FK mềm → `App_ApprovalWorkflow.Id`, gán khi Submit (null khi còn `Draft`) |
| `CreatedAtUtc/CreatedBy/LastModifiedAtUtc/LastModifiedBy` | audit | |
| `IsDeleted/DeletedAtUtc` | soft-delete | |

**Domain method chung**: `Submit()` (validate quota + tạo `ApprovalWorkflow`), `Cancel()` (chỉ khi `Status` chưa qua cấp duyệt nào — theo BR-18), `ApplyApprovalResult(...)` (nhận `ApprovalWorkflowCompleted`/`Rejected` event, cập nhật `Status`).

### 1.4.2 Entity con theo 5 loại đơn

**1. `LeaveRequest`** (kế thừa base) — map `HRMT2360`(`DXP`) + `OOT2010_1` + `OOP2013`:

| Field | Kiểu | Ghi chú |
|---|---|---|
| `AbsentTypeId` | AbsentTypeId | Loại nghỉ (phép năm/không lương/ốm...) — lookup riêng |
| `LeaveFromDate`/`LeaveToDate` | date | Khoảng ngày nghỉ |
| `TotalTime` | decimal | Tổng giờ/ngày nghỉ quy đổi |
| `OffsetTime` | decimal | Giờ điều chỉnh (offset) |
| `TimeAllowanceHours` | decimal | Giờ được tính phụ cấp kèm theo |
| `IsCompensatory` | bool | `true` nếu là nghỉ bù cho OT (BR-17) |
| `SourceOvertimeRequestId` | Id? | FK → `OvertimeRequest.Id`, chỉ có giá trị khi `IsCompensatory = true` |

Entity con của `LeaveRequest` (child, không phải aggregate riêng): **`LeaveRequestDetail`** — map `OOT2010_1` (đơn nghỉ phép có thể trải nhiều ngày, mỗi ngày 1 ca/số giờ khác nhau):

| Field | Kiểu | Ghi chú |
|---|---|---|
| `LeaveRequestId` | Id | FK |
| `LeaveDate` | date | |
| `ShiftId` | ShiftId | Ca áp dụng ngày đó |
| `DailyHours` | decimal | Số giờ nghỉ trong ngày đó (cho phép nghỉ nửa ngày) |
| `Status` | `RequestStatus` | Trạng thái riêng của từng dòng ngày (kế thừa từ trạng thái chung, hiếm khi lệch) |

**2. `OvertimeRequest`** — map `DXLTG` + `OOP2007`:

| Field | Kiểu | Ghi chú |
|---|---|---|
| `OvertimeDate` | date | Ngày đăng ký làm thêm |
| `ShiftId` | ShiftId | Ca liên quan |
| `PlannedFromTime`/`PlannedToTime` | time | Khung giờ OT dự kiến |
| `OvertimeHours` | decimal | Tổng giờ OT đăng ký (`OverTime`) |
| `OvertimeHoursNormalDay` | decimal | (`OverTimeNN` — **Data Gap**: ngữ nghĩa chính xác chưa xác định trong khảo sát code cũ, tạm giữ tên trung tính, cần xác nhận lại với SME trước khi generate code chính thức) |
| `OvertimeHoursCompany` | decimal | Giờ OT công ty ghi nhận/duyệt cuối cùng (`OverTimeCompany`, có thể khác giờ đăng ký do làm tròn/chính sách) |
| `IsPreShiftOT` | bool | Tăng ca trước ca làm việc chính |

**3. `OutOfOfficeRequest`** — map `DXRN`:

| Field | Kiểu | Ghi chú |
|---|---|---|
| `OutDate` | date | |
| `FromTime`/`ToTime` | time | |
| `Place` | string | Nơi đến |
| `GoStraight` | bool | Đi thẳng tới nơi, không qua công ty |
| `ComeStraight` | bool | Về thẳng nhà, không quay lại công ty |
| `IsOnTripOT` | bool | Có tính OT cho khoảng thời gian ra ngoài/công tác hay không |

**4. `PunchCorrectionRequest`** — map `DXBSQT`:

| Field | Kiểu | Ghi chú |
|---|---|---|
| `WorkingDate` | date | Ngày công cần bổ sung/sửa |
| `PunchDateTime` | datetime | Thời điểm quẹt thẻ đề nghị |
| `InOutType` | enum(`In`,`Out`) | Vào/ra |
| `EditType` | enum(`Add`,`Edit`) | Bổ sung mới hoặc sửa bản ghi có sẵn |

**5. `ShiftSwapRequest`** — map `DXDC`/`DCTT` + `OOP2034`:

| Field | Kiểu | Ghi chú |
|---|---|---|
| `RequestedShiftId` | ShiftId | Ca muốn áp dụng |
| `FromWorkingDate` | date | Ngày làm việc gốc muốn đổi |
| `ToWorkingDate` | date | Ngày áp dụng ca mới |
| `SwapWithEmployeeId` | EmployeeId? | Đồng nghiệp đổi ca chéo (nếu có) |

**Ngoài phạm vi 5 loại chính — `BusinessTripRequest`** (Đề nghị công tác) — map `OOT2020_1`, chỉ ghi nhận là **integration point**, không đi sâu nghiệp vụ tạm ứng (thuộc Kế toán/Tài chính, ngoài phạm vi HRM):

| Field | Kiểu | Ghi chú |
|---|---|---|
| `TripPurpose` | string | Mục đích công tác |
| `Destination` | string | Nơi đến |
| `GoFromDate`/`GoToDate` | date | |
| `EstimatedAdvanceAmount` | decimal? | Số tiền tạm ứng dự kiến |
| `ExternalAdvanceVoucherRef` | string? | Tham chiếu tới phiếu tạm ứng bên hệ thống Kế toán/Tài chính (integration, không FK cứng) |

### 1.4.3 Domain Events (Employee Self-Service Request)

| Event | Khi nào |
|---|---|
| `LeaveRequestSubmitted` | `LeaveRequest.Submit()` thành công, đã pass validate (BR-11, BR-12, BR-15) |
| `OvertimeRequestSubmitted` | `OvertimeRequest.Submit()` thành công, đã pass validate (BR-13, BR-14, BR-15) |
| `OutOfOfficeRequestSubmitted` | |
| `PunchCorrectionRequestSubmitted` | |
| `ShiftSwapRequestSubmitted` | |
| `BusinessTripRequestSubmitted` | |
| `LeaveRequestApproved` / `LeaveRequestRejected` | Khi nhận `ApprovalWorkflowCompleted`/`Rejected` tương ứng — trừ số dư phép (nếu Approved) |
| `OvertimeRequestApproved` / `OvertimeRequestRejected` | |
| `PunchCorrectionApproved` | Kích hoạt ghi nhận vào bảng chấm công của module 04 (chỉ khi Approved — BR-19) |
| `RequestCancelled` | Requester huỷ đơn khi còn được phép (BR-18) |

### 1.4.4 Business Rules — Employee Self-Service Request

- **BR-11 (Trần mượn phép theo năm)**: Nhân viên được phép xin nghỉ vượt số ngày phép đã tích luỹ (leave borrowing), nhưng tổng số ngày mượn trong 1 năm không vượt quá `MaxBorrowLeaveYear` cấu hình tại `HF0390` (module 06 — Payroll Policy).
- **BR-12 (Giới hạn tháng được mượn phép)**: Mượn phép chỉ được chấp nhận trong các tháng nằm trong `MonthAllowBorrowLeave` (`HF0390`); ngoài các tháng này, đơn có số ngày vượt số dư sẵn có bị từ chối ngay ở bước validate (trước khi tạo `ApprovalWorkflow`).
- **BR-13 (Trần giờ OT/tháng)**: Tổng giờ OT đăng ký (đã `Approved` + đang `Pending`) trong 1 tháng của 1 nhân viên không vượt trần cấu hình — mặc định **40 giờ/tháng** theo Bộ luật Lao động Việt Nam, có thể khác theo nhóm đối tượng (`ObjectType`) qua `HF0390` (`TMaxPerMonth`/`HMaxPerMonth`/`HMaxPerTime`).
- **BR-14 (Chức danh được phép tạo đơn OT)**: Chỉ nhân viên có `Position`/`DutyId` nằm trong danh sách được phép cấu hình mới có thể tạo `OvertimeRequest` — nhân viên không đủ điều kiện bị chặn ngay khi Submit.
- **BR-15 (Hạn mức số đơn/tháng)**: Mỗi loại đơn (Leave/Overtime/OutOfOffice/PunchCorrection/ShiftSwap) có số lượng đơn tối đa 1 nhân viên được tạo trong 1 tháng, cấu hình riêng từng loại tại `HF0390` (`MaxRequestDXP`/`MaxRequestDXLTG`/`MaxRequestDXRN`/`MaxRequestDXBSQT`/`MaxRequestDXDC`) — chống lạm dụng tạo đơn tràn lan.
- **BR-16 (Org Snapshot bất biến)**: Tổ chức (`Department`/`Section`/`Subsection`/`Process`) được snapshot vào đơn ngay khi tạo, dùng để resolve approver qua Approval Engine; nếu nhân viên bị điều chuyển sau khi đơn đã tạo, đơn vẫn giữ nguyên snapshot cũ (không tự cập nhật theo tổ chức hiện tại).
- **BR-17 (Liên kết nghỉ bù ↔ OT nguồn)**: `LeaveRequest.IsCompensatory = true` chỉ hợp lệ khi có thể xác định (tuỳ chọn) `SourceOvertimeRequestId` trỏ tới `OvertimeRequest` đã `Approved` trước đó, phục vụ truy vết nghỉ bù.
- **BR-18 (Khoá sửa/xoá đơn đã vào tiến trình duyệt)**: Request có `Status` là `InProgress` (đã qua ít nhất 1 cấp duyệt), `Approved` không được nhân viên tự sửa/xoá/huỷ trực tiếp — áp dụng lại BR-06 của Approval Engine.
- **BR-19 (Punch Correction chỉ có hiệu lực sau khi Approved)**: `PunchCorrectionRequest` chỉ được ghi nhận (áp dụng) vào dữ liệu chấm công thực tế của module Timekeeping **sau khi** `ApprovalWorkflowCompleted` — không ghi đè trực tiếp khi mới `Submitted`, đảm bảo toàn vẹn dữ liệu chấm công gốc.

## 1.5 Đề xuất DB Schema

### Approval Engine

**`App_ApprovalMatrix`**

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| `Id` | PK | |
| `RequestTypeCode` | varchar(30) | `UNIQUE NOT NULL` |
| `RequestTypeName` | nvarchar(200) | `NOT NULL` |
| `BoundedContext` | varchar(50) | `NOT NULL` |
| `TotalLevels` | int | `NOT NULL DEFAULT 0` |
| `IsActive` | bit | `NOT NULL DEFAULT 1` |
| `EffectiveFromDate` | date | `NOT NULL` |
| `CreatedAtUtc/CreatedBy/LastModifiedAtUtc/LastModifiedBy` | audit | |
| `IsDeleted/DeletedAtUtc` | soft-delete | |

Index: `UX_ApprovalMatrix_RequestTypeCode (RequestTypeCode)`.

**`App_ApprovalMatrixLevel`**

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| `Id` | PK | |
| `ApprovalMatrixId` | FK → `App_ApprovalMatrix.Id` | `NOT NULL` |
| `Level` | int | `NOT NULL` |
| `DutyId` | FK → `Emp_Duty.Id` (module 01) | `NOT NULL` |
| `OrgScope` | varchar(20) | `NOT NULL` |
| `IsRequired` | bit | `NOT NULL DEFAULT 1` |
| audit | | |

Index: `UX_ApprovalMatrixLevel_Matrix_Level (ApprovalMatrixId, Level)`.

**`App_ApprovalWorkflow`**

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| `Id` | PK | |
| `RequestTypeCode` | varchar(30) | `NOT NULL` (tham chiếu mềm) |
| `SourceBoundedContext` | varchar(50) | `NOT NULL` |
| `SourceDocumentId` | Id | `NOT NULL` (không FK cứng — đa hình) |
| `RequesterEmployeeId` | FK → `Emp_Employee.Id` | `NOT NULL` |
| `RequesterDepartmentId` | FK → `Emp_Department.Id` | `NULL` |
| `RequesterSectionId` | FK → `Emp_Team.Id` (xem quy ước Section=Team tại `01-employee-profile.md` §1.3.8) | `NULL` |
| `RequesterSubsectionId` | FK → `Emp_Subsection.Id` (bảng tuỳ chọn, chỉ tồn tại nếu doanh nghiệp cần cấp tổ chức sâu hơn Team — xem `01-employee-profile.md` §1.3.8) | `NULL` |
| `TotalLevels` | int | `NOT NULL` |
| `CurrentLevel` | int | `NOT NULL DEFAULT 1` |
| `Status` | varchar(20) | `NOT NULL DEFAULT 'InProgress'` |
| `StartedAtUtc` | datetime2 | `NOT NULL` |
| `CompletedAtUtc` | datetime2 | `NULL` |
| audit + soft-delete | | |

Index: `IX_ApprovalWorkflow_SourceDocument (SourceBoundedContext, RequestTypeCode, SourceDocumentId)`, `IX_ApprovalWorkflow_Requester (RequesterEmployeeId, Status)`.

**`App_ApprovalStep`**

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| `Id` | PK | |
| `ApprovalWorkflowId` | FK → `App_ApprovalWorkflow.Id` | `NOT NULL` |
| `Level` | int | `NOT NULL` |
| `ApproverEmployeeId` | FK → `Emp_Employee.Id` | `NULL` |
| `ApproverDutyId` | FK → `Emp_Duty.Id` | `NOT NULL` |
| `Status` | varchar(20) | `NOT NULL DEFAULT 'Pending'` |
| `DecisionAtUtc` | datetime2 | `NULL` |
| `Comment` | nvarchar(1000) | `NULL` |
| `IsAutoSkipped` | bit | `NOT NULL DEFAULT 0` |
| `NotifiedAtUtc` | datetime2 | `NULL` |
| audit | | |

Index: `UX_ApprovalStep_Workflow_Level (ApprovalWorkflowId, Level)`, `IX_ApprovalStep_Approver (ApproverEmployeeId, Status)` — phục vụ màn hình "Đơn chờ tôi duyệt".

**`App_ApprovalHistory`**

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| `Id` | PK | |
| `ApprovalWorkflowId` | FK → `App_ApprovalWorkflow.Id` | `NOT NULL` |
| `ApprovalStepId` | FK → `App_ApprovalStep.Id` | `NULL` |
| `Level` | int | `NULL` |
| `ActorEmployeeId` | FK → `Emp_Employee.Id` | `NULL` |
| `Action` | varchar(20) | `NOT NULL` |
| `ActionAtUtc` | datetime2 | `NOT NULL` |
| `Comment` | nvarchar(1000) | `NULL` |
| `SnapshotStatus` | varchar(20) | `NOT NULL` |
| `CreatedAtUtc` | datetime2 | `NOT NULL` |

Index: `IX_ApprovalHistory_Workflow (ApprovalWorkflowId, ActionAtUtc)`.

### Employee Self-Service Request

Mỗi bảng dưới đây **lặp lại đầy đủ field base** (`ApplicationNo`, `EmployeeId`, `DepartmentId`, `SectionId`, `SubsectionId`, `ProcessId`, `Reason`, `Status`, `ApprovalWorkflowId`, audit, soft-delete) + field riêng của loại đơn (TPC — mục 0.3).

**`Req_LeaveRequest`**: + `AbsentTypeId (FK)`, `LeaveFromDate`, `LeaveToDate`, `TotalTime`, `OffsetTime`, `TimeAllowanceHours`, `IsCompensatory`, `SourceOvertimeRequestId (FK → Req_OvertimeRequest.Id, NULL)`.
Index: `IX_LeaveRequest_Employee_Status (EmployeeId, Status)`, `IX_LeaveRequest_DateRange (LeaveFromDate, LeaveToDate)`.

**`Req_LeaveRequestDetail`**: `Id (PK)`, `LeaveRequestId (FK → Req_LeaveRequest.Id)`, `LeaveDate`, `ShiftId (FK)`, `DailyHours`, `Status`.
Index: `UX_LeaveRequestDetail_Request_Date (LeaveRequestId, LeaveDate)`.

**`Req_OvertimeRequest`**: + `OvertimeDate`, `ShiftId (FK)`, `PlannedFromTime`, `PlannedToTime`, `OvertimeHours`, `OvertimeHoursNormalDay`, `OvertimeHoursCompany`, `IsPreShiftOT`.
Index: `IX_OvertimeRequest_Employee_Month (EmployeeId, OvertimeDate)` — phục vụ tính trần 40h/tháng (BR-13).

**`Req_OutOfOfficeRequest`**: + `OutDate`, `FromTime`, `ToTime`, `Place`, `GoStraight`, `ComeStraight`, `IsOnTripOT`.

**`Req_PunchCorrectionRequest`**: + `WorkingDate`, `PunchDateTime`, `InOutType`, `EditType`.
Index: `IX_PunchCorrectionRequest_Employee_WorkingDate (EmployeeId, WorkingDate)`.

**`Req_ShiftSwapRequest`**: + `RequestedShiftId (FK)`, `FromWorkingDate`, `ToWorkingDate`, `SwapWithEmployeeId (FK, NULL)`.

**`Req_BusinessTripRequest`**: + `TripPurpose`, `Destination`, `GoFromDate`, `GoToDate`, `EstimatedAdvanceAmount`, `ExternalAdvanceVoucherRef`.

Tất cả 6 bảng trên có `IX_<Table>_EmployeeId_MonthYear` phục vụ tính quota BR-15 (đếm số đơn/tháng/loại) — có thể derive từ cột ngày chính của mỗi bảng (`LeaveFromDate`, `OvertimeDate`, `OutDate`, `WorkingDate`, `FromWorkingDate`, `GoFromDate`).

## 1.6 Quan hệ với Bounded Context khác

**Consumer của Approval Engine (1a)** — liệt kê đầy đủ theo khảo sát + suy luận từ context map (`00-domain-overview.md`):

| Bounded Context | Loại chứng từ dùng Approval Engine | Ghi chú |
|---|---|---|
| `05-request-approval.md` (chính module này, phần 1b) | Leave/Overtime/OutOfOffice/PunchCorrection/ShiftSwap/BusinessTrip Request | Consumer điển hình, thiết kế mẫu cho các module khác |
| `02-recruitment.md` | Kế hoạch tuyển dụng (Recruitment Plan), Kết quả thử việc (Probation Result) | |
| `03-labor-contract.md` | Ký số hợp đồng lao động (Labor Contract Signing) | Có thể có thêm bước "ký" ngoài "duyệt" — xử lý ở module 03, chỉ dùng Engine cho bước duyệt nội bộ trước khi gửi ký |
| `04-timekeeping-shift.md` | Đăng ký/phân ca (Shift Registration) | Trạng thái `Status = 2` (Rejected/cần xem lại) trong màn hình cũ `HRMF1260` ánh xạ trực tiếp `ApprovalStepStatus.Rejected` |
| `09-training.md` | Kế hoạch đào tạo, Kết quả đào tạo | |
| `11-personnel-decisions.md` | Quyết định bổ nhiệm/miễn nhiệm/điều chuyển/thôi việc | |

**Quan hệ khác**:
- `Request & Approval` (phần 1b) → `Timekeeping & Shift` (04): `LeaveRequest`/`PunchCorrectionRequest`/`ShiftSwapRequest` khi `Approved` sẽ ghi nhận/điều chỉnh dữ liệu chấm công thực tế — quan hệ Upstream (Request) → Downstream (Timekeeping), thực thi qua domain event (`LeaveRequestApproved`, `PunchCorrectionApproved`...), không gọi trực tiếp cross-aggregate.
- `Request & Approval` → `Payroll` (06): gián tiếp qua Timekeeping — số liệu nghỉ phép/OT ảnh hưởng công chấm, công chấm ảnh hưởng tính lương; module 05 không phụ thuộc trực tiếp module 06.
- `Request & Approval` ↔ `Employee Profile` (01): mọi `EmployeeId`, org snapshot (`DepartmentId`/`SectionId`/`SubsectionId`/`ProcessId`), `DutyId` đều tham chiếu tới Employee Profile — Employee Profile là upstream.
- `Request & Approval` → module Kế toán/Tài chính (ngoài phạm vi HRM): `BusinessTripRequest` có thể sinh phiếu tạm ứng — chỉ ghi nhận là **integration point** (`ExternalAdvanceVoucherRef`), không mô hình hoá chi tiết.
- Cấu hình `HF0390` (Payroll Policy — `06-payroll.md`) là **upstream Configuration Aggregate** cho các BR-11..BR-15 (leave borrowing cap, OT cap, request quota) — module 05 chỉ đọc, không sở hữu.

---

# Phần 2 — BDD (Behavior-Driven Development)

## 2.1 User Stories

**Vai trò Approver (người duyệt — áp dụng chung mọi loại chứng từ qua Approval Engine)**:
- Là **Approver**, tôi muốn thấy danh sách "Đơn chờ tôi duyệt" tổng hợp từ mọi loại chứng từ, để xử lý duyệt tập trung một nơi.
- Là **Approver**, tôi muốn duyệt hoặc từ chối 1 cấp duyệt kèm ý kiến, để luồng duyệt tiếp tục hoặc dừng lại đúng ý tôi.
- Là **Approver**, tôi muốn hệ thống tự động bỏ qua cấp duyệt của tôi nếu tôi đã duyệt ở cấp liền trước, để không phải duyệt lặp vô nghĩa.
- Là **Approver**, tôi muốn nhận thông báo ngay khi có đơn chờ tôi duyệt, để không bỏ sót.

**Vai trò Employee (nhân viên — người tạo đơn)**:
- Là **Employee**, tôi muốn tạo đơn xin nghỉ phép cho 1 hoặc nhiều ngày, để xin nghỉ hợp lệ và được duyệt đúng quy trình.
- Là **Employee**, tôi muốn biết số ngày phép còn lại và có thể mượn phép trong hạn mức cho phép, để không bị từ chối đơn oan.
- Là **Employee**, tôi muốn tạo đơn xin làm thêm giờ, với hệ thống tự kiểm tra tôi có đủ điều kiện (chức danh, chưa vượt trần giờ OT tháng) trước khi gửi duyệt.
- Là **Employee**, tôi muốn tạo đơn xin ra ngoài trong giờ làm, ghi rõ nơi đến và có đi/về thẳng hay không.
- Là **Employee**, tôi muốn tạo đơn xin bổ sung dữ liệu quẹt thẻ khi quên chấm công, để công của tôi được ghi nhận đúng.
- Là **Employee**, tôi muốn tạo đơn xin đổi ca làm việc.
- Là **Employee**, tôi muốn theo dõi trạng thái đơn đã nộp (đang chờ duyệt ở cấp nào, đã duyệt hay bị từ chối kèm lý do).
- Là **Employee**, tôi muốn huỷ đơn khi đơn chưa được duyệt ở cấp nào, và không thể sửa/xoá đơn đã có tiến trình duyệt.

## 2.2 Gherkin Scenarios

```gherkin
Feature: Approval Engine - Khởi tạo luồng duyệt đúng số cấp cấu hình

  Background:
    Given loại chứng từ "DXP" (Leave Request) được cấu hình trong Approval Matrix với 3 cấp duyệt:
      | Level | DutyId          | OrgScope   |
      | 1     | Tổ trưởng       | Subsection |
      | 2     | Trưởng phòng    | Department |
      | 3     | Giám đốc Nhân sự| Company    |

  @BR-01
  Scenario: Submit đơn tạo đủ số dòng ApprovalStep theo cấu hình
    Given nhân viên "NV001" thuộc Tổ "T01", Phòng "P01"
    When "NV001" submit 1 Leave Request
    Then hệ thống tạo 1 ApprovalWorkflow với TotalLevels = 3
    And hệ thống tạo đúng 3 dòng ApprovalStep (Level 1, 2, 3) đều ở trạng thái Pending
    And ApprovalWorkflow.Status = "InProgress"
    And ApprovalWorkflow.CurrentLevel = 1

  @BR-01
  Scenario: Phát hiện đơn thiếu ApprovalStep là lỗi toàn vẹn dữ liệu
    Given 1 ApprovalWorkflow có TotalLevels = 3
    But chỉ tồn tại 2 dòng ApprovalStep trong hệ thống
    When chạy kiểm tra toàn vẹn dữ liệu "HasMissingApprovalSteps"
    Then hệ thống báo cáo ApprovalWorkflow này là lỗi dữ liệu cần xử lý thủ công
```

```gherkin
Feature: Approval Engine - Duyệt đa cấp thành công (happy path)

  @BR-02 @BR-07
  Scenario: Duyệt lần lượt qua đủ 3 cấp thì luồng hoàn tất
    Given 1 ApprovalWorkflow của Leave Request "NV001" đang ở Level 1, Pending, người duyệt cấp 1 là "Tổ trưởng T01"
    When "Tổ trưởng T01" duyệt cấp 1 với ý kiến "Đồng ý"
    Then ApprovalStep Level 1 chuyển "Approved"
    And hệ thống resolve người duyệt cấp 2 là "Trưởng phòng P01" và gửi thông báo cho họ
    And ApprovalWorkflow.CurrentLevel = 2

    When "Trưởng phòng P01" duyệt cấp 2 với ý kiến "OK"
    Then ApprovalStep Level 2 chuyển "Approved"
    And ApprovalWorkflow.CurrentLevel = 3

    When "Giám đốc Nhân sự" duyệt cấp 3 với ý kiến "Duyệt"
    Then ApprovalStep Level 3 chuyển "Approved"
    And ApprovalWorkflow.Status = "Approved"
    And sự kiện ApprovalWorkflowCompleted được phát sinh
    And "NV001" nhận được thông báo đơn đã được duyệt toàn bộ
```

```gherkin
Feature: Approval Engine - Từ chối dừng luồng

  @BR-05 @BR-07
  Scenario: Từ chối ở cấp 2 dừng toàn bộ luồng duyệt
    Given 1 ApprovalWorkflow của Leave Request "NV001" đã Approved ở Level 1, đang chờ duyệt Level 2
    When "Trưởng phòng P01" từ chối cấp 2 với lý do "Không đủ nhân sự trực thay"
    Then ApprovalStep Level 2 chuyển "Rejected"
    And ApprovalWorkflow.Status = "Rejected" ngay lập tức
    And ApprovalStep Level 3 vẫn giữ "Pending" và không còn khả năng được duyệt
    And Leave Request tương ứng chuyển Status = "Rejected"
    And "NV001" nhận được thông báo đơn bị từ chối kèm lý do
```

```gherkin
Feature: Approval Engine - Auto-skip khi trùng người duyệt liên tiếp

  @BR-03 @BR-04
  Scenario: Người duyệt cấp 2 trùng người duyệt cấp 1 thì tự động Skip
    Given nhân viên "NV002" vừa là Tổ trưởng vừa kiêm nhiệm Trưởng phòng của Phòng mình
    And 1 ApprovalWorkflow của Leave Request "NV002" có 3 cấp, Level 1 và Level 2 đều resolve ra cùng 1 người là "NV002" (do kiêm nhiệm)
    When "NV002" duyệt cấp 1
    Then ApprovalStep Level 1 chuyển "Approved"
    And hệ thống resolve người duyệt Level 2, phát hiện trùng "NV002"
    And ApprovalStep Level 2 tự động chuyển "Skipped", IsAutoSkipped = true
    And ApprovalWorkflow.CurrentLevel chuyển thẳng sang Level 3
    And "NV002" không nhận thêm thông báo duyệt cho Level 2

  @BR-04
  Scenario: Đệ quy resolve người duyệt vượt quá 10 cấp bị chặn
    Given 1 ApprovalMatrix (cấu hình sai) khiến chuỗi auto-skip resolve liên tiếp trùng người duyệt hơn 10 lần
    When hệ thống thực hiện ResolveAndAssignNextApprover
    Then quá trình đệ quy dừng lại sau lần gọi thứ 10
    And ApprovalWorkflow chuyển trạng thái lỗi cấu hình, không tự động Approved
    And ghi nhận sự kiện lỗi vào ApprovalHistory để admin xử lý cấu hình Approval Matrix
```

```gherkin
Feature: Approval Engine - Khoá sửa/xoá chứng từ đã có tiến trình duyệt

  @BR-06 @BR-18
  Scenario: Không cho sửa đơn đã được duyệt ít nhất 1 cấp
    Given Leave Request "REQ-0001" của "NV001" đã được Approved ở Level 1
    When "NV001" thử sửa nội dung đơn "REQ-0001"
    Then hệ thống từ chối thao tác với thông báo "Đơn đã có tiến trình duyệt, không thể sửa trực tiếp"

  @BR-06 @BR-18
  Scenario: Không cho xoá đơn đã Approved toàn bộ
    Given Leave Request "REQ-0002" của "NV001" đã Status = "Approved"
    When "NV001" thử xoá đơn "REQ-0002"
    Then hệ thống từ chối thao tác

  Scenario: Cho phép huỷ đơn khi chưa qua cấp duyệt nào
    Given Leave Request "REQ-0003" của "NV001" đang Status = "InProgress" nhưng cả 3 ApprovalStep vẫn "Pending"
    When "NV001" huỷ đơn "REQ-0003"
    Then ApprovalWorkflow chuyển "Cancelled"
    And Leave Request "REQ-0003" chuyển Status = "Cancelled"
```

```gherkin
Feature: Leave Request - Submit đơn nghỉ phép & mượn phép

  @BR-11
  Scenario: Nghỉ phép trong hạn mức mượn phép của năm
    Given "NV001" còn 2 ngày phép, đã mượn 1 ngày trong năm nay, MaxBorrowLeaveYear = 3
    And tháng hiện tại nằm trong MonthAllowBorrowLeave
    When "NV001" xin nghỉ 3 ngày (vượt số dư 2 ngày, mượn thêm 1 ngày)
    Then đơn được chấp nhận submit (tổng mượn trong năm = 2, vẫn <= 3)
    And ApprovalWorkflow được khởi tạo

  @BR-11
  Scenario: Nghỉ phép vượt trần mượn phép năm bị từ chối ngay khi submit
    Given "NV001" đã mượn 3/3 ngày phép được phép mượn trong năm (MaxBorrowLeaveYear = 3)
    And "NV001" không còn ngày phép nào trong số dư thực tế
    When "NV001" xin nghỉ thêm 1 ngày phép
    Then hệ thống từ chối submit với lý do "Vượt hạn mức mượn phép trong năm"
    And không có ApprovalWorkflow nào được tạo

  @BR-12
  Scenario: Mượn phép ngoài tháng được phép mượn bị từ chối
    Given MonthAllowBorrowLeave chỉ cho phép các tháng 1,2,3
    And hiện tại là tháng 7
    And "NV001" không còn đủ số dư phép cho số ngày xin nghỉ
    When "NV001" xin nghỉ vượt số dư phép hiện có
    Then hệ thống từ chối submit với lý do "Tháng hiện tại không được phép mượn"

  @BR-15
  Scenario Outline: Giới hạn số đơn nghỉ phép mỗi tháng
    Given MaxRequestDXP = 3 và "NV001" đã tạo <SoDonDaTao> đơn Leave Request trong tháng
    When "NV001" tạo thêm 1 đơn Leave Request
    Then kết quả submit là "<KetQua>"

    Examples:
      | SoDonDaTao | KetQua      |
      | 2          | Thành công  |
      | 3          | Bị từ chối  |
```

```gherkin
Feature: Overtime Request - Trần giờ OT theo tháng & điều kiện chức danh

  @BR-13
  Scenario Outline: Kiểm tra trần 40 giờ OT/tháng ở các ranh giới
    Given "NV003" đã có <GioOTDaDangKy> giờ OT được duyệt/đang chờ duyệt trong tháng
    And trần OT tháng cấu hình là 40 giờ
    When "NV003" xin thêm OT với số giờ "<GioOTMoi>"
    Then kết quả submit là "<KetQua>"

    Examples:
      | GioOTDaDangKy | GioOTMoi | KetQua      |
      | 38             | 1.98     | Thành công  |
      | 38             | 2        | Thành công  |
      | 38             | 2.01     | Bị từ chối  |
      | 40             | 0.01     | Bị từ chối  |

  @BR-14
  Scenario: Chức danh không được phép tạo đơn OT
    Given "NV004" có chức danh "Thực tập sinh" không nằm trong danh sách được phép tạo Overtime Request
    When "NV004" thử tạo đơn xin làm thêm giờ
    Then hệ thống từ chối submit với lý do "Chức danh không được phép tạo đơn OT"

  @BR-15
  Scenario: Vượt số lượng đơn OT tối đa trong tháng
    Given MaxRequestDXLTG = 5 và "NV003" đã tạo 5 đơn Overtime Request hợp lệ trong tháng
    When "NV003" tạo thêm 1 đơn Overtime Request
    Then hệ thống từ chối submit với lý do "Vượt số lượng đơn tối đa trong tháng"
```

```gherkin
Feature: Out-of-Office / Punch Correction / Shift Swap Request

  Scenario: Nhân viên tạo đơn xin ra ngoài đi thẳng không quay lại công ty
    Given "NV005" cần ra ngoài gặp khách hàng lúc 14:00
    When "NV005" tạo Out-of-Office Request với Place = "Khách hàng ABC", GoStraight = true, ComeStraight = false
    Then đơn được submit và ApprovalWorkflow theo Approval Matrix của loại "DXRN" được khởi tạo

  @BR-19
  Scenario: Punch Correction chỉ áp dụng vào bảng chấm công sau khi Approved
    Given "NV006" quên quẹt thẻ giờ ra ngày 10/07/2026
    And "NV006" tạo Punch Correction Request cho ngày đó, InOutType = "Out"
    When đơn vừa Submit, còn ở trạng thái "InProgress"
    Then dữ liệu chấm công thực tế của ngày 10/07/2026 CHƯA bị thay đổi
    When đơn được duyệt qua hết các cấp, ApprovalWorkflowCompleted phát sinh
    Then dữ liệu chấm công thực tế của ngày 10/07/2026 được cập nhật theo đề nghị

  Scenario: Nhân viên tạo đơn xin đổi ca
    Given "NV007" đang được phân ca "Ca sáng" ngày 15/07/2026
    When "NV007" tạo Shift Swap Request đổi sang "Ca chiều" áp dụng cho ngày 15/07/2026
    Then đơn được submit và ApprovalWorkflow theo Approval Matrix của loại "DXDC" được khởi tạo
```

```gherkin
Feature: Request quota per month - áp dụng chung cho mọi loại đơn

  @BR-15
  Scenario Outline: Kiểm tra hạn mức số đơn theo từng loại request
    Given cấu hình <TranSoDon> cho loại đơn "<LoaiDon>" là <MaxRequest>
    And nhân viên đã tạo <SoDonHienTai> đơn loại "<LoaiDon>" hợp lệ trong tháng hiện tại
    When nhân viên tạo thêm 1 đơn loại "<LoaiDon>"
    Then kết quả là "<KetQua>"

    Examples:
      | TranSoDon         | LoaiDon             | MaxRequest | SoDonHienTai | KetQua      |
      | MaxRequestDXRN     | OutOfOffice          | 4          | 3            | Thành công  |
      | MaxRequestDXRN     | OutOfOffice          | 4          | 4            | Bị từ chối  |
      | MaxRequestDXBSQT   | PunchCorrection      | 3          | 2            | Thành công  |
      | MaxRequestDXBSQT   | PunchCorrection      | 3          | 3            | Bị từ chối  |
```

---

# Phần 3 — TDD (Test-Driven Development)

## 3.1 Bảng Test Case

| TC | Scenario liên quan | BR | Loại test | Input | Expected Output |
|---|---|---|---|---|---|
| TC-01 | Submit tạo đủ N ApprovalStep | BR-01 | Unit | ApprovalMatrix(DXP, 3 cấp) + Submit LeaveRequest | 1 ApprovalWorkflow (TotalLevels=3) + 3 ApprovalStep (Pending) |
| TC-02 | Phát hiện thiếu ApprovalStep | BR-01 | Unit | ApprovalWorkflow(TotalLevels=3) chỉ có 2 ApprovalStep | `HasMissingApprovalSteps()` = true |
| TC-03 | Duyệt đủ 3 cấp → Completed | BR-02, BR-07 | Integration | 3 lần gọi `ApproveStep` liên tiếp đúng approver từng cấp | Workflow.Status = Approved; event `ApprovalWorkflowCompleted` phát sinh đúng 1 lần |
| TC-04 | Resolve approver theo DutyId + OrgScope | BR-02 | Unit | ApprovalMatrixLevel(Level=1, DutyId=Tổ trưởng, OrgScope=Subsection), Requester.SubsectionId=T01 | `ResolveApprover()` trả về nhân viên giữ DutyId=Tổ trưởng của T01 |
| TC-05 | Reject cấp 2 dừng luồng | BR-05 | Integration | Approve Level 1, Reject Level 2 | Workflow.Status=Rejected; Level 3 vẫn Pending, không thể `ApproveStep(Level=3)` (phải throw `InvalidOperationException`) |
| TC-06 | Auto-skip khi trùng approver | BR-03 | Unit | Level 1 approver = X (đã Approved); resolve Level 2 cũng ra X | ApprovalStep Level 2: Status=Skipped, IsAutoSkipped=true; CurrentLevel nhảy sang Level 3 |
| TC-07 | Đệ quy resolve vượt 10 cấp | BR-04 | Unit | Mock resolver luôn trả về cùng 1 người 12 lần liên tiếp | Dừng ở lần thứ 10, ném lỗi cấu hình `ApprovalMatrixMisconfiguredException` |
| TC-08 | Khoá sửa đơn đã duyệt 1 cấp | BR-06, BR-18 | Unit | LeaveRequest.Status=InProgress, có 1 ApprovalStep Approved | `Update()` ném `RequestLockedException` |
| TC-09 | Khoá xoá đơn Approved | BR-06, BR-18 | Unit | LeaveRequest.Status=Approved | `Delete()` ném `RequestLockedException` |
| TC-10 | Huỷ đơn khi chưa duyệt cấp nào | BR-18 | Unit | LeaveRequest.Status=InProgress, tất cả ApprovalStep=Pending | `Cancel()` thành công, Workflow=Cancelled |
| TC-11 | Mượn phép trong hạn mức | BR-11 | Unit | Đã mượn 1/3 ngày trong năm, xin thêm 1 ngày (số dư không đủ 1 ngày) | Submit thành công, tổng mượn = 2 |
| TC-12 | Mượn phép vượt hạn mức năm | BR-11 | Unit | Đã mượn 3/3 ngày, xin thêm ngày mượn | Submit bị từ chối `LeaveBorrowingCapExceededException` |
| TC-13 | Mượn phép ngoài tháng cho phép | BR-12 | Unit | Tháng hiện tại không thuộc MonthAllowBorrowLeave, cần mượn | Submit bị từ chối |
| TC-14 (boundary) | OT 38h + 1.98h = 39.98h | BR-13 | Unit | Đã có 38h, đăng ký thêm 1.98h | Submit thành công (39.98 <= 40) |
| TC-15 (boundary) | OT 38h + 2h = 40h (đúng biên) | BR-13 | Unit | Đã có 38h, đăng ký thêm 2h | Submit thành công (= 40, không vượt) |
| TC-16 (boundary) | OT 38h + 2.01h = 40.01h | BR-13 | Unit | Đã có 38h, đăng ký thêm 2.01h | Submit bị từ chối `MonthlyOvertimeCapExceededException` |
| TC-17 (boundary) | OT 40h + 0.01h | BR-13 | Unit | Đã đủ 40h, đăng ký thêm 0.01h | Submit bị từ chối |
| TC-18 | Chức danh không đủ điều kiện tạo OT | BR-14 | Unit | Employee.DutyId không thuộc allowed list | Submit bị từ chối `PositionNotEligibleForOvertimeException` |
| TC-19 (boundary) | Số đơn Leave = MaxRequest - 1 | BR-15 | Unit | Đã tạo 2/3 đơn DXP trong tháng | Submit thành công |
| TC-20 (boundary) | Số đơn Leave = MaxRequest | BR-15 | Unit | Đã tạo 3/3 đơn DXP trong tháng | Submit bị từ chối `MonthlyRequestQuotaExceededException` |
| TC-21 | Punch Correction chưa Approved không ảnh hưởng chấm công | BR-19 | Integration | Submit PunchCorrectionRequest, chưa duyệt | Bảng TimeLog không đổi |
| TC-22 | Punch Correction Approved áp dụng vào chấm công | BR-19 | Integration | ApprovalWorkflowCompleted của PunchCorrectionRequest | Bảng TimeLog được cập nhật đúng theo đề nghị |
| TC-23 | Thông báo gửi đúng người duyệt kế tiếp | BR-07 | Unit | ApproveStep(Level=1) thành công | `INotificationService.Notify` được gọi với ApproverEmployeeId của Level 2 |
| TC-24 | Snapshot org không đổi khi nhân viên bị điều chuyển sau khi tạo đơn | BR-16 | Unit | Request đã tạo với DepartmentId=P01; sau đó Employee đổi sang P02 | Request.DepartmentId vẫn = P01, Workflow resolve theo P01 |
| TC-25 | Nghỉ bù liên kết OT nguồn | BR-17 | Unit | LeaveRequest.IsCompensatory=true, SourceOvertimeRequestId trỏ tới OvertimeRequest đã Approved | Submit thành công; validate fail nếu SourceOvertimeRequestId trỏ tới OT chưa Approved |
| TC-26 | ApprovalHistory append-only | BR-10 | Unit | Gọi `UpdateHistory()`/`DeleteHistory()` (không tồn tại API sửa/xoá) | Không có method nào cho phép sửa/xoá bản ghi History — chỉ có `Append()` |
| TC-27 | Request vào đúng ranh giới cuối tháng/đầu tháng | Edge case | Integration | OvertimeRequest ngày 31/07 23:59 và 01/08 00:01 tính vào 2 tháng khác nhau khi cộng dồn trần OT | Giờ OT được cộng dồn theo đúng tháng của `OvertimeDate`, không lẫn giữa 2 tháng |

## 3.2 Gợi ý chữ ký Class/Interface (C#-style)

```csharp
// ===== Approval Engine (generic, domain-agnostic) =====

public interface IApprovalEngine
{
    ApprovalWorkflow StartWorkflow(StartApprovalWorkflowRequest request);

    void ApproveStep(Guid approvalWorkflowId, int level, Guid approverEmployeeId, string comment);

    void RejectStep(Guid approvalWorkflowId, int level, Guid approverEmployeeId, string comment);

    void CancelWorkflow(Guid approvalWorkflowId, Guid actorEmployeeId, string reason);

    // BR-02 / BR-03 / BR-04
    Guid? ResolveNextApprover(ApprovalMatrixLevel level, OrgSnapshot requesterOrg, Guid? previousApproverEmployeeId, int recursionDepth = 0);

    IReadOnlyList<ApprovalStep> GetPendingStepsForApprover(Guid approverEmployeeId);

    // BR-01 — data integrity check, chạy khi Start và/hoặc theo job định kỳ
    bool HasMissingApprovalSteps(Guid approvalWorkflowId);
}

public class StartApprovalWorkflowRequest
{
    public string RequestTypeCode { get; set; }
    public string SourceBoundedContext { get; set; }
    public Guid SourceDocumentId { get; set; }
    public Guid RequesterEmployeeId { get; set; }
    public OrgSnapshot RequesterOrg { get; set; }
}

public record OrgSnapshot(Guid? DepartmentId, Guid? SectionId, Guid? SubsectionId, Guid? ProcessId);

public interface IApprovalMatrixService
{
    ApprovalMatrix GetActiveMatrix(string requestTypeCode);
    void AddLevel(string requestTypeCode, int level, Guid dutyId, OrgScope orgScope, bool isRequired = true);
    void RemoveLevel(string requestTypeCode, int level); // chỉ ảnh hưởng workflow future — BR-09
}

public interface IApprovalHistoryService
{
    void Append(Guid approvalWorkflowId, Guid? approvalStepId, int? level, Guid? actorEmployeeId,
        ApprovalHistoryAction action, string comment, string snapshotStatus); // append-only — BR-10
    IReadOnlyList<ApprovalHistory> GetTimeline(Guid approvalWorkflowId);
}

// ===== Employee Self-Service Request =====

public interface ILeaveRequestService
{
    Guid Submit(SubmitLeaveRequestCommand command); // ném exception nếu vi phạm BR-11/12/15

    decimal GetRemainingLeave(Guid employeeId, int year);              // GetRemainingLeave
    BorrowLeaveInfo GetDataBorrowLeave(Guid employeeId, int year);     // GetDataBorrowLeave
    bool CheckIsAnnualLeave(Guid absentTypeId);                       // CheckIsAnnualLeave
    void ValidateLeaveBalance(Guid employeeId, DateOnly from, DateOnly to, decimal totalTime);
    void ValidateBorrowingCap(Guid employeeId, int year, int month, decimal requestedBorrowDays);

    void Cancel(Guid leaveRequestId, Guid actorEmployeeId);
}

public interface IOvertimeRequestService
{
    Guid Submit(SubmitOvertimeRequestCommand command); // ném exception nếu vi phạm BR-13/14/15

    decimal GetMaximumOvertimeHours(Guid employeeId, int year, int month); // GetMaximumOvertimeHours
    void ValidateMonthlyCap(Guid employeeId, int year, int month, decimal additionalHours);
    void ValidatePositionEligibility(Guid employeeId);                    // CheckPositionAllowCreateOTRequest
    void CheckDataOvertime40(Guid employeeId, int year, int month, decimal additionalHours); // trần luật lao động VN
}

public interface IOutOfOfficeRequestService
{
    Guid Submit(SubmitOutOfOfficeRequestCommand command);
}

public interface IPunchCorrectionRequestService
{
    Guid Submit(SubmitPunchCorrectionRequestCommand command);
    void ApplyToTimekeeping(Guid punchCorrectionRequestId); // gọi khi ApprovalWorkflowCompleted — BR-19
}

public interface IShiftSwapRequestService
{
    Guid Submit(SubmitShiftSwapRequestCommand command);
}

public interface IRequestQuotaService
{
    int CountCurrentRequest(Guid employeeId, string requestTypeCode, int year, int month); // CountCurrentRequest
    int GetMaxRequest(string requestTypeCode);                                             // GetMaxRequest (HF0390)
    void ValidateMonthlyQuota(Guid employeeId, string requestTypeCode, int year, int month); // BR-15, ném MonthlyRequestQuotaExceededException
}
```

## 3.3 Edge Cases cần test kỹ

1. **Đệ quy resolve approver tối đa 10 cấp (BR-04)**: cấu hình lỗi khiến chuỗi auto-skip lặp vô hạn — bắt buộc có test giả lập resolver trả về cùng 1 người liên tục để xác nhận thuật toán dừng đúng ở lần thứ 10 và không treo hệ thống/không StackOverflow.
2. **Request thiếu ApprovalStep (data integrity — BR-01)**: giả lập trạng thái dữ liệu bất thường (VD do lỗi transaction nửa chừng ở hệ thống cũ) để đảm bảo job/kiểm tra định kỳ phát hiện được, không chỉ dựa vào validate lúc Submit.
3. **Request rơi đúng ranh giới tháng**: OvertimeRequest ngày cuối tháng 23:59 vs đầu tháng 00:01 — đảm bảo hàm cộng dồn giờ OT theo tháng (`GetMaximumOvertimeHours`, `CheckDataOvertime40`) nhóm đúng theo `OvertimeDate`, không bị lệch múi giờ (UTC vs giờ địa phương Việt Nam UTC+7) dẫn đến gộp nhầm giờ OT ngày 31 vào tháng sau hoặc ngược lại.
4. **Mượn phép đúng/vượt hạn mức năm, đúng biên**: test hạn mức = số đã mượn (không vượt, phải cho qua) và hạn mức = số đã mượn - 1 (phải chặn phần vượt), tương tự pattern boundary đã áp dụng cho OT 40h.
5. **Auto-skip nhiều cấp liên tiếp cùng 1 người**: không chỉ 2 cấp liền kề, mà 3+ cấp liên tiếp cùng resolve ra 1 người (tổ chức nhỏ, 1 người kiêm nhiều chức dany) — đảm bảo `CurrentLevel` nhảy đúng qua hết các cấp bị skip liên tiếp, không dừng giữa chừng.
6. **Approve/Reject không đúng thứ tự cấp**: gọi `ApproveStep(Level=3)` khi `CurrentLevel` đang là 1 — phải bị chặn (không cho duyệt "nhảy cấp"), kể cả khi người gọi có quyền hợp lệ ở Level 3.
7. **Huỷ workflow đang InProgress giữa chừng (đã duyệt 1 vài cấp)**: xác nhận rule — theo BR-06/BR-18, một khi đã có ≥1 cấp Approved thì không cho Cancel tự do nữa (phải phân biệt rõ với trường hợp toàn bộ còn Pending mới được Cancel tự do ở TC-10).
8. **Trùng `ApplicationNo`**: đảm bảo sinh số đơn không trùng khi nhiều nhân viên submit đồng thời (concurrency), đặc biệt vào đầu giờ làm việc khi lượng đơn tạo dồn dập.
