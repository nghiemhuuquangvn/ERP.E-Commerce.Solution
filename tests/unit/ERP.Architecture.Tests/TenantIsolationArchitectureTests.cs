using System.Reflection;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Every DbContext that maps a tenant-owned type (<see cref="ITenantOwned"/>: BaseEntity and
/// POCOs that opt in) must be tenant-scoped — implement <see cref="ITenantScopedDbContext"/>
/// and call ApplyTenantQueryFilters / EnforceTenantOnSave (CLAUDE.md "Multi-Tenant Isolation").
/// Otherwise every tenant reads every other tenant's rows.
/// </summary>
public class TenantIsolationArchitectureTests
{
    private static readonly string[] ApiAssemblies =
    [
        "ERP.ACC.API", "ERP.CRM.API", "ERP.CHB.API", "ERP.HRM.API", "ERP.Identity.API",
        "ERP.NTF.API", "ERP.POS.API", "ERP.PAY.API", "ERP.PRD.API", "ERP.PRM.API",
        "ERP.PO.Api", "ERP.RPT.API", "ERP.SO.Api", "ERP.MST.API", "ERP.SHP.API", "ERP.WH.API",
    ];

    /// <summary>
    /// Contexts not tenant-scoped yet (rollout in progress — the list may only shrink).
    /// AuthDbContext is permanent: IdentityService isolates with explicit per-query tenant
    /// predicates (login must look users up across the tenant resolved from the host).
    /// </summary>
    private static readonly HashSet<string> NotYetScoped = new(StringComparer.Ordinal)
    {
        "ERP.Identity.Infrastructure.Persistence.AuthDbContext",
        // Phase 2 rollout (2026-10-20) — remove each one as its service is isolated.
        "ERP.ACC.Infrastructure.Persistence.AccountingDbContext",
        "ERP.CHB.Infrastructure.Persistence.ChatBotDbContext",
        "ERP.HRM.Infrastructure.Persistence.HrmDbContext",
        "ERP.MST.Infrastructure.Persistence.MasterDataDbContext",
        "ERP.NTF.Infrastructure.Persistence.NotificationDbContext",
        "ERP.PAY.Infrastructure.Persistence.PaymentDbContext",
        "ERP.POS.Infrastructure.Persistence.POSDbContext",
        "ERP.PRD.Infrastructure.Persistence.ProductDbContext",
        "ERP.PRM.Infrastructure.Persistence.PromotionDbContext",
        "ERP.SHP.Infrastructure.Persistence.ShippingDbContext",
        "ERP.SO.Infrastructure.SalesOrderDbContext",
    };

    private static IEnumerable<Type> DbContextTypes()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>(ApiAssemblies.Select(Assembly.Load));
        while (queue.Count > 0)
        {
            var assembly = queue.Dequeue();
            if (!seen.Add(assembly.GetName().Name!))
                continue;

            foreach (var type in assembly.GetTypes()
                         .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(DbContext).IsAssignableFrom(t)))
                yield return type;

            foreach (var reference in assembly.GetReferencedAssemblies()
                         .Where(r => r.Name!.StartsWith("ERP.", StringComparison.Ordinal)
                                     && !r.Name.StartsWith("ERP.Shared", StringComparison.Ordinal)))
                queue.Enqueue(Assembly.Load(reference));
        }
    }

    private static bool MapsTenantOwnedTypes(Type context) =>
        context.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.PropertyType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(t => t.GetGenericArguments()[0])
            .Any(e => typeof(ITenantOwned).IsAssignableFrom(e) && !typeof(IPlatformWideEntity).IsAssignableFrom(e));

    [Fact]
    public void EveryDbContextWithTenantOwnedRows_IsTenantScoped()
    {
        var offenders = DbContextTypes()
            .Where(MapsTenantOwnedTypes)
            .Where(c => !typeof(ITenantScopedDbContext).IsAssignableFrom(c))
            .Select(c => c.FullName!)
            .Where(name => !NotYetScoped.Contains(name))
            .OrderBy(n => n)
            .ToList();

        offenders.Should().BeEmpty(
            "a DbContext mapping ITenantOwned rows must implement ITenantScopedDbContext, call " +
            "modelBuilder.ApplyTenantQueryFilters(this) and ChangeTracker.EnforceTenantOnSave(...)");
    }

    [Fact]
    public void NotYetScopedList_HasNoStaleEntries()
    {
        var unscoped = DbContextTypes()
            .Where(c => !typeof(ITenantScopedDbContext).IsAssignableFrom(c))
            .Select(c => c.FullName!)
            .ToHashSet(StringComparer.Ordinal);

        NotYetScoped.Where(n => !unscoped.Contains(n)).Should().BeEmpty("remove contexts that are tenant-scoped now");
    }
}
