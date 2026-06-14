namespace ERP.Shared.Authorization;

/// <summary>
/// Định nghĩa 4 roles và mapping với permissions
/// Dùng chung cho tất cả 14 services
/// </summary>
public static class RoleDefinitions
{
    public static readonly Dictionary<string, string[]> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Admin - Toàn quyền
        {
            "Admin",
            new[]
            {
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_Delete,
                PermissionConstants.PurchaseOrder_Approve,
                PermissionConstants.PurchaseOrder_Cancel,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                PermissionConstants.Supplier_Create,
                PermissionConstants.Supplier_Read,
                PermissionConstants.Supplier_Update,
                PermissionConstants.Supplier_Delete,
                PermissionConstants.Supplier_Deactivate
            }
        },

        // Manager - Quản lý đơn hàng
        {
            "Manager",
            new[]
            {
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_Approve,
                PermissionConstants.PurchaseOrder_Cancel,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                PermissionConstants.Supplier_Create,
                PermissionConstants.Supplier_Read,
                PermissionConstants.Supplier_Update
            }
        },

        // Purchaser - Tạo và quản lý PO
        {
            "Purchaser",
            new[]
            {
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                PermissionConstants.Supplier_Read
            }
        },

        // Viewer - Chỉ xem
        {
            "Viewer",
            new[]
            {
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.Supplier_Read
            }
        }
    };

    /// <summary>
    /// Lấy permissions cho một role
    /// </summary>
    public static string[] GetPermissionsForRole(string role)
    {
        return RolePermissions.TryGetValue(role, out var permissions)
            ? permissions
            : Array.Empty<string>();
    }

    /// <summary>
    /// Lấy permissions cho nhiều roles (union)
    /// </summary>
    public static string[] GetPermissionsForRoles(params string[] roles)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            var rolePermissions = GetPermissionsForRole(role);
            foreach (var permission in rolePermissions)
            {
                permissions.Add(permission);
            }
        }

        return permissions.ToArray();
    }

    /// <summary>
    /// Kiểm tra user có quyền cụ thể không
    /// </summary>
    public static bool HasPermission(string[] roles, string permission)
    {
        var permissions = GetPermissionsForRoles(roles);
        return permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}
