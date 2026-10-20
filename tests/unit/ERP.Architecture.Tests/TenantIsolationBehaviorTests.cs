using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Two-tenant behaviour of each service's real DbContext (EF InMemory): tenant B never sees
/// tenant A's rows, and a row of another tenant can't be saved.
/// </summary>
public class TenantIsolationBehaviorTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private readonly string _database = Guid.NewGuid().ToString();

    private T Db<T>(Guid? tenant) where T : DbContext =>
        (T)TenantFilterCoverageTests.Create(typeof(T), tenant, _database);

    /// <summary>Seeds <paramref name="entity"/> as tenant A, then checks isolation from tenant B.</summary>
    private void AssertIsolated<TContext, TEntity>(TEntity entity, Func<TEntity, Guid> tenantOf)
        where TContext : DbContext where TEntity : class
    {
        using (var a = Db<TContext>(TenantA))
        {
            a.Set<TEntity>().Add(entity);
            a.SaveChanges();
        }

        tenantOf(entity).Should().Be(TenantA, "a new row is stamped with the caller's tenant");

        using (var a = Db<TContext>(TenantA))
            a.Set<TEntity>().Should().HaveCount(1);

        using (var b = Db<TContext>(TenantB))
        {
            b.Set<TEntity>().Should().BeEmpty("tenant B must not see tenant A's rows");

            b.Set<TEntity>().Attach(entity);
            b.Entry(entity).State = EntityState.Modified;
            var act = () => b.SaveChanges();
            act.Should().Throw<TenantIsolationViolationException>("tenant B must not write tenant A's rows");
        }

        using (var none = Db<TContext>(null))
            none.Set<TEntity>().Should().BeEmpty("no tenant = fail closed");
    }

    [Fact]
    public void SaleOrder_CommissionPlans() =>
        AssertIsolated<ERP.SO.Infrastructure.SalesOrderDbContext, ERP.SO.Domain.Entities.CommissionPlan>(
            ERP.SO.Domain.Entities.CommissionPlan.Create("Standard", 0.05m), e => e.TenantId);

    [Fact]
    public void SaleOrder_CustomerSalesProfiles() =>
        AssertIsolated<ERP.SO.Infrastructure.SalesOrderDbContext, ERP.SO.Domain.Entities.CustomerSalesProfile>(
            ERP.SO.Domain.Entities.CustomerSalesProfile.Create(Guid.NewGuid(), 1000m), e => e.TenantId);
}
