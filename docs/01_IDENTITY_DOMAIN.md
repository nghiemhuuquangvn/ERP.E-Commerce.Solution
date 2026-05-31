# Phân Hệ Identity & Access Management (IAM) - Thiết Kế Tái Sử Dụng

## 1. Triết Lý Thiết Kế (Design Philosophy)
- **Domain Agnostic:** Phân hệ Identity không chứa bất kỳ logic nào liên quan đến nghiệp vụ ERP, E-commerce hay HRM. Nó chỉ quản lý: Ai là người dùng? Họ thuộc nhóm (Role) nào? Và họ có những quyền hạn (Permission) gì?
- **Granular Permissions (RBAC + PBAC):** Sử dụng Role để gom nhóm người dùng, nhưng hệ thống sẽ kiểm tra quyền hạn dựa trên các chuỗi ký tự Permission cụ thể (Ví dụ: `Users.Create`, `Reports.View`).
- **Token-Based Auth:** Sử dụng cơ chế song hành JWT Access Token (Thời gian sống ngắn) và Refresh Token (Thời gian sống dài, lưu DB để có thể hủy phiên làm việc từ xa).

## 2. Mô Hình Cơ Sở Dữ Liệu Tái Sử Dụng (Database Schema)

Hệ thống sử dụng 5 bảng cốt lõi (Không phụ thuộc vào Entity Framework Identity mặc định để tối ưu hiệu năng và dễ dàng chuyển đổi giữa SQL Server / PostgreSQL):

- **Users:** `Id (Guid)`, `Email (Varchar)`, `PasswordHash (Varchar)`, `IsActive (Bool)`, `CreatedAt (DateTime)`.
- **Roles:** `Id (Guid)`, `Name (Varchar)`, `Description (Varchar)`.
- **Permissions:** `Id (Guid)`, `Code (Varchar - Unique)`, `Description (Varchar)`, `Module (Varchar)`.
- **UserRoles:** `UserId (Guid)`, `RoleId (Guid)` -> Khóa chính phức hợp.
- **RolePermissions:** `RoleId (Guid)`, `PermissionId (Guid)` -> Khóa chính phức hợp.
- **UserRefreshTokens:** `Id (Guid)`, `UserId (Guid)`, `Token (Varchar)`, `ExpiresAt (DateTime)`, `IsRevoked (Bool)`.

## 3. Quản Lý Quyền Hạn Động (Dynamic Permission Ingestion)
Để API này cắm vào bất kỳ dự án nào cũng chạy được, danh mục `Permissions` không được fix cứng (Hardcode) trong DB:
- Mỗi khi một Microservice mới (hoặc dự án mới) khởi chạy, nó sẽ tự quét các Controller/Handler của chính nó để thu thập danh sách các Permission Code hiện có.
- Gửi một danh sách các Permission Code này về Identity Service qua REST API hoặc Kafka để tự động cập nhật (Upsert) vào bảng `Permissions`.

## 4. Cơ Chế Xác Thực Cho Toàn Hệ Thống (Cross-Service Verification)
Khi phân hệ này được dùng trong mô hình nhiều dự án hoặc Microservices, có 2 cách để các dịch vụ khác xác thực Token:

### Cách 1: Stateless Validation (Tối ưu nhất cho các dự án sau)
- Identity Service ký JWT bằng một **Private Key (RSA 256)**.
- Các dự án khác chỉ cần cấu hình Middleware JWT Bearer với **Public Key** tương ứng để tự giải mã và kiểm tra Claims mà không cần gọi ngược lại Identity Service.
- Các Permissions gắn với User sẽ được nén thẳng vào trong JWT Claim dưới dạng mảng string để các service khác tự đọc và phân quyền tại chỗ.

### Cách 2: Introspection Endpoint
- Các service khác gọi API `/api/v1/auth/introspect` gửi kèm Token để Identity Service xác thực thủ công và trả về thông tin User + Permissions.