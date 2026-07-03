namespace ERP.Shared.Authorization;

/// <summary>
/// Định nghĩa tất cả permissions trong hệ thống
/// Dùng chung cho tất cả 14 services
/// </summary>
public static class PermissionConstants
{
    // Purchase Order Permissions
    public const string PurchaseOrder_Create = "purchase_order:create";
    public const string PurchaseOrder_Read = "purchase_order:read";
    public const string PurchaseOrder_Update = "purchase_order:update";
    public const string PurchaseOrder_Delete = "purchase_order:delete";
    public const string PurchaseOrder_Approve = "purchase_order:approve";
    public const string PurchaseOrder_Cancel = "purchase_order:cancel";
    public const string PurchaseOrder_AddItem = "purchase_order:add_item";
    public const string PurchaseOrder_RemoveItem = "purchase_order:remove_item";
    public const string PurchaseOrder_Receive = "purchase_order:receive";

    // Supplier Permissions
    public const string Supplier_Create = "supplier:create";
    public const string Supplier_Read = "supplier:read";
    public const string Supplier_Update = "supplier:update";
    public const string Supplier_Delete = "supplier:delete";
    public const string Supplier_Deactivate = "supplier:deactivate";
    public const string Supplier_Rate = "supplier:rate";

    /// <summary>
    /// Lấy tất cả permissions
    /// </summary>
    public static string[] GetAllPermissions()
    {
        return new[]
        {
            PurchaseOrder_Create, PurchaseOrder_Read, PurchaseOrder_Update, PurchaseOrder_Delete,
            PurchaseOrder_Approve, PurchaseOrder_Cancel, PurchaseOrder_AddItem, PurchaseOrder_RemoveItem,
            PurchaseOrder_Receive,
            Supplier_Create, Supplier_Read, Supplier_Update, Supplier_Delete, Supplier_Deactivate,
            Supplier_Rate
        };
    }
}
