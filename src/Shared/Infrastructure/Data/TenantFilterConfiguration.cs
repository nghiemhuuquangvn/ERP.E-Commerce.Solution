using System.Linq.Expressions;
using ERP.Shared.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Shared.Infrastructure.Data;

/// <summary>
/// Configures automatic tenant filtering for all entities.
/// Ensures that queries are automatically filtered to only include data for the current tenant.
/// </summary>
public static class TenantFilterConfiguration
{
    public static void ConfigureTenantFilter(ModelBuilder builder, Guid tenantId)
    {
        var entityTypes = builder.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            // Only configure filter for types that inherit from BaseEntity
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            // Create expression: e => e.TenantId == tenantId
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var propertyAccess = Expression.Property(parameter, nameof(BaseEntity.TenantId));
            var tenantIdConstant = Expression.Constant(tenantId);
            var tenantFilter = Expression.Equal(propertyAccess, tenantIdConstant);
            var lambda = Expression.Lambda(tenantFilter, parameter);

            // Set query filter
            entityType.SetQueryFilter(lambda);
        }
    }
}
