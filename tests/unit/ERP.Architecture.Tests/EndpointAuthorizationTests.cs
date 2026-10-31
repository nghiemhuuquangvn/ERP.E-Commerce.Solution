using System.Reflection;
using ERP.Shared.Authorization;
using ERP.Shared.Authorization.ServiceAuth;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Every HTTP action in every API must state its authorization explicitly:
/// [AuthorizePermission] / [AuthorizeAnyPermission] (a permission), [AuthorizeAnyAuthenticatedUser]
/// (deliberately any signed-in user), [AllowAnonymous] (deliberately public) or
/// [AuthorizeInternalService] (service-to-service only, see InternalEndpointTests) — on the action
/// or its controller. A bare [Authorize] or nothing at all fails the build: that is exactly how
/// ProductService ended up letting storefront customers edit the catalog.
/// </summary>
public class EndpointAuthorizationTests
{
    private static readonly string[] ApiAssemblies =
    [
        "Ocelot.API", "ERP.ACC.API", "ERP.CRM.API", "ERP.CHB.API", "ERP.HRM.API", "ERP.Identity.API",
        "ERP.LOG.API", "ERP.NTF.API", "ERP.POS.API", "ERP.PAY.API", "ERP.PRD.API", "ERP.PRM.API",
        "ERP.PO.Api", "ERP.RPT.API", "ERP.SO.Api", "ERP.MST.API", "ERP.SHP.API", "ERP.WH.API",
    ];

    /// <summary>
    /// Controllers that still check permissions INSIDE each action
    /// (`if (!_userContext.HasPermission(...)) return Forbid();`) — reflection can't see that.
    /// Migrating them to attributes is phase 5 of the service-authz plan; the list may only
    /// shrink (see AllowlistHasNoStaleEntries).
    /// </summary>
    private static readonly HashSet<string> InActionCheckAllowlist = new(StringComparer.Ordinal)
    {
        "ERP.ACC.API:ERP.ACC.API.Controllers.AccountsController",
        "ERP.ACC.API:ERP.ACC.API.Controllers.BillsController",
        "ERP.ACC.API:ERP.ACC.API.Controllers.FiscalPeriodsController",
        "ERP.ACC.API:ERP.ACC.API.Controllers.InvoicesController",
        "ERP.ACC.API:ERP.ACC.API.Controllers.JournalEntriesController",
        "ERP.ACC.API:ERP.ACC.API.Controllers.ReportsController",
        "ERP.ACC.API:ERP.ACC.API.Controllers.SettingsController",
        "ERP.ACC.API:ERP.ACC.API.Controllers.TaxRatesController",
        "ERP.MST.API:ERP.MST.API.Controllers.SettingsController",
        "ERP.NTF.API:ERP.NTF.API.Controllers.NotificationsController",
        "ERP.NTF.API:ERP.NTF.API.Controllers.PreferencesController",
        "ERP.NTF.API:ERP.NTF.API.Controllers.SettingsController",
        "ERP.PAY.API:ERP.PAY.API.Controllers.InstallmentsController",
        "ERP.PAY.API:ERP.PAY.API.Controllers.PaymentMethodsController",
        "ERP.PAY.API:ERP.PAY.API.Controllers.PaymentsController",
        "ERP.PAY.API:ERP.PAY.API.Controllers.SettingsController",
        "ERP.PAY.API:ERP.PAY.API.Controllers.SettlementsController",
        "ERP.POS.API:ERP.POS.API.Controllers.CashShiftsController",
        "ERP.POS.API:ERP.POS.API.Controllers.PosProductsController",
        "ERP.POS.API:ERP.POS.API.Controllers.ReportsController",
        "ERP.POS.API:ERP.POS.API.Controllers.TerminalsController",
        "ERP.POS.API:ERP.POS.API.Controllers.TransactionsController",
        "ERP.PRD.API:ERP.PRD.API.Controllers.BrandsController",
        "ERP.PRD.API:ERP.PRD.API.Controllers.ProductUomConversionsController",
        "ERP.PRD.API:ERP.PRD.API.Controllers.SettingsController",
        "ERP.PRM.API:ERP.PRM.API.Controllers.FlashSalesController",
        "ERP.PRM.API:ERP.PRM.API.Controllers.LoyaltyController",
        "ERP.PRM.API:ERP.PRM.API.Controllers.PromotionsController",
        "ERP.RPT.API:ERP.RPT.API.Controllers.ReportsController",
        "ERP.SHP.API:ERP.SHP.API.Controllers.CarriersController",
        "ERP.SHP.API:ERP.SHP.API.Controllers.ReturnShipmentsController",
        "ERP.SHP.API:ERP.SHP.API.Controllers.SettingsController",
        "ERP.SHP.API:ERP.SHP.API.Controllers.ShipmentsController",
        "ERP.SO.Api:ERP.SO.Api.Controllers.CommissionPlansController",
        "ERP.SO.Api:ERP.SO.Api.Controllers.CommissionsController",
        "ERP.SO.Api:ERP.SO.Api.Controllers.CustomerSalesProfilesController",
        "ERP.SO.Api:ERP.SO.Api.Controllers.QuotationsController",
        "ERP.SO.Api:ERP.SO.Api.Controllers.SalesOrdersController",
        "ERP.SO.Api:ERP.SO.Api.Controllers.SettingsController",
        "ERP.WH.API:ERP.WH.API.Controllers.BarcodeMappingsController",
        "ERP.WH.API:ERP.WH.API.Controllers.InventoryController",
        "ERP.WH.API:ERP.WH.API.Controllers.InventoryValuationSnapshotsController",
        "ERP.WH.API:ERP.WH.API.Controllers.LocationsController",
        "ERP.WH.API:ERP.WH.API.Controllers.LotsController",
        "ERP.WH.API:ERP.WH.API.Controllers.SerialsController",
        "ERP.WH.API:ERP.WH.API.Controllers.SettingsController",
        "ERP.WH.API:ERP.WH.API.Controllers.StockAdjustmentsController",
        "ERP.WH.API:ERP.WH.API.Controllers.StockMovementController",
        "ERP.WH.API:ERP.WH.API.Controllers.StocktakesController",
        "ERP.WH.API:ERP.WH.API.Controllers.TransferOrdersController",
        "ERP.WH.API:ERP.WH.API.Controllers.WarehousesController",
        "Ocelot.API:Ocelot.API.Controllers.MonitoringController",
    };

    private static readonly Type[] ExplicitAttributes =
    [
        typeof(AuthorizePermissionAttribute),
        typeof(AuthorizeAnyPermissionAttribute),
        typeof(AuthorizeAnyAuthenticatedUserAttribute),
        typeof(AllowAnonymousAttribute),
        typeof(AuthorizeInternalServiceAttribute),
    ];

    internal static IEnumerable<(Type Controller, MethodInfo Action)> Actions() =>
        ApiAssemblies
            .Select(Assembly.Load)
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(m => (t, m)));

    private static bool IsExplicit(MemberInfo member) =>
        ExplicitAttributes.Any(a => member.GetCustomAttributes(a, inherit: true).Length > 0);

    private static string Name(Type controller) => $"{controller.Assembly.GetName().Name}:{controller.FullName}";

    [Fact]
    public void EveryActionDeclaresItsAuthorization()
    {
        var offenders = Actions()
            .Where(x => !IsExplicit(x.Action) && !IsExplicit(x.Controller) && !InActionCheckAllowlist.Contains(Name(x.Controller)))
            .Select(x => $"{Name(x.Controller)}.{x.Action.Name}")
            .OrderBy(x => x)
            .ToList();

        offenders.Should().BeEmpty(
            "every action needs [AuthorizePermission]/[AuthorizeAnyPermission]/[AuthorizeAnyAuthenticatedUser]/[AllowAnonymous]:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void AllowlistHasNoStaleEntries()
    {
        var actions = Actions().ToList();
        var stale = InActionCheckAllowlist
            .Where(name => !actions.Any(x => Name(x.Controller) == name
                                             && !IsExplicit(x.Action) && !IsExplicit(x.Controller)))
            .ToList();

        stale.Should().BeEmpty("fully attributed (or deleted) controllers must leave the allowlist");
    }
}
