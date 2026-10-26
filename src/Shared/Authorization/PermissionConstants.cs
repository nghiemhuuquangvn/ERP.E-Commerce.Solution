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
    public const string PurchaseOrder_FinanceApprove = "purchase_order:finance_approve";
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
    public const string Shipping_View = "shipping:view";
    public const string Shipping_CarriersManage = "shipping:carriers:manage";

    // Warehouse / Inventory Permissions
    public const string Inventory_View = "inventory:view";
    public const string Inventory_Manage = "inventory:manage";
    // Granular permissions added alongside the Stock Adjustment / Stocktake approval-gate
    // retrofit — Inventory_Manage remains for the unchanged endpoints (Locations CRUD,
    // movement history, lots/serials read, receive/reserve/transfer) to limit blast radius.
    public const string Inventory_Adjustment_Create = "inventory:adjustment:create";
    public const string Inventory_Adjustment_Approve = "inventory:adjustment:approve";
    public const string Inventory_Stocktake_Approve = "inventory:stocktake:approve";
    public const string Inventory_Barcode_View = "inventory:barcode:view";
    public const string Inventory_Warehouse_Manage = "inventory:warehouse:manage";
    // Module 8 reconciliation — closing a period-end valuation snapshot is a Finance-grade
    // action (immutable once closed), gated separately from the coarse Inventory_Manage.
    public const string Inventory_Valuation_Close = "inventory:valuation:close";

    // PurchaseOrderService — StockAlertEvent-driven purchase suggestions (Module 8
    // reconciliation). Follows this service's own purchase_order:*/supplier:* snake_case
    // family, not the inventory:* colon-nested style above.
    public const string PurchaseRequest_View = "purchase_request:view";
    public const string PurchaseRequest_Manage = "purchase_request:manage";

    // PurchaseOrderService — RFQ, supplier invoices with three-way match, returns to vendor (ROADMAP P1-8).
    public const string Rfq_Read = "rfq:read";
    public const string Rfq_Manage = "rfq:manage";
    public const string SupplierInvoice_Read = "supplier_invoice:read";
    public const string SupplierInvoice_Manage = "supplier_invoice:manage";
    public const string SupplierInvoice_Approve = "supplier_invoice:approve";
    /// <summary>Approve an invoice the three-way match put on hold (quantity or price outside the tolerance).</summary>
    public const string SupplierInvoice_ApproveVariance = "supplier_invoice:approve_variance";
    public const string VendorReturn_Read = "vendor_return:read";
    public const string VendorReturn_Manage = "vendor_return:manage";
    public const string PurchasingSettings_Manage = "purchasing_settings:manage";

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
    public const string Promotions_View = "promotions:view";

    // Notification Permissions
    public const string Notifications_Send = "notifications:send";
    public const string Notifications_TemplatesManage = "notifications:templates:manage";
    public const string Notifications_WebhooksManage = "notifications:webhooks:manage";
    // Covers "view a notification/preference" — none of the three constants above (all
    // write-oriented) fit read-only actions like GetById/GetHistory/Preferences.Get.
    // NOTE: granting this alone is not sufficient for IDOR-prone endpoints scoped to a
    // specific recipient/user — callers must also pass an ownership check (see
    // NotificationsController/PreferencesController) unless they hold an elevated,
    // admin-scope permission (Notifications_Send) allowing cross-recipient access.
    public const string Notifications_View = "notifications:view";

    // Reporting Permissions
    public const string Reports_SalesView = "reports:sales:view";
    public const string Reports_InventoryView = "reports:inventory:view";
    public const string Reports_PaymentsView = "reports:payments:view";
    public const string Reports_PosView = "reports:pos:view";
    public const string Reports_HrView = "reports:hr:view";

    // Logging Permissions
    public const string Logs_View = "logs:view";

    // CRM Permissions
    // NOTE: same class of bug as Accounting's ":view" gap — CRMService's controllers
    // already checked every one of these strings via [AuthorizePermission("...")], but
    // none were defined here, so every CRM endpoint was Forbidden for every role except
    // SuperAdmin (which bypasses via AuthorizePermissionAttribute's HasRole("SuperAdmin")
    // check) until these were added.
    public const string Crm_ContactsView = "crm:contacts:view";
    public const string Crm_ContactsManage = "crm:contacts:manage";
    public const string Crm_LeadsView = "crm:leads:view";
    public const string Crm_LeadsManage = "crm:leads:manage";
    public const string Crm_OpportunitiesView = "crm:opportunities:view";
    public const string Crm_OpportunitiesManage = "crm:opportunities:manage";
    public const string Crm_CasesView = "crm:cases:view";
    public const string Crm_CasesManage = "crm:cases:manage";
    public const string Crm_ActivitiesView = "crm:activities:view";
    public const string Crm_ActivitiesManage = "crm:activities:manage";

    // HRM Permissions (same gap/fix as CRM above)
    public const string Hrm_EmployeesView = "hrm:employees:view";
    public const string Hrm_AttendanceView = "hrm:attendance:view";
    public const string Hrm_AttendanceManage = "hrm:attendance:manage";
    public const string Hrm_LeaveManage = "hrm:leave:manage";
    public const string Hrm_ShiftsView = "hrm:shifts:view";
    public const string Hrm_ShiftsManage = "hrm:shifts:manage";
    public const string Hrm_PayrollManage = "hrm:payroll:manage";

    // SharedMasterDataService Permissions (same gap/fix as CRM above)
    public const string MasterData_DepartmentsView = "masterdata:departments:view";
    public const string MasterData_DepartmentsManage = "masterdata:departments:manage";
    public const string MasterData_PositionsView = "masterdata:positions:view";
    public const string MasterData_PositionsManage = "masterdata:positions:manage";
    public const string MasterData_EmployeesView = "masterdata:employees:view";
    public const string MasterData_EmployeesManage = "masterdata:employees:manage";
    public const string MasterData_UnitsOfMeasureView = "masterdata:units-of-measure:view";
    public const string MasterData_UnitsOfMeasureManage = "masterdata:units-of-measure:manage";
    public const string MasterData_GeographyView = "masterdata:geography:view";
    public const string MasterData_GeographyManage = "masterdata:geography:manage";
    public const string MasterData_LanguagesView = "masterdata:languages:view";
    public const string MasterData_LanguagesManage = "masterdata:languages:manage";
    public const string MasterData_CompaniesView = "masterdata:companies:view";
    public const string MasterData_CompaniesManage = "masterdata:companies:manage";
    public const string MasterData_BranchesView = "masterdata:branches:view";
    public const string MasterData_BranchesManage = "masterdata:branches:manage";
    public const string MasterData_DivisionsView = "masterdata:divisions:view";
    public const string MasterData_DivisionsManage = "masterdata:divisions:manage";
    public const string MasterData_TeamsView = "masterdata:teams:view";
    public const string MasterData_TeamsManage = "masterdata:teams:manage";
    public const string MasterData_BusinessUnitsView = "masterdata:business-units:view";
    public const string MasterData_BusinessUnitsManage = "masterdata:business-units:manage";
    public const string MasterData_CostCentersView = "masterdata:cost-centers:view";
    public const string MasterData_CostCentersManage = "masterdata:cost-centers:manage";
    public const string MasterData_ProfitCentersView = "masterdata:profit-centers:view";
    public const string MasterData_ProfitCentersManage = "masterdata:profit-centers:manage";
    // Covers both OrganizationCalendar and its nested Holiday actions — mirrors how
    // Geography's Country/Province/District/Ward already share one View/Manage pair.
    public const string MasterData_OrganizationCalendarsView = "masterdata:organization-calendars:view";
    public const string MasterData_OrganizationCalendarsManage = "masterdata:organization-calendars:manage";
    public const string MasterData_EmployeeTeamAssignmentsView = "masterdata:employee-team-assignments:view";
    public const string MasterData_EmployeeTeamAssignmentsManage = "masterdata:employee-team-assignments:manage";
    // Covers both Currency and its nested ExchangeRate sub-resource — mirrors how
    // OrganizationCalendar/Holiday already share one View/Manage pair.
    public const string MasterData_CurrenciesView = "masterdata:currencies:view";
    public const string MasterData_CurrenciesManage = "masterdata:currencies:manage";
    public const string MasterData_PaymentTermsView = "masterdata:payment-terms:view";
    public const string MasterData_PaymentTermsManage = "masterdata:payment-terms:manage";

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

    // ProductService Permissions — Brand (flat reference list) and ProductUomConversion
    // (per-product UoM conversion) are new entities gated with fine-grained
    // permissions; ProductService's other controllers (Categories, PriceLists,
    // Products, Variants, Images) still rely on the blanket [Authorize] attribute
    // only, so these two are the first to use the View/Manage pairing convention
    // here, enforced inline via IUserContextService.HasPermission(...) — the same
    // lighter-weight pattern SettingsController already uses in this service.
    // Catalog permissions — the codes the Admin SPA already gates on (products:view /
    // create / edit). Before 2026-10-20 none of them existed and every ProductService
    // catalog endpoint only required [Authorize], so any signed-in user (including a
    // storefront Customer, whose role has no permissions) could change the catalog.
    public const string Products_View = "products:view";
    public const string Products_Create = "products:create";
    public const string Products_Edit = "products:edit";
    public const string Products_Delete = "products:delete";
    /// <summary>Bulk import/export and search reindexing.</summary>
    public const string Products_Manage = "products:manage";

    public const string Product_BrandsView = "product:brands:view";
    public const string Product_BrandsManage = "product:brands:manage";
    public const string Product_UomConversionsView = "product:uom-conversions:view";
    public const string Product_UomConversionsManage = "product:uom-conversions:manage";

    // IdentityService Permissions — merged in from IdentityService's own, previously
    // separate ERP.Identity.Application.Authorization.Permissions (2026-07-29
    // consolidation: IdentityService ran a second, DB-lookup-based authorization
    // mechanism with its own duplicate permission-string vocabulary; this merge makes
    // PermissionConstants the single source of truth for every service, Identity
    // included).
    public const string Identity_UsersView = "identity:users:view";
    public const string Identity_UsersCreate = "identity:users:create";
    public const string Identity_UsersEdit = "identity:users:edit";
    public const string Identity_UsersManage = "identity:users:manage";
    public const string Identity_UsersDelete = "identity:users:delete";
    public const string Identity_RolesView = "identity:roles:view";
    public const string Identity_RolesCreate = "identity:roles:create";
    public const string Identity_RolesEdit = "identity:roles:edit";
    public const string Identity_RolesManage = "identity:roles:manage";
    public const string Identity_RolesDelete = "identity:roles:delete";
    public const string Identity_PermissionsView = "identity:permissions:view";
    public const string Identity_PermissionsManage = "identity:permissions:manage";
    public const string Identity_TenantsView = "identity:tenants:view";
    public const string Identity_TenantsCreate = "identity:tenants:create";
    public const string Identity_TenantsEdit = "identity:tenants:edit";
    public const string Identity_TenantsManage = "identity:tenants:manage";
    public const string Identity_TenantsDelete = "identity:tenants:delete";

    // Security Permissions (audit logs, API keys, webhooks) — merged in alongside Identity_* above
    public const string Security_AuditView = "security:audit:view";
    public const string Security_ApiKeysView = "security:apikeys:view";
    public const string Security_ApiKeysManage = "security:apikeys:manage";
    public const string Security_WebhooksView = "security:webhooks:view";
    public const string Security_WebhooksManage = "security:webhooks:manage";

    // License Permissions (module catalog, plan catalog, tenant licenses) — merged in
    // from IdentityService's own LicensePermissions
    public const string License_ModulesManage = "license:modules:manage";
    public const string License_PlansView = "license:plans:view";
    public const string License_PlansManage = "license:plans:manage";
    public const string License_TenantView = "license:tenant:view";
    public const string License_TenantIssue = "license:tenant:issue";

    // Settings Permissions — the platform/system-tier categories that, unlike the
    // business-domain Settings_* categories above, are only ever checked inside
    // IdentityService's own SettingsControllers. Merged in from 11 previously
    // separate per-category *SettingsPermissions classes + the coarse umbrella
    // SettingsPermissions, for the same single-source-of-truth reason as Identity_*.
    public const string Settings_SystemView = "settings:system:view";
    public const string Settings_SystemManage = "settings:system:manage";
    public const string Settings_TenantView = "settings:tenant:view";
    public const string Settings_TenantManage = "settings:tenant:manage";

    public const string Settings_General_SystemView = "settings:general:system:view";
    public const string Settings_General_SystemManage = "settings:general:system:manage";
    public const string Settings_General_TenantView = "settings:general:tenant:view";
    public const string Settings_General_TenantManage = "settings:general:tenant:manage";

    // Company Settings has no System tier — tenant-only resource
    public const string Settings_Company_TenantView = "settings:company:tenant:view";
    public const string Settings_Company_TenantManage = "settings:company:tenant:manage";

    public const string Settings_Scheduler_SystemView = "settings:scheduler:system:view";
    public const string Settings_Scheduler_SystemManage = "settings:scheduler:system:manage";
    public const string Settings_Scheduler_TenantView = "settings:scheduler:tenant:view";
    public const string Settings_Scheduler_TenantManage = "settings:scheduler:tenant:manage";

    public const string Settings_Integration_SystemView = "settings:integration:system:view";
    public const string Settings_Integration_SystemManage = "settings:integration:system:manage";
    public const string Settings_Integration_TenantView = "settings:integration:tenant:view";
    public const string Settings_Integration_TenantManage = "settings:integration:tenant:manage";

    public const string Settings_Api_SystemView = "settings:api:system:view";
    public const string Settings_Api_SystemManage = "settings:api:system:manage";
    public const string Settings_Api_TenantView = "settings:api:tenant:view";
    public const string Settings_Api_TenantManage = "settings:api:tenant:manage";

    public const string Settings_Localization_SystemView = "settings:localization:system:view";
    public const string Settings_Localization_SystemManage = "settings:localization:system:manage";
    public const string Settings_Localization_TenantView = "settings:localization:tenant:view";
    public const string Settings_Localization_TenantManage = "settings:localization:tenant:manage";

    public const string Settings_Audit_SystemView = "settings:audit:system:view";
    public const string Settings_Audit_SystemManage = "settings:audit:system:manage";
    public const string Settings_Audit_TenantView = "settings:audit:tenant:view";
    public const string Settings_Audit_TenantManage = "settings:audit:tenant:manage";

    public const string Settings_FileStorage_SystemView = "settings:file-storage:system:view";
    public const string Settings_FileStorage_SystemManage = "settings:file-storage:system:manage";
    public const string Settings_FileStorage_TenantView = "settings:file-storage:tenant:view";
    public const string Settings_FileStorage_TenantManage = "settings:file-storage:tenant:manage";

    public const string Settings_Advanced_SystemView = "settings:advanced:system:view";
    public const string Settings_Advanced_SystemManage = "settings:advanced:system:manage";
    public const string Settings_Advanced_TenantView = "settings:advanced:tenant:view";
    public const string Settings_Advanced_TenantManage = "settings:advanced:tenant:manage";

    public const string Settings_Workflow_SystemView = "settings:workflow:system:view";
    public const string Settings_Workflow_SystemManage = "settings:workflow:system:manage";
    public const string Settings_Workflow_TenantView = "settings:workflow:tenant:view";
    public const string Settings_Workflow_TenantManage = "settings:workflow:tenant:manage";

    public const string Settings_FeatureFlags_SystemView = "settings:feature-flags:system:view";
    public const string Settings_FeatureFlags_SystemManage = "settings:feature-flags:system:manage";
    public const string Settings_FeatureFlags_TenantView = "settings:feature-flags:tenant:view";
    public const string Settings_FeatureFlags_TenantManage = "settings:feature-flags:tenant:manage";

    /// <summary>
    /// Lấy tất cả permissions
    /// </summary>
    public static string[] GetAllPermissions() =>
    [
        PurchaseOrder_Create, PurchaseOrder_Read, PurchaseOrder_Update, PurchaseOrder_Delete,
        PurchaseOrder_Approve, PurchaseOrder_FinanceApprove, PurchaseOrder_Cancel, PurchaseOrder_AddItem, PurchaseOrder_RemoveItem,
        PurchaseOrder_Receive,
        Supplier_Create, Supplier_Read, Supplier_Update, Supplier_Delete, Supplier_Deactivate, Supplier_Rate,
        PurchaseRequest_View, PurchaseRequest_Manage,
        Rfq_Read, Rfq_Manage, SupplierInvoice_Read, SupplierInvoice_Manage, SupplierInvoice_Approve, SupplierInvoice_ApproveVariance,
        VendorReturn_Read, VendorReturn_Manage, PurchasingSettings_Manage,
        SalesOrder_Create, SalesOrder_Read, SalesOrder_Update, SalesOrder_Approve, SalesOrder_Cancel,
        Accounting_AccountsView, Accounting_AccountsManage,
        Accounting_InvoicesView, Accounting_InvoicesManage,
        Accounting_BillsView, Accounting_BillsManage,
        Accounting_JournalsView, Accounting_JournalsManage,
        Accounting_PeriodsView, Accounting_PeriodsManage,
        Accounting_BankView, Accounting_BankManage,
        Accounting_TaxView, Accounting_TaxManage,
        Accounting_ReportsView,
        Shipping_Create, Shipping_Update, Shipping_View, Shipping_CarriersManage,
        Inventory_View, Inventory_Manage,
        Inventory_Adjustment_Create, Inventory_Adjustment_Approve, Inventory_Stocktake_Approve,
        Inventory_Barcode_View, Inventory_Warehouse_Manage, Inventory_Valuation_Close,
        Payments_View, Payments_Create, Payments_Authorize, Payments_Capture, Payments_Cancel, Payments_Refund,
        Payments_SettlementsView, Payments_SettlementsManage,
        POS_Transactions, POS_TerminalsManage, POS_ReportsView, POS_ShiftsView, POS_ShiftsManage,
        Promotions_Manage, Promotions_View,
        Notifications_Send, Notifications_TemplatesManage, Notifications_WebhooksManage, Notifications_View,
        Reports_SalesView, Reports_InventoryView, Reports_PaymentsView, Reports_PosView, Reports_HrView,
        Logs_View,
        Crm_ContactsView, Crm_ContactsManage, Crm_LeadsView, Crm_LeadsManage,
        Crm_OpportunitiesView, Crm_OpportunitiesManage, Crm_CasesView, Crm_CasesManage,
        Crm_ActivitiesView, Crm_ActivitiesManage,
        Hrm_EmployeesView, Hrm_AttendanceView, Hrm_AttendanceManage, Hrm_LeaveManage,
        Hrm_ShiftsView, Hrm_ShiftsManage, Hrm_PayrollManage,
        MasterData_DepartmentsView, MasterData_DepartmentsManage,
        MasterData_PositionsView, MasterData_PositionsManage,
        MasterData_EmployeesView, MasterData_EmployeesManage,
        MasterData_UnitsOfMeasureView, MasterData_UnitsOfMeasureManage,
        MasterData_GeographyView, MasterData_GeographyManage,
        MasterData_LanguagesView, MasterData_LanguagesManage,
        MasterData_CompaniesView, MasterData_CompaniesManage,
        MasterData_BranchesView, MasterData_BranchesManage,
        MasterData_DivisionsView, MasterData_DivisionsManage,
        MasterData_TeamsView, MasterData_TeamsManage,
        MasterData_BusinessUnitsView, MasterData_BusinessUnitsManage,
        MasterData_CostCentersView, MasterData_CostCentersManage,
        MasterData_ProfitCentersView, MasterData_ProfitCentersManage,
        MasterData_OrganizationCalendarsView, MasterData_OrganizationCalendarsManage,
        MasterData_EmployeeTeamAssignmentsView, MasterData_EmployeeTeamAssignmentsManage,
        MasterData_CurrenciesView, MasterData_CurrenciesManage,
        MasterData_PaymentTermsView, MasterData_PaymentTermsManage,
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
        Products_View, Products_Create, Products_Edit, Products_Delete, Products_Manage,
        Product_BrandsView, Product_BrandsManage,
        Product_UomConversionsView, Product_UomConversionsManage,
        Identity_UsersView, Identity_UsersCreate, Identity_UsersEdit, Identity_UsersManage, Identity_UsersDelete,
        Identity_RolesView, Identity_RolesCreate, Identity_RolesEdit, Identity_RolesManage, Identity_RolesDelete,
        Identity_PermissionsView, Identity_PermissionsManage,
        Identity_TenantsView, Identity_TenantsCreate, Identity_TenantsEdit, Identity_TenantsManage, Identity_TenantsDelete,
        Security_AuditView, Security_ApiKeysView, Security_ApiKeysManage, Security_WebhooksView, Security_WebhooksManage,
        License_ModulesManage, License_PlansView, License_PlansManage, License_TenantView, License_TenantIssue,
        Settings_SystemView, Settings_SystemManage, Settings_TenantView, Settings_TenantManage,
        Settings_General_SystemView, Settings_General_SystemManage,
        Settings_General_TenantView, Settings_General_TenantManage,
        Settings_Company_TenantView, Settings_Company_TenantManage,
        Settings_Scheduler_SystemView, Settings_Scheduler_SystemManage,
        Settings_Scheduler_TenantView, Settings_Scheduler_TenantManage,
        Settings_Integration_SystemView, Settings_Integration_SystemManage,
        Settings_Integration_TenantView, Settings_Integration_TenantManage,
        Settings_Api_SystemView, Settings_Api_SystemManage,
        Settings_Api_TenantView, Settings_Api_TenantManage,
        Settings_Localization_SystemView, Settings_Localization_SystemManage,
        Settings_Localization_TenantView, Settings_Localization_TenantManage,
        Settings_Audit_SystemView, Settings_Audit_SystemManage,
        Settings_Audit_TenantView, Settings_Audit_TenantManage,
        Settings_FileStorage_SystemView, Settings_FileStorage_SystemManage,
        Settings_FileStorage_TenantView, Settings_FileStorage_TenantManage,
        Settings_Advanced_SystemView, Settings_Advanced_SystemManage,
        Settings_Advanced_TenantView, Settings_Advanced_TenantManage,
        Settings_Workflow_SystemView, Settings_Workflow_SystemManage,
        Settings_Workflow_TenantView, Settings_Workflow_TenantManage,
        Settings_FeatureFlags_SystemView, Settings_FeatureFlags_SystemManage,
        Settings_FeatureFlags_TenantView, Settings_FeatureFlags_TenantManage,
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
