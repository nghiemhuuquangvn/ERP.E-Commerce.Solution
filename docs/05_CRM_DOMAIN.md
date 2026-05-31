# Phân Hệ Quách Hàng & Chăm Sóc (CRM Domain)

## 1. Triết Lý Thiết Kế
- **360-Degree Customer View:** Tập trung toàn bộ hành vi mua sắm, khiếu nại, và hạng thành viên về một mối để phục vụ các chiến dịch Automation Marketing.
- **Event-Driven Loyalty:** Tự động hóa việc thăng hạng và trao thưởng dựa trên các sự kiện tiêu dùng thời gian thực từ phân hệ Sales Order.

## 2. Các Thực Thể Cốt Lõi (Core Entities & Value Objects)
### 2.1. Aggregate Root: `CustomerProfile`
- `Id`: `Guid` (Trùng với UserId bên Identity Service)
- `CustomerNumber`: `String`
- `FullName`: `String`
- `Tier`: `CustomerTier` (`Standard`, `Silver`, `Gold`, `Diamond`)
- `TotalLifetimeSpend`: `Decimal` (Tổng chi tiêu tích lũy)
- `CurrentPoints`: `Int` (Điểm khả dụng hiện tại)

### 2.2. Entity: `LoyaltyPointLedger` (Sổ cái tích điểm)
- `Id`: `Guid`
- `CustomerId`: `Guid`
- `PointsChanged`: `Int` (Ví dụ: `+100` khi mua hàng, `-500` khi đổi quà)
- `Reason`: `String`
- `ReferenceSource`: `String` (Mã đơn hàng SO-xxxxx)

### 2.3. Aggregate Root: `SupportTicket` (Hỗ trợ & Khiếu nại)
- `Id`: `Guid`
- `TicketNumber`: `String`
- `CustomerId`: `Guid`
- `Title`: `String`
- `Description`: `String`
- `Priority`: `TicketPriority` (`Low`, `Medium`, `High`, `Critical`)
- `Status`: `TicketStatus` (`New`, `Assigned`, `Resolved`, `Closed`)
- `AssignedEmployeeId`: `Guid?` (Liên kết sang nhân viên xử lý bên HRM)

### 2.4. Value Object: `InteractionHistory` (Nhật ký tương tác)
- `InteractionTime`: `DateTime`
- `Channel`: `String` (`CallCenter`, `MobileApp`, `Email`, `Zalo`)
- `SummaryNotes`: `String`

## 3. Các Sự Kiện Tích Hợp (Integration Events)
- `CustomerTierUpgradedEvent`: Phát ra khi khách hàng đủ doanh số thăng hạng $\rightarrow$ Hệ thống tự động gửi Email chúc mừng và tặng Voucher chào mừng hạng mới vào `VoucherWallet`.
- `SupportTicketEscalatedEvent`: Khi một Ticket có độ ưu tiên `Critical` quá 2 tiếng không được xử lý $\rightarrow$ Bắn thông báo qua Work Management để giao Task khẩn cấp cho Quản lý nhóm.