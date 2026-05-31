# Hướng Dẫn Kiến Trúc Hệ Thống: OOP & AOP Guidelines (.NET 10)

Tài liệu này định nghĩa các quy tắc cốt lõi về Lập trình hướng đối tượng (OOP) áp dụng cho tầng Domain và Lập trình hướng khía cạnh (AOP) áp dụng cho các khía cạnh dùng chung (Cross-cutting concerns) của toàn bộ hệ thống ERP & E-commerce. Claude Code phải tuyệt đối tuân thủ các quy tắc này khi sinh mã nguồn.

## 1. Tiêu Chuẩn OOP & Rich Domain Model (Tầng Domain)

Tầng Domain phải hoàn toàn tinh khiết (Pure), không chứa logic của framework, không chứa dependency của ORM (EF Core) và áp dụng nghiêm ngặt tính đóng gói.

### 1.1. Các Lớp Cơ Sở Định Nghĩa (Core Base Classes)
Mọi thực thể trong hệ thống phải kế thừa từ các lớp cơ sở sau:

- **Entity<TId>:** Định nghĩa một đối tượng có danh tính bất biến thông qua thuộc tính `Id`.
- **AggregateRoot<TId>:** Là một Entity đóng vai trò là cổng giao tiếp duy nhất của một cụm thực thể (Aggregate Window). Chỉ có Aggregate Root mới có Repository tương ứng.
- **ValueObject:** Sử dụng C# `record` để định nghĩa các đối tượng không có danh tính, so sánh bằng giá trị cấu trúc thuộc tính (ví dụ: `Address`, `Money`).

### 1.2. Quy Tắc Đóng Gói (Encapsulation Rules)
- **Không sử dụng Public Setters:** Tất cả các thuộc tính của Entity phải được khai báo dạng `public get; private set;` hoặc `init;`.
- **Behavior-Driven State:** Thay đổi trạng thái của thực thể bắt buộc phải thông qua các hàm nghiệp vụ tường minh (Domain Methods).
  - *❌ Nghiêm cấm:* `order.Status = OrderStatus.Completed;`
  - *✅ Bắt buộc:* `order.CompleteOrder(dateTimeProvider);`
- **Primary Constructors (.NET 9):** Khuyến khích sử dụng Primary Constructors cho các Value Object và Commands/Queries để mã nguồn ngắn gọn.

---

## 2. Thiết Kế Hướng Khía Cạnh - AOP (Tầng Application & Infrastructure)

Để giữ cho tầng Domain sạch sẽ, các logic bổ trợ (Validation, Logging, Transaction, Security) không được viết trực tiếp trong Domain Handlers mà phải được tách ra thành các Khía cạnh (Aspects) chạy tự động.

### 2.1. Khía Cạnh Kiểm Tra Dữ Liệu (Validation Aspect)
- **Công nghệ:** `MediatR Pipeline Behavior` + `FluentValidation`.
- **Quy tắc:** Mỗi khi có một `ICommand` gửi đến hệ thống, Pipeline Behavior sẽ tự động intercept (chặn lại), quét qua tất cả các lớp `AbstractValidator<T>` tương ứng. Nếu dữ liệu không hợp lệ, hệ thống tự động ném ra `ValidationException` trước khi chạy vào Command Handler.

### 2.2. Khía Cạnh Quản Lý Giao Dịch (Transaction Aspect)
- **Công nghệ:** `MediatR Pipeline Behavior` + `Entity Framework Core, Repository Pattern, Unit of Work`.
- **Quy tắc:** Tất cả các luồng ghi dữ liệu (`Command`) sẽ tự động được bọc trong một Database Transaction thông qua một Behavior chung.
  - Tự động mở `BeginTransactionAsync()` khi Command bắt đầu.
  - Tự động gọi `SaveChangesAsync()` và `CommitAsync()` sau khi Handler chạy thành công không có lỗi.
  - Tự động `RollbackAsync()` nếu có bất kỳ Exception nào xảy ra trong quá trình xử lý.

### 2.3. Khía Cạnh Nhật Ký Kiểm Toán (Auditing Aspect)
- **Công nghệ:** `EF Core SaveChangesInterceptor`.
- **Quy tắc:** Nhà phát triển không viết mã thủ công để cập nhật ngày giờ, người sửa. Interceptor sẽ tự động chặn sự kiện lưu xuống DB, kiểm tra các Entity có triển khai Interface `IAuditableEntity` để điền tự động dữ liệu:
  - Nếu trạng thái là `Added`: Điền `CreatedAt = DateTime.UtcNow`, `CreatedBy = CurrentUser.Id`.
  - Nếu trạng thái là `Modified`: Điền `UpdatedAt = DateTime.UtcNow`, `UpdatedBy = CurrentUser.Id`.

### 2.4. Khía Cạnh Phân Quyền Bảo Mật (Security Aspect)
- **Công nghệ:** `ASP.NET Core Policy-based Authorization` + `Action Filters`.
- **Quy tắc:** Phân quyền kiểm tra ở mức tinh lọc (Granular Permission). Sử dụng Custom Attribute `[HasPermission("Domain.Action")]` đè lên các API Endpoints. Khía cạnh bảo mật sẽ tự động phân tích JWT Access Token để đối chiếu quyền trước khi kích hoạt endpoint nghiệp vụ.

---

## 3. Bản Bản Thiết Kế Mã Nguồn Mẫu (Architectural Blueprints)

### Mã mẫu cho ValueObject và Entity (OOP)
```csharp
// Value Object chuẩn hóa dùng record trong C# 
public abstract record ValueObject;

public record ShippingAddress(string Street, string City, string Province, string Country) : ValueObject;

// Aggregate Root mẫu với tính đóng gói cao
public abstract class AggregateRoot<TId> : Entity<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}