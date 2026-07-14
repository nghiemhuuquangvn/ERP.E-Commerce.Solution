namespace ERP.PO.Application.Authorization;

/// <summary>
/// Định nghĩa các Role và Permissions tương ứng
/// </summary>
public static class RoleDefinitions
{
    // Role Names
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Purchaser = "Purchaser";
    public const string Viewer = "Viewer";

    // Role to Permissions Mapping
    public static readonly Dictionary<string, string[]> RolePermissions = new()
    {
        // SuperAdmin: toàn quyền hệ thống — không có entry này thì
        // ExtractPermissionsFromRoles trả về mảng rỗng cho SuperAdmin,
        // vì Dictionary.TryGetValue không khớp được role "SuperAdmin".
        {
            SuperAdmin, PermissionConstants.AllPermissions
        },

        // Admin: Toàn quyền
        {
            Admin, new[]
            {
                // Purchase Orders
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_Delete,
                PermissionConstants.PurchaseOrder_Approve,
                PermissionConstants.PurchaseOrder_Cancel,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                PermissionConstants.PurchaseOrder_Receive,

                // Suppliers
                PermissionConstants.Supplier_Create,
                PermissionConstants.Supplier_Read,
                PermissionConstants.Supplier_Update,
                PermissionConstants.Supplier_Delete,
                PermissionConstants.Supplier_Deactivate,
                PermissionConstants.Supplier_Rate
            }
        },

        // Manager: Quản lý đơn hàng, duyệt, không thể xóa
        {
            Manager, new[]
            {
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_Approve,
                PermissionConstants.PurchaseOrder_Cancel,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                PermissionConstants.PurchaseOrder_Receive,

                PermissionConstants.Supplier_Create,
                PermissionConstants.Supplier_Read,
                PermissionConstants.Supplier_Update,
                PermissionConstants.Supplier_Rate
            }
        },

        // Purchaser: Tạo và quản lý đơn hàng (không duyệt)
        {
            Purchaser, new[]
            {
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                PermissionConstants.PurchaseOrder_Receive,

                PermissionConstants.Supplier_Read
            }
        },

        // Viewer: Chỉ xem
        {
            Viewer, new[]
            {
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.Supplier_Read
            }
        }
    };

    /// <summary>
    /// Lấy tất cả permissions của một role
    /// </summary>
    public static string[] GetRolePermissions(string role)
    {
        return RolePermissions.TryGetValue(role, out var permissions)
            ? permissions
            : Array.Empty<string>();
    }

    /// <summary>
    /// Kiểm tra user có quyền cụ thể không
    /// </summary>
    public static bool HasPermission(string role, string permission)
    {
        var permissions = GetRolePermissions(role);
        return permissions.Contains(permission);
    }
}
