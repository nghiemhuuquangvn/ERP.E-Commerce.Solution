# Hệ Thống Quyền Hạn (Permission System) - Hướng Dẫn Chi Tiết

## 📋 Mục Đích Hệ Thống

Hệ thống quyền hạn xác định:
1. **User nào** - Xác thực user qua JWT
2. **Tenant nào** - Xác định user thuộc organization/tenant nào
3. **Permissions gì** - User có quyền thực hiện hành động nào

---

## 🏗️ Kiến Trúc Hệ Thống

```
┌─────────────────────────────────────────────────────┐
│         HTTP Request with JWT Token                 │
└────────────────────┬────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────┐
│     AuthenticationMiddleware                        │
│  - Lấy token từ Authorization header                │
│  - Parse JWT claims (sub, tenant_id, roles)        │
└────────────────────┬────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────┐
│     TenantMiddleware                                │
│  - Lưu tenant_id vào context                        │
│  - Đảm bảo tenant isolation                         │
└────────────────────┬────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────┐
│     UserContextService                              │
│  - Lấy UserContext từ HttpContext                   │
│  - Tính toán permissions từ roles                   │
│  - Cung cấp helper methods                          │
└────────────────────┬────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────┐
│     [AuthorizePermission] Attribute                 │
│  - Kiểm tra user có quyền cụ thể                    │
│  - Return 401 Unauthorized hoặc 403 Forbidden       │
└────────────────────┬────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────┐
│     Controller Action                               │
│  - Thực thi business logic                          │
│  - User info có sẵn qua _userContextService         │
└─────────────────────────────────────────────────────┘
```

---

## 🔑 Các Thành Phần Chính

### 1. **PermissionConstants.cs** - Danh sách tất cả quyền

```csharp
public static class PermissionConstants
{
    // Purchase Order Permissions
    public const string PurchaseOrder_Create = "purchase_order:create";
    public const string PurchaseOrder_Read = "purchase_order:read";
    public const string PurchaseOrder_Approve = "purchase_order:approve";
    
    // Supplier Permissions
    public const string Supplier_Create = "supplier:create";
    public const string Supplier_Read = "supplier:read";
    
    // ... tất cả các permissions
}
```

### 2. **RoleDefinitions.cs** - Mapping Role → Permissions

```csharp
public static class RoleDefinitions
{
    public static readonly Dictionary<string, string[]> RolePermissions = new()
    {
        { "Admin", new[] { /* tất cả permissions */ } },
        { "Manager", new[] { /* permissions quản lý */ } },
        { "Purchaser", new[] { /* permissions tạo PO */ } },
        { "Viewer", new[] { /* chỉ read */ } }
    };
}
```

### 3. **UserContext** - Thông tin User hiện tại

```csharp
public class UserContext
{
    public string UserId { get; set; }           // ID của user
    public string TenantId { get; set; }         // ID của tenant
    public string[] Roles { get; set; }          // Danh sách roles
    public string[] Permissions { get; set; }    // Danh sách permissions
    
    public bool HasPermission(string permission) => /* check */;
    public bool HasRole(string role) => /* check */;
}
```

### 4. **IUserContextService** - Lấy thông tin user

```csharp
public interface IUserContextService
{
    UserContext GetCurrentUser();
    bool HasPermission(string permission);
    bool HasRole(string role);
    string GetUserId();
    string GetTenantId();
}
```

### 5. **[AuthorizePermission]** - Custom Attribute

```csharp
[AuthorizePermission(PermissionConstants.PurchaseOrder_Create)]
public async Task<IActionResult> CreatePurchaseOrder(...)
{
    // Chỉ user có permission "purchase_order:create" mới tới đây
}
```

---

## 👥 Các Roles và Permissions

### Admin
✅ Toàn quyền - tất cả permissions

### Manager
✅ Quản lý đơn hàng
- Tạo, đọc, cập nhật PO
- Duyệt/hủy PO
- Tạo, đọc, cập nhật supplier
❌ Không thể: xóa PO, deactivate supplier

### Purchaser
✅ Tạo và quản lý đơn hàng
- Tạo, đọc, cập nhật PO
- Thêm/xóa items
❌ Không thể: duyệt PO, quản lý supplier

### Viewer
✅ Chỉ xem
- Đọc PO
- Đọc Supplier

---

## 🔐 JWT Token Structure

```json
{
  "sub": "user@example.com",                    // User ID
  "preferred_username": "user@example.com",    // User name
  "tenant_id": "tenant-123",                   // Tenant ID
  "tenant_name": "Company ABC",                // Tenant name
  "role": ["Admin", "Manager"],                // Roles
  "exp": 1234567890,                           // Expiry
  "iat": 1234567800                            // Issued at
}
```

---

## 📝 Cách Sử Dụng

### 1. Đặt Authorization cho Endpoint

```csharp
[ApiController]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IUserContextService _userContextService;

    // Tạo PO - yêu cầu purchase_order:create
    [HttpPost]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Create)]
    public async Task<IActionResult> CreatePurchaseOrder(CreatePurchaseOrderDto request)
    {
        var user = _userContextService.GetCurrentUser();
        // user.UserId, user.TenantId, user.Permissions...
    }

    // Duyệt PO - yêu cầu purchase_order:approve
    [HttpPost("{id}/approve")]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Approve)]
    public async Task<IActionResult> ApprovePurchaseOrder(Guid id)
    {
        var user = _userContextService.GetCurrentUser();
        // Chỉ Manager hoặc Admin tới đây
    }
}
```

### 2. Kiểm tra Permission trong Handler/Service

```csharp
public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand>
{
    private readonly IUserContextService _userContextService;

    public async Task<PurchaseOrderDto> Handle(...)
    {
        var user = _userContextService.GetCurrentUser();
        
        // Kiểm tra user có quyền
        if (!user.HasPermission(PermissionConstants.PurchaseOrder_Create))
            throw new UnauthorizedAccessException();

        // Kiểm tra tenant isolation
        var tenantId = user.TenantId;
        // Đảm bảo data chỉ thuộc tenant này
    }
}
```

### 3. Multiple Permissions

```csharp
// Có TẤT CẢ permissions
[AuthorizePermission(
    PermissionConstants.PurchaseOrder_Read,
    PermissionConstants.PurchaseOrder_Update)]
public async Task<IActionResult> UpdatePO(...) { }

// Có ÍT NHẤT một permission
[AuthorizeAnyPermission(
    PermissionConstants.PurchaseOrder_Create,
    PermissionConstants.PurchaseOrder_Read)]
public async Task<IActionResult> GetOrCreatePO(...) { }

// Check by Role
[AuthorizeRole("Admin", "Manager")]
public async Task<IActionResult> AdminAction(...) { }
```

---

## 🔒 Tenant Isolation

Middleware tự động đảm bảo tenant isolation:

```csharp
// TenantMiddleware tự động lưu tenant_id
public async Task InvokeAsync(HttpContext context, IUserContextService userContextService)
{
    var user = userContextService.GetCurrentUser();
    context.Items["TenantId"] = user.TenantId;  // ← Tự động lưu
    
    await _next(context);
}

// Trong handler, kiểm tra tenant
var tenantId = context.HttpContext.Items["TenantId"]?.ToString();
// Chỉ truy cập data của tenant này
```

---

## 📊 Bảng Permissions & Endpoints

| Endpoint | Permission | Roles | Mô tả |
|----------|-----------|-------|-------|
| POST /api/purchase-orders | purchase_order:create | Admin, Manager, Purchaser | Tạo PO |
| GET /api/purchase-orders | purchase_order:read | Tất cả | Danh sách PO |
| GET /api/purchase-orders/{id} | purchase_order:read | Tất cả | Chi tiết PO |
| POST /api/{id}/submit | purchase_order:update | Admin, Manager, Purchaser | Submit PO |
| POST /api/{id}/approve | purchase_order:approve | Admin, Manager | Duyệt PO |
| POST /api/{id}/cancel | purchase_order:cancel | Admin, Manager | Hủy PO |
| POST /api/suppliers | supplier:create | Admin, Manager | Tạo Supplier |
| GET /api/suppliers | supplier:read | Tất cả | Danh sách Supplier |
| PUT /api/suppliers/{id} | supplier:update | Admin, Manager | Cập nhật Supplier |
| POST /api/suppliers/{id}/deactivate | supplier:deactivate | Admin | Tắt Supplier |

---

## 🛠️ Thêm Permission Mới

### Bước 1: Thêm vào PermissionConstants
```csharp
public const string NewFeature_Action = "new_feature:action";
```

### Bước 2: Cập nhật RoleDefinitions
```csharp
{
    "Admin", new[] { ..., PermissionConstants.NewFeature_Action }
},
```

### Bước 3: Thêm [AuthorizePermission] vào Endpoint
```csharp
[AuthorizePermission(PermissionConstants.NewFeature_Action)]
public async Task<IActionResult> NewFeatureAction() { }
```

---

## 🔧 Cấu Hình trong appsettings.json

```json
{
  "Authorization": {
    "EnableMultiTenant": true,
    "RequireAuthentication": true
  }
}
```

---

## 📈 Logging & Audit

Middleware tự động log user context:

```
INFO: Request from user john@example.com in tenant tenant-123
WARN: User john@example.com denied access to purchase_order:delete (missing permission)
```

---

## ✅ Kiểm tra Danh Sách

Hệ thống bao gồm:

✅ **PermissionConstants.cs** - 13 permissions định nghĩa  
✅ **RoleDefinitions.cs** - 4 roles với mapping permissions  
✅ **UserContext.cs** - Model thông tin user  
✅ **IUserContextService** - Interface service  
✅ **UserContextService** - Triển khai service  
✅ **[AuthorizePermission]** - Custom attribute  
✅ **[AuthorizeAnyPermission]** - Multiple permissions  
✅ **[AuthorizeRole]** - Role-based check  
✅ **AuthenticationMiddleware** - Parse JWT  
✅ **TenantMiddleware** - Tenant isolation  
✅ **Controllers Updated** - Với [AuthorizePermission]  

---

## 🚀 Sử Dụng

### Gửi Request với JWT

```bash
curl -X POST http://localhost:5003/api/purchase-orders \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "poNumber": "PO-2024-001",
    "supplierId": "550e8400-e29b-41d4-a716-446655440000"
  }'
```

### Response

**401 Unauthorized** - Không có token  
**403 Forbidden** - Token hợp lệ nhưng user không có permission  
**200 OK** - User có permission, thực thi thành công  

---

## 📚 Files Tạo Ra

```
src/ERP.PO.Application/Authorization/
├── PermissionConstants.cs         # Định nghĩa permissions
├── RoleDefinitions.cs             # Mapping role → permissions
├── UserContext.cs                 # Model user info
└── IUserContextService.cs          # Interface service

src/ERP.PO.Api/Authorization/
├── UserContextService.cs          # Triển khai service
├── AuthorizePermissionAttribute.cs # Custom attributes
├── AuthenticationMiddleware.cs     # Parse JWT
└── TenantMiddleware.cs             # Tenant isolation
```

---

## 🎯 Tóm Tắt

✅ **Xác định User** - Từ JWT (sub claim)  
✅ **Xác định Tenant** - Từ JWT (tenant_id claim)  
✅ **Xác định Permission** - Từ Role mapping  
✅ **Enforce Permission** - Via [AuthorizePermission] attribute  
✅ **Tenant Isolation** - Automatic via middleware  
✅ **Audit Trail** - Automatic logging  

**Hệ thống quyền hạn hoàn chỉnh đã sẵn sàng sử dụng!**
