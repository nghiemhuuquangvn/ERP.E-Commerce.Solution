using System.Linq.Expressions;
using ERP.Shared.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ERP.Shared.Infrastructure.Data;

/// <summary>A DbContext whose BaseEntity rows are isolated per tenant.</summary>
public interface ITenantScopedDbContext
{
    /// <summary>Tenant of the current unit of work; null = unknown (fail closed).</summary>
    Guid? CurrentTenantId { get; }
}

/// <summary>
/// Multi-tenant isolation for every <see cref="BaseEntity"/> in a DbContext:
/// a global query filter (<see cref="ApplyTenantQueryFilters"/>) plus write-side stamping
/// and validation (<see cref="EnforceTenantOnSave"/>).
/// </summary>
public static class TenantFilterConfiguration
{
    /// <summary>
    /// Adds <c>e =&gt; e.TenantId == context.CurrentTenantId</c> to every BaseEntity type.
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
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType) || entityType.BaseType != null)
                continue; // derived types inherit the root's filter

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantId = Expression.Convert(
                Expression.Property(parameter, nameof(BaseEntity.TenantId)), typeof(Guid?));
            var tenantFilter = Expression.Equal(tenantId, currentTenant);

            var existing = entityType.GetQueryFilter();
            var body = existing == null
                ? (Expression)tenantFilter
                : Expression.AndAlso(
                    new ReplaceParameterVisitor(existing.Parameters[0], parameter).Visit(existing.Body)!,
                    tenantFilter);

            entityType.SetQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>
    /// Call at the start of SaveChanges. New BaseEntity rows without a TenantId get the
    /// current tenant; any Added/Modified row belonging to a different tenant — or any
    /// tenant-owned write with no tenant known at all — throws instead of being saved.
    /// </summary>
    public static void EnforceTenantOnSave(this ChangeTracker changeTracker, Guid? currentTenantId)
    {
        foreach (var entry in changeTracker.Entries<BaseEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            if (currentTenantId is null)
                throw new TenantIsolationViolationException(
                    $"Cannot save {entry.Entity.GetType().Name}: no tenant is associated with the current operation.");

            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = currentTenantId.Value;
                continue;
            }

            if (entry.Entity.TenantId != currentTenantId.Value)
                throw new TenantIsolationViolationException(
                    $"Cannot save {entry.Entity.GetType().Name} {entry.Entity.Id}: it belongs to a different tenant.");
        }
    }

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
