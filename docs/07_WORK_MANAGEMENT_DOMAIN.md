# Phân Hệ Quản Lý Công Việc & Quy Trình Động (Work Management Domain)

## 1. Triết Lý Thiết Kế
- **Centralized Automation Engine:** Đóng vai trò là lõi xử lý tác vụ công việc và quy trình phê duyệt biểu mẫu cho toàn bộ các phân hệ khác (Cross-cutting Concern Domain).
- **State Machine Architecture:** Quản lý các bước chuyển trạng thái (Transitions) chặt chẽ, đảm bảo tính minh bạch dữ liệu (Audit Trail).

## 2. Các Thực Thể Cốt Lõi (Core Entities & Value Objects)
### 2.1. Aggregate Root: `WorkTask` (Nhiệm vụ/Công việc cá nhân)
- `Id`: `Guid`
- `Title`: `String`
- `Description`: `String`
- `AssigneeId`: `Guid` (ID nhân viên thực hiện từ HRM)
- `ReporterId`: `Guid` (Người giao việc)
- `Priority`: `TaskPriority` (`Low`, `Medium`, `High`)
- `Status`: `TaskStatus` (`ToDo`, `InProgress`, `InReview`, `Done`)
- `DueDate`: `DateTime?`

### 2.2. Aggregate Root: `WorkflowDefinition` (Định nghĩa quy trình duyệt mẫu)
- `Id`: `Guid`
- `WorkflowKey`: `String` (Mã định danh duy nhất, ví dụ: `PURCHASE_ORDER_APPROVAL`, `HRM_LEAVE_APPROVAL`)
- `Name`: `String`
- `Steps`: `List<WorkflowStepDefinition>` (Bước 1 điều kiện là gì, ai duyệt; Bước 2 ai duyệt...)

### 2.3. Aggregate Root: `WorkflowInstance` (Một thực thể quy trình đang chạy)
- `Id`: `Guid`
- `DefinitionId`: `Guid`
- `TargetRecordId`: `Guid` (ID của bản ghi cần duyệt, ví dụ: ID của bản ghi PurchaseOrder cụ thể)
- `CurrentStepOrder`: `Int` (Đang ở bước duyệt thứ mấy)
- `Status`: `InstanceStatus` (`Running`, `Approved`, `Rejected`)
- `ApprovalHistories`: `List<ApprovalActionLog>`

### 2.4. Entity: `ApprovalActionLog` (Nhật ký phê duyệt)
- `Id`: `Guid`
- `ApproverId`: `Guid` (Người bấm duyệt)
- `Action`: `ApprovalAction` (`Approve`, `Reject`, `RequestChanges`)
- `Comment`: `String` (Lý do duyệt/từ chối)
- `Timestamp`: `DateTime`

## 3. Cơ Chế Kết Nối Liên Dịch Vụ (Dynamic Callback Mechanism)
Để phân hệ này duyệt được đơn cho các dịch vụ khác (như PO):
1. PO Service bắn Event `WorkflowStartRequestedEvent` kèm theo `TargetRecordId` và `WorkflowKey = 'PURCHASE_ORDER_APPROVAL'`.
2. Work Management nhận lệnh $\rightarrow$ Khởi tạo `WorkflowInstance` tương ứng $\rightarrow$ Giao Task cho Trưởng phòng.
3. Khi tất cả các bước được duyệt hoàn tất, Work Management xuất bản Event `WorkflowCompletedEvent` chứa `TargetRecordId` để PO Service tự động đổi trạng thái đơn hàng sang `Approved`.

## 4. Các Sự Kiện Tích Hợp (Integration Events)
- `WorkflowStepTriggeredEvent`: Phát ra khi chuyển sang bước duyệt mới $\rightarrow$ Hệ thống gửi tin nhắn thông báo (Zalo/Email) cho người có thẩm quyền tiếp theo vào duyệt.
- `WorkflowCompletedEvent`: Phát ra khi quy trình đi đến bước cuối cùng thành công $\rightarrow$ Trả kết quả về cho phân hệ gốc xử lý logic tiếp theo.
- `WorkflowRejectedEvent`: Phát ra khi có một cấp từ chối đơn $\rightarrow$ Trả kết quả về để phân hệ gốc mở khóa hoặc hủy tài liệu.