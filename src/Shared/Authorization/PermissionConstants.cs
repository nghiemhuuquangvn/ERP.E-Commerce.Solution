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
    // NOTE: AccountingService's controllers check both a "...:view" (GET) and
    // "...:manage" (POST/PUT/status actions) variant per entity — the ":view"
    // half of this pairing was missing entirely from this vocabulary (the one
    // ERP.Shared.Authorization.RoleDefinitions/business-service controllers
    // actually use, distinct from IdentityService's own DB-seeded AuthDB
    // permissions), so every GET endpoint across all 7 controllers was
    // unconditionally Forbidden for every role, including SuperAdmin, until
    // these were added.
    public const string Accounting_AccountsView = "accounting:accounts:view";
    public const string Accounting_AccountsManage = "accounting:accounts:manage";
    public const string Accounting_InvoicesView = "accounting:invoices:view";
    public const string Accounting_InvoicesManage = "accounting:invoices:manage";
    public const string Accounting_BillsView = "accounting:bills:view";
    public const string Accounting_BillsManage = "accounting:bills:manage";
    public const string Accounting_JournalsView = "accounting:journals:view";
    public const string Accounting_JournalsManage = "accounting:journals:manage";
    public const string Accounting_PeriodsView = "accounting:periods:view";
    public const string Accounting_PeriodsManage = "accounting:periods:manage";
    public const string Accounting_BankView = "accounting:bank:view";
    public const string Accounting_BankManage = "accounting:bank:manage";
    public const string Accounting_TaxView = "accounting:tax:view";
    public const string Accounting_TaxManage = "accounting:tax:manage";
    public const string Accounting_ReportsView = "accounting:reports:view";

    // Shipping Permissions
    public const string Shipping_Create = "shipping:create";
    public const string Shipping_Update = "shipping:update";

    // Warehouse / Inventory Permissions
    public const string Inventory_View = "inventory:view";
    public const string Inventory_Manage = "inventory:manage";

    // Payment Permissions
    public const string Payments_View = "payments:view";
    public const string Payments_Create = "payments:create";
    public const string Payments_Authorize = "payments:authorize";
    public const string Payments_Capture = "payments:capture";
    public const string Payments_Cancel = "payments:cancel";
    public const string Payments_Refund = "payments:refund";
    public const string Payments_SettlementsView = "payments:settlements:view";
    public const string Payments_SettlementsManage = "payments:settlements:manage";

    // POS Permissions
    public const string POS_Transactions = "pos:transactions";
    public const string POS_TerminalsManage = "pos:terminals:manage";
    public const string POS_ReportsView = "pos:reports:view";
    public const string POS_ShiftsView = "pos:shifts:view";
    public const string POS_ShiftsManage = "pos:shifts:manage";

    // Promotion Permissions
    public const string Promotions_Manage = "promotions:manage";

    // Notification Permissions
    public const string Notifications_Send = "notifications:send";
    public const string Notifications_TemplatesManage = "notifications:templates:manage";
    public const string Notifications_WebhooksManage = "notifications:webhooks:manage";

    // Reporting Permissions
    public const string Reports_SalesView = "reports:sales:view";
    public const string Reports_InventoryView = "reports:inventory:view";
    public const string Reports_PaymentsView = "reports:payments:view";
    public const string Reports_PosView = "reports:pos:view";
    public const string Reports_HrView = "reports:hr:view";

    // Logging Permissions
    public const string Logs_View = "logs:view";

    // Per-service Settings categories: {system|tenant} x {view|manage}, mirroring
    // IdentityService's own Settings tiering (system = SuperAdmin-only master
    // switch, tenant = per-tenant, requires the system switch to also be on).
    public const string Settings_CartCheckout_SystemView = "settings:cart-checkout:system:view";
    public const string Settings_CartCheckout_SystemManage = "settings:cart-checkout:system:manage";
    public const string Settings_CartCheckout_TenantView = "settings:cart-checkout:tenant:view";
    public const string Settings_CartCheckout_TenantManage = "settings:cart-checkout:tenant:manage";

    public const string Settings_Currency_SystemView = "settings:currency:system:view";
    public const string Settings_Currency_SystemManage = "settings:currency:system:manage";
    public const string Settings_Currency_TenantView = "settings:currency:tenant:view";
    public const string Settings_Currency_TenantManage = "settings:currency:tenant:manage";

    public const string Settings_Email_SystemView = "settings:email:system:view";
    public const string Settings_Email_SystemManage = "settings:email:system:manage";
    public const string Settings_Email_TenantView = "settings:email:tenant:view";
    public const string Settings_Email_TenantManage = "settings:email:tenant:manage";

    public const string Settings_Inventory_SystemView = "settings:inventory:system:view";
    public const string Settings_Inventory_SystemManage = "settings:inventory:system:manage";
    public const string Settings_Inventory_TenantView = "settings:inventory:tenant:view";
    public const string Settings_Inventory_TenantManage = "settings:inventory:tenant:manage";

    public const string Settings_Notification_SystemView = "settings:notification:system:view";
    public const string Settings_Notification_SystemManage = "settings:notification:system:manage";
    public const string Settings_Notification_TenantView = "settings:notification:tenant:view";
    public const string Settings_Notification_TenantManage = "settings:notification:tenant:manage";

    public const string Settings_Order_SystemView = "settings:order:system:view";
    public const string Settings_Order_SystemManage = "settings:order:system:manage";
    public const string Settings_Order_TenantView = "settings:order:tenant:view";
    public const string Settings_Order_TenantManage = "settings:order:tenant:manage";

    public const string Settings_Payment_SystemView = "settings:payment:system:view";
    public const string Settings_Payment_SystemManage = "settings:payment:system:manage";
    public const string Settings_Payment_TenantView = "settings:payment:tenant:view";
    public const string Settings_Payment_TenantManage = "settings:payment:tenant:manage";

    public const string Settings_Shipping_SystemView = "settings:shipping:system:view";
    public const string Settings_Shipping_SystemManage = "settings:shipping:system:manage";
    public const string Settings_Shipping_TenantView = "settings:shipping:tenant:view";
    public const string Settings_Shipping_TenantManage = "settings:shipping:tenant:manage";

    public const string Settings_Sms_SystemView = "settings:sms:system:view";
    public const string Settings_Sms_SystemManage = "settings:sms:system:manage";
    public const string Settings_Sms_TenantView = "settings:sms:tenant:view";
    public const string Settings_Sms_TenantManage = "settings:sms:tenant:manage";

    public const string Settings_StoreProduct_SystemView = "settings:store-product:system:view";
    public const string Settings_StoreProduct_SystemManage = "settings:store-product:system:manage";
    public const string Settings_StoreProduct_TenantView = "settings:store-product:tenant:view";
    public const string Settings_StoreProduct_TenantManage = "settings:store-product:tenant:manage";

    public const string Settings_Tax_SystemView = "settings:tax:system:view";
    public const string Settings_Tax_SystemManage = "settings:tax:system:manage";
    public const string Settings_Tax_TenantView = "settings:tax:tenant:view";
    public const string Settings_Tax_TenantManage = "settings:tax:tenant:manage";

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
        Accounting_AccountsView, Accounting_AccountsManage,
        Accounting_InvoicesView, Accounting_InvoicesManage,
        Accounting_BillsView, Accounting_BillsManage,
        Accounting_JournalsView, Accounting_JournalsManage,
        Accounting_PeriodsView, Accounting_PeriodsManage,
        Accounting_BankView, Accounting_BankManage,
        Accounting_TaxView, Accounting_TaxManage,
        Accounting_ReportsView,
        Shipping_Create, Shipping_Update,
        Inventory_View, Inventory_Manage,
        Payments_View, Payments_Create, Payments_Authorize, Payments_Capture, Payments_Cancel, Payments_Refund,
        Payments_SettlementsView, Payments_SettlementsManage,
        POS_Transactions, POS_TerminalsManage, POS_ReportsView, POS_ShiftsView, POS_ShiftsManage,
        Promotions_Manage,
        Notifications_Send, Notifications_TemplatesManage, Notifications_WebhooksManage,
        Reports_SalesView, Reports_InventoryView, Reports_PaymentsView, Reports_PosView, Reports_HrView,
        Logs_View,
        Settings_CartCheckout_SystemView, Settings_CartCheckout_SystemManage,
        Settings_CartCheckout_TenantView, Settings_CartCheckout_TenantManage,
        Settings_Currency_SystemView, Settings_Currency_SystemManage,
        Settings_Currency_TenantView, Settings_Currency_TenantManage,
        Settings_Email_SystemView, Settings_Email_SystemManage,
        Settings_Email_TenantView, Settings_Email_TenantManage,
        Settings_Inventory_SystemView, Settings_Inventory_SystemManage,
        Settings_Inventory_TenantView, Settings_Inventory_TenantManage,
        Settings_Notification_SystemView, Settings_Notification_SystemManage,
        Settings_Notification_TenantView, Settings_Notification_TenantManage,
        Settings_Order_SystemView, Settings_Order_SystemManage,
        Settings_Order_TenantView, Settings_Order_TenantManage,
        Settings_Payment_SystemView, Settings_Payment_SystemManage,
        Settings_Payment_TenantView, Settings_Payment_TenantManage,
        Settings_Shipping_SystemView, Settings_Shipping_SystemManage,
        Settings_Shipping_TenantView, Settings_Shipping_TenantManage,
        Settings_Sms_SystemView, Settings_Sms_SystemManage,
        Settings_Sms_TenantView, Settings_Sms_TenantManage,
        Settings_StoreProduct_SystemView, Settings_StoreProduct_SystemManage,
        Settings_StoreProduct_TenantView, Settings_StoreProduct_TenantManage,
        Settings_Tax_SystemView, Settings_Tax_SystemManage,
        Settings_Tax_TenantView, Settings_Tax_TenantManage,
    ];

    /// <summary>
    /// Tenant-tier-only subset of the Settings permissions (view+manage, no system tier) —
    /// used by the Admin role, which manages its own tenant's settings but never the
    /// SuperAdmin-only system-wide switches.
    /// </summary>
    public static string[] GetTenantSettingsPermissions() =>
    [
        Settings_CartCheckout_TenantView, Settings_CartCheckout_TenantManage,
        Settings_Currency_TenantView, Settings_Currency_TenantManage,
        Settings_Email_TenantView, Settings_Email_TenantManage,
        Settings_Inventory_TenantView, Settings_Inventory_TenantManage,
        Settings_Notification_TenantView, Settings_Notification_TenantManage,
        Settings_Order_TenantView, Settings_Order_TenantManage,
        Settings_Payment_TenantView, Settings_Payment_TenantManage,
        Settings_Shipping_TenantView, Settings_Shipping_TenantManage,
        Settings_Sms_TenantView, Settings_Sms_TenantManage,
        Settings_StoreProduct_TenantView, Settings_StoreProduct_TenantManage,
        Settings_Tax_TenantView, Settings_Tax_TenantManage,
    ];
}
