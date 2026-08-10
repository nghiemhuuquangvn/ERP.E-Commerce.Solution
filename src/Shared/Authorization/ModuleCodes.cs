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
    public const string ChatBot = "CHB";

    /// <summary>
    /// Not a real service — a tier flag layered on top of <see cref="ChatBot"/>.
    /// A tenant needs <see cref="ChatBot"/> licensed to use the chatbot at all
    /// (this is what <c>LicenseEnforcementMiddleware</c>/<c>ModuleIdentity</c> gates
    /// on for ChatBotService); a tenant additionally holding this code gets routed to
    /// the paid Claude provider instead of the free ChatGPT provider. Read directly via
    /// <see cref="UserContext.HasModule"/> inside ChatBotService's own provider
    /// resolver — never registered as a service's <c>ModuleIdentity</c> since the shared
    /// middleware only checks one code per service. See docs/business/Identity/License.md.
    /// </summary>
    public const string ChatBotPremium = "CHB_PREMIUM";

    public static readonly string[] All =
    [
        SaleOrder, PurchaseOrder, Warehouse, Accounting, Crm, Hrm, Product,
        Payment, Shipping, Pos, Promotion, Notification, Reporting, SharedMasterData,
        ChatBot, ChatBotPremium
    ];
}
