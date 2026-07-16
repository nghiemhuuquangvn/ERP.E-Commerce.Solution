namespace ERP.Shared.Authorization;

/// <summary>
/// Canonical module codes for the 14 licensable backend services (everything except
/// IdentityService, which is the platform itself and is never gated by a license).
/// Matches the short codes already used in CLAUDE.md and the Ocelot Gateway's route
/// prefixes (e.g. "/wh/", "/acc/") — this is the single source of truth other code
/// should reference instead of re-typing the string literal.
/// </summary>
public static class ModuleCodes
{
    public const string SaleOrder = "SO";
    public const string PurchaseOrder = "PO";
    public const string Warehouse = "WH";
    public const string Accounting = "ACC";
    public const string Crm = "CRM";
    public const string Hrm = "HRM";
    public const string Product = "PRD";
    public const string Payment = "PAY";
    public const string Shipping = "SHP";
    public const string Pos = "POS";
    public const string Promotion = "PRM";
    public const string Notification = "NTF";
    public const string Reporting = "RPT";
    public const string SharedMasterData = "MST";

    public static readonly string[] All =
    [
        SaleOrder, PurchaseOrder, Warehouse, Accounting, Crm, Hrm, Product,
        Payment, Shipping, Pos, Promotion, Notification, Reporting, SharedMasterData
    ];
}
