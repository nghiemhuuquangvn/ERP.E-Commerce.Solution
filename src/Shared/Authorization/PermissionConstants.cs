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

    // Sales Order Permissions
    public const string SalesOrder_Create = "sales_order:create";
    public const string SalesOrder_Read = "sales_order:read";
    public const string SalesOrder_Update = "sales_order:update";
    public const string SalesOrder_Approve = "sales_order:approve";
    public const string SalesOrder_Cancel = "sales_order:cancel";

    // Accounting Permissions
    public const string Accounting_InvoicesManage = "accounting:invoices:manage";
    public const string Accounting_BillsManage = "accounting:bills:manage";
    public const string Accounting_JournalsManage = "accounting:journals:manage";
    public const string Accounting_PeriodsManage = "accounting:periods:manage";
    public const string Accounting_BankManage = "accounting:bank:manage";
    public const string Accounting_TaxManage = "accounting:tax:manage";
    public const string Accounting_ReportsView = "accounting:reports:view";

    // Shipping Permissions
    public const string Shipping_Create = "shipping:create";
    public const string Shipping_Update = "shipping:update";

    // Payment Permissions
    public const string Payments_Create = "payments:create";
    public const string Payments_Refund = "payments:refund";
    public const string Payments_SettlementsManage = "payments:settlements:manage";

    // POS Permissions
    public const string POS_Transactions = "pos:transactions";
    public const string POS_TerminalsManage = "pos:terminals:manage";
    public const string POS_ReportsView = "pos:reports:view";

    // Promotion Permissions
    public const string Promotions_Manage = "promotions:manage";

    // Notification Permissions
    public const string Notifications_Send = "notifications:send";
    public const string Notifications_TemplatesManage = "notifications:templates:manage";
    public const string Notifications_WebhooksManage = "notifications:webhooks:manage";

    /// <summary>
    /// Lấy tất cả permissions
    /// </summary>
    public static string[] GetAllPermissions() =>
    [
        PurchaseOrder_Create, PurchaseOrder_Read, PurchaseOrder_Update, PurchaseOrder_Delete,
        PurchaseOrder_Approve, PurchaseOrder_Cancel, PurchaseOrder_AddItem, PurchaseOrder_RemoveItem,
        PurchaseOrder_Receive,
        Supplier_Create, Supplier_Read, Supplier_Update, Supplier_Delete, Supplier_Deactivate, Supplier_Rate,
        SalesOrder_Create, SalesOrder_Read, SalesOrder_Update, SalesOrder_Approve, SalesOrder_Cancel,
        Accounting_InvoicesManage, Accounting_BillsManage, Accounting_JournalsManage, Accounting_PeriodsManage,
        Accounting_BankManage, Accounting_TaxManage, Accounting_ReportsView,
        Shipping_Create, Shipping_Update,
        Payments_Create, Payments_Refund, Payments_SettlementsManage,
        POS_Transactions, POS_TerminalsManage, POS_ReportsView,
        Promotions_Manage,
        Notifications_Send, Notifications_TemplatesManage, Notifications_WebhooksManage,
    ];
}
