namespace ERP.Shared.Authorization;

/// <summary>
/// Định nghĩa roles và mapping với permissions
/// Dùng chung cho tất cả 14 services
/// </summary>
public static class RoleDefinitions
{
    public static readonly Dictionary<string, string[]> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        // SuperAdmin - Toàn quyền hệ thống
        {
            "SuperAdmin",
            PermissionConstants.GetAllPermissions()
        },

        // Admin - Toàn quyền nghiệp vụ
        {
            "Admin",
            [
                // Purchase Order
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_Delete,
                PermissionConstants.PurchaseOrder_Approve,
                PermissionConstants.PurchaseOrder_Cancel,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                PermissionConstants.PurchaseOrder_Receive,
                // Supplier
                PermissionConstants.Supplier_Create,
                PermissionConstants.Supplier_Read,
                PermissionConstants.Supplier_Update,
                PermissionConstants.Supplier_Delete,
                PermissionConstants.Supplier_Deactivate,
                PermissionConstants.Supplier_Rate,
                // Sales Order
                PermissionConstants.SalesOrder_Create,
                PermissionConstants.SalesOrder_Read,
                PermissionConstants.SalesOrder_Update,
                PermissionConstants.SalesOrder_Approve,
                PermissionConstants.SalesOrder_Cancel,
                // Accounting
                PermissionConstants.Accounting_InvoicesManage,
                PermissionConstants.Accounting_BillsManage,
                PermissionConstants.Accounting_JournalsManage,
                PermissionConstants.Accounting_PeriodsManage,
                PermissionConstants.Accounting_BankManage,
                PermissionConstants.Accounting_TaxManage,
                PermissionConstants.Accounting_ReportsView,
                // Shipping
                PermissionConstants.Shipping_Create,
                PermissionConstants.Shipping_Update,
                // Payment
                PermissionConstants.Payments_Create,
                PermissionConstants.Payments_Refund,
                PermissionConstants.Payments_SettlementsManage,
                // POS
                PermissionConstants.POS_Transactions,
                PermissionConstants.POS_TerminalsManage,
                PermissionConstants.POS_ReportsView,
                // Promotion
                PermissionConstants.Promotions_Manage,
                // Notifications
                PermissionConstants.Notifications_Send,
                PermissionConstants.Notifications_TemplatesManage,
                PermissionConstants.Notifications_WebhooksManage,
            ]
        },

        // Manager - Quản lý đơn hàng và vận hành
        {
            "Manager",
            [
                // Purchase Order
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_Approve,
                PermissionConstants.PurchaseOrder_Cancel,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                // Supplier
                PermissionConstants.Supplier_Create,
                PermissionConstants.Supplier_Read,
                PermissionConstants.Supplier_Update,
                // Sales Order
                PermissionConstants.SalesOrder_Create,
                PermissionConstants.SalesOrder_Read,
                PermissionConstants.SalesOrder_Update,
                PermissionConstants.SalesOrder_Approve,
                PermissionConstants.SalesOrder_Cancel,
                // Accounting (read reports only)
                PermissionConstants.Accounting_ReportsView,
                // Shipping
                PermissionConstants.Shipping_Create,
                PermissionConstants.Shipping_Update,
                // POS
                PermissionConstants.POS_Transactions,
                PermissionConstants.POS_ReportsView,
                // Promotion
                PermissionConstants.Promotions_Manage,
            ]
        },

        // Purchaser - Tạo và quản lý PO
        {
            "Purchaser",
            [
                PermissionConstants.PurchaseOrder_Create,
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.PurchaseOrder_Update,
                PermissionConstants.PurchaseOrder_AddItem,
                PermissionConstants.PurchaseOrder_RemoveItem,
                PermissionConstants.Supplier_Read,
            ]
        },

        // Viewer - Chỉ xem
        {
            "Viewer",
            [
                PermissionConstants.PurchaseOrder_Read,
                PermissionConstants.Supplier_Read,
                PermissionConstants.SalesOrder_Read,
                PermissionConstants.Accounting_ReportsView,
                PermissionConstants.POS_ReportsView,
            ]
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
