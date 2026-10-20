using System.Linq.Expressions;
using ERP.Shared.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ERP.Shared.Infrastructure.Data;

/// <summary>A DbContext whose tenant-owned rows are isolated per tenant.</summary>
public interface ITenantScopedDbContext
{
    /// <summary>Tenant of the current unit of work; null = unknown (fail closed).</summary>
    Guid? CurrentTenantId { get; }
}

/// <summary>
/// Multi-tenant isolation for every <see cref="ITenantOwned"/> type in a DbContext
/// (<see cref="BaseEntity"/> and plain POCOs alike): a global query filter
/// (<see cref="ApplyTenantQueryFilters"/>) plus write-side stamping and validation
/// (<see cref="EnforceTenantOnSave"/>).
/// <list type="bullet">
/// <item><see cref="IPlatformWideEntity"/> rows (System*Settings) are not filtered; only the
/// platform tenant — or work with no tenant (seed/jobs) — may change them.</item>
/// <item><see cref="ISharedReferenceData"/> rows are visible to a tenant when they are its
/// own or the platform tenant's; only the platform tenant may change the shared ones.</item>
/// </list>
/// </summary>
public static class TenantFilterConfiguration
{
    private const string TenantIdProperty = nameof(ITenantOwned.TenantId);

    /// <summary>
    /// Adds <c>e =&gt; e.TenantId == context.CurrentTenantId</c> to every tenant-owned type.
    /// The filter references the DbContext INSTANCE, which EF Core turns into a query
    /// parameter re-read for every query — unlike the previous constant-valued filter,
    /// which EF baked into the once-per-process cached model, so every request was filtered
    /// by whichever tenant happened to build the model first. A null CurrentTenantId
    /// compares as SQL <c>= NULL</c> and matches no rows.
    /// Call from OnModelCreating AFTER entity configuration (an entity's own HasQueryFilter
    /// would otherwise be overwritten).
    /// </summary>
    public static void ApplyTenantQueryFilters(this ModelBuilder builder, ITenantScopedDbContext context)
    {
        var contextExpression = Expression.Constant(context);
        var currentTenant = Expression.Property(contextExpression, nameof(ITenantScopedDbContext.CurrentTenantId));

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(ITenantOwned).IsAssignableFrom(clrType)
                || typeof(IPlatformWideEntity).IsAssignableFrom(clrType)
                || entityType.BaseType != null   // derived types inherit the root's filter
                || entityType.IsOwned())         // owned types follow their owner
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            var tenantId = Expression.Convert(Expression.Property(parameter, TenantIdProperty), typeof(Guid?));
            Expression tenantFilter = Expression.Equal(tenantId, currentTenant);

            if (typeof(ISharedReferenceData).IsAssignableFrom(clrType))
                tenantFilter = Expression.OrElse(
                    tenantFilter,
                    Expression.Equal(tenantId, Expression.Constant(WellKnownTenants.Platform, typeof(Guid?))));

            var existing = entityType.GetQueryFilter();
            var body = existing == null
                ? tenantFilter
                : Expression.AndAlso(
                    new ReplaceParameterVisitor(existing.Parameters[0], parameter).Visit(existing.Body)!,
                    tenantFilter);

            entityType.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>
    /// Call at the start of SaveChanges. New tenant-owned rows without a TenantId get the
    /// current tenant; any Added/Modified/Deleted row belonging to a different tenant — or
    /// any tenant-owned write with no tenant known at all — throws instead of being saved.
    /// </summary>
    public static void EnforceTenantOnSave(this ChangeTracker changeTracker, Guid? currentTenantId)
    {
        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            switch (entry.Entity)
            {
                case IPlatformWideEntity:
                    // Added is allowed: services lazily create the default singleton row
                    // (well-known id) on first read, whoever reads first.
                    if (entry.State != EntityState.Added
                        && currentTenantId is { } tenant && tenant != WellKnownTenants.Platform)
                        throw new TenantIsolationViolationException(
                            $"Cannot change platform-wide {entry.Entity.GetType().Name}: only the platform tenant may.");
                    continue;

                case ITenantOwned owned:
                    EnforceOwned(entry, owned, currentTenantId);
                    continue;
            }
        }
    }

    private static void EnforceOwned(EntityEntry entry, ITenantOwned owned, Guid? currentTenantId)
    {
        var name = entry.Entity.GetType().Name;

        if (currentTenantId is not { } tenant)
            throw new TenantIsolationViolationException(
                $"Cannot save {name}: no tenant is associated with the current operation.");

        if (entry.State == EntityState.Added && owned.TenantId == Guid.Empty)
        {
            // Through EF, so POCOs with a private setter are stamped too.
            entry.Property(TenantIdProperty).CurrentValue = tenant;
            return;
        }

        if (owned.TenantId == tenant)
            return;

        if (owned is ISharedReferenceData && owned.TenantId == WellKnownTenants.Platform)
            throw new TenantIsolationViolationException(
                $"Cannot change shared {name} {KeyOf(entry)}: shared reference data belongs to the platform tenant.");

        throw new TenantIsolationViolationException(
            $"Cannot save {name} {KeyOf(entry)}: it belongs to a different tenant.");
    }

    private static string KeyOf(EntityEntry entry) =>
        string.Join(",", entry.Metadata.FindPrimaryKey()?.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString()) ?? []);

    [Obsolete("Bakes a constant tenant into EF's cached model (first caller's tenant wins for the whole process). Use ApplyTenantQueryFilters with an ITenantScopedDbContext.")]
    public static void ConfigureTenantFilter(ModelBuilder builder, Guid tenantId)
    {
        throw new NotSupportedException(
            "ConfigureTenantFilter is unsafe (see Obsolete message); use ApplyTenantQueryFilters.");
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}

public sealed class TenantIsolationViolationException(string message) : InvalidOperationException(message);
