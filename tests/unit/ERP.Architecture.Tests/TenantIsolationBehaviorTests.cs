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

    [Fact]
    public void Payment_Payments() =>
        AssertIsolated<ERP.PAY.Infrastructure.Persistence.PaymentDbContext, ERP.PAY.Domain.Entities.Payment>(
            ERP.PAY.Domain.Entities.Payment.Initiate(Guid.NewGuid(), 100m, ERP.PAY.Domain.Enums.PaymentMethod.CreditCard), e => e.TenantId);

    [Fact]
    public void Accounting_Accounts() =>
        AssertIsolated<ERP.ACC.Infrastructure.Persistence.AccountingDbContext, ERP.ACC.Domain.Entities.Account>(
            ERP.ACC.Domain.Entities.Account.Create("1111", "Cash", 1, 1), e => e.TenantId);

    [Fact]
    public void Accounting_TaxRates() =>
        AssertIsolated<ERP.ACC.Infrastructure.Persistence.AccountingDbContext, ERP.ACC.Domain.Entities.TaxRate>(
            ERP.ACC.Domain.Entities.TaxRate.Create("VAT 10%", "VAT10", 10m), e => e.TenantId);

    [Fact]
    public void Product_Brands() =>
        AssertIsolated<ERP.PRD.Infrastructure.Persistence.ProductDbContext, ERP.PRD.Domain.Entities.Brand>(
            ERP.PRD.Domain.Entities.Brand.Create("ACME", "Acme"), e => e.TenantId);

    [Fact]
    public void Product_Categories() =>
        AssertIsolated<ERP.PRD.Infrastructure.Persistence.ProductDbContext, ERP.PRD.Domain.Entities.Category>(
            ERP.PRD.Domain.Entities.Category.Create("Shirts", code: "SHIRTS"), e => e.TenantId);

    [Fact]
    public void Warehouse_Warehouses() =>
        AssertIsolated<ERP.WH.Infrastructure.Persistence.WarehouseDbContext, ERP.WH.Domain.Aggregates.Location.Warehouse>(
            new ERP.WH.Domain.Aggregates.Location.Warehouse("WH01", "Main"), e => e.TenantId);

    [Fact]
    public void Warehouse_BarcodeMappings() =>
        AssertIsolated<ERP.WH.Infrastructure.Persistence.WarehouseDbContext, ERP.WH.Domain.Aggregates.Inventory.BarcodeMapping>(
            new ERP.WH.Domain.Aggregates.Inventory.BarcodeMapping("8930001", Guid.NewGuid()), e => e.TenantId);

    [Fact]
    public void PurchaseOrder_PurchaseRequests() =>
        AssertIsolated<ERP.PO.Infrastructure.Data.PurchasingDbContext, ERP.PO.Domain.Entities.PurchaseRequest>(
            ERP.PO.Domain.Entities.PurchaseRequest.Create(Guid.NewGuid(), "Widget", 1, 5, 10), e => e.TenantId);

    [Fact]
    public void Shipping_Carriers() =>
        AssertIsolated<ERP.SHP.Infrastructure.Persistence.ShippingDbContext, ERP.SHP.Domain.Entities.Carrier>(
            ERP.SHP.Domain.Entities.Carrier.Create("GHN", "GHN", 10000m), e => e.TenantId);

    [Fact]
    public void Promotion_Promotions() =>
        AssertIsolated<ERP.PRM.Infrastructure.Persistence.PromotionDbContext, ERP.PRM.Domain.Entities.Promotion>(
            ERP.PRM.Domain.Entities.Promotion.Create("Autumn sale", ERP.PRM.Domain.Enums.DiscountType.Percentage, 10m,
                DateTime.UtcNow, DateTime.UtcNow.AddDays(7)), e => e.TenantId);

    [Fact]
    public void Promotion_ProductCache() =>
        AssertIsolated<ERP.PRM.Infrastructure.Persistence.PromotionDbContext, ERP.PRM.Infrastructure.Models.PromotionProductCache>(
            new ERP.PRM.Infrastructure.Models.PromotionProductCache { ProductId = Guid.NewGuid(), Category = "Shirts", Price = 10m }, e => e.TenantId);

    [Fact]
    public void Pos_Terminals() =>
        AssertIsolated<ERP.POS.Infrastructure.Persistence.POSDbContext, ERP.POS.Domain.Entities.Terminal>(
            ERP.POS.Domain.Entities.Terminal.Create("Counter 1"), e => e.TenantId);

    [Fact]
    public void Pos_ProductCache() =>
        AssertIsolated<ERP.POS.Infrastructure.Persistence.POSDbContext, ERP.POS.Application.EventHandlers.PosProductCache>(
            new ERP.POS.Application.EventHandlers.PosProductCache { ProductId = Guid.NewGuid(), Name = "Tee", Sku = "TEE", Price = 10m }, e => e.TenantId);

    [Fact]
    public void Hrm_WorkShifts() =>
        AssertIsolated<ERP.HRM.Infrastructure.Persistence.HrmDbContext, ERP.HRM.Domain.Entities.WorkShift>(
            ERP.HRM.Domain.Entities.WorkShift.Create("Morning", TimeSpan.FromHours(8), TimeSpan.FromHours(17)), e => e.TenantId);

    [Fact]
    public void MasterData_Departments() =>
        AssertIsolated<ERP.MST.Infrastructure.Persistence.MasterDataDbContext, ERP.MST.Domain.Entities.Department>(
            ERP.MST.Domain.Entities.Department.Create("Sales", "SALES"), e => e.TenantId);

    [Fact]
    public void MasterData_Countries_AreSharedFromThePlatformTenant_ReadOnlyForTenants()
    {
        var platform = ERP.Shared.Core.Domain.WellKnownTenants.Platform;
        var vn = ERP.MST.Domain.Entities.Country.Create("VN", "Viet Nam");
        using (var p = Db<ERP.MST.Infrastructure.Persistence.MasterDataDbContext>(platform))
        {
            p.Set<ERP.MST.Domain.Entities.Country>().Add(vn);
            p.SaveChanges();
        }

        using (var a = Db<ERP.MST.Infrastructure.Persistence.MasterDataDbContext>(TenantA))
        {
            a.Set<ERP.MST.Domain.Entities.Country>().Add(ERP.MST.Domain.Entities.Country.Create("XA", "Only A"));
            a.SaveChanges();
            a.Set<ERP.MST.Domain.Entities.Country>().Select(c => c.Code).Should().BeEquivalentTo("VN", "XA");
        }

        using var b = Db<ERP.MST.Infrastructure.Persistence.MasterDataDbContext>(TenantB);
        b.Set<ERP.MST.Domain.Entities.Country>().Select(c => c.Code).Should().BeEquivalentTo(new[] { "VN" },
            "a tenant sees the shared rows but never another tenant's additions");
        b.Entry(b.Set<ERP.MST.Domain.Entities.Country>().Single()).State = EntityState.Modified;
        var act = () => b.SaveChanges();
        act.Should().Throw<TenantIsolationViolationException>("only the platform tenant may change shared reference data");
    }
}
