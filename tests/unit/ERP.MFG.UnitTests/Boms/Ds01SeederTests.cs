using ERP.MFG.Application;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Boms;
using ERP.MFG.Application.Features.Routings;
using ERP.MFG.Application.Features.Seeding;
using ERP.MFG.Application.Services;
using ERP.MFG.Infrastructure.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Common;
using ERP.Shared.Authorization;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.MFG.UnitTests.Boms;

/// <summary>The DS-01 demo seeder (M6) runs the real handlers over in-memory stores: the data must satisfy the numbers of the test-case book, and a second run must create nothing.</summary>
public class Ds01SeederTests
{
    private readonly Fakes _f = new();
    private readonly ServiceProvider _sp;
    private readonly Dictionary<string, Guid> _units = [];

    public Ds01SeederTests()
    {
        foreach (var (code, dec) in new[] { ("PCS", 0), ("SET", 0), ("M3", 3), ("L", 2), ("KG", 2) })
        {
            var id = Guid.NewGuid();
            _units[code] = id;
            _f.Units[id] = new UnitInfo(id, code, code, dec, null, 1m);
        }
        foreach (var (sku, unit) in new[] { ("TP-BAN01", "PCS"), ("BTP-MAT01", "PCS"), ("BTP-CHAN01", "PCS"), ("KIT-PKG01", "SET"), ("NVL-GO01", "M3"), ("NVL-VIT01", "PCS"), ("NVL-SON01", "L"),
                     ("NVL-KEO01", "KG"), ("NVL-KEO02", "KG"), ("NVL-THUNG01", "PCS"), ("NVL-XOP01", "PCS"), ("PP-MUN01", "KG") })
        {
            var p = new ProductInfo(Guid.NewGuid(), "T-" + sku, "Product " + sku, true, _units[unit]);
            _f.Products[p.Id] = p;
        }

        var user = new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage, PermissionConstants.Manufacturing_BomApprove, PermissionConstants.Manufacturing_RoutingApprove);
        var services = new ServiceCollection();
        services.AddManufacturingApplication();
        services.AddMemoryCache();
        services.AddSingleton<IUserContextService>(user);
        services.AddSingleton(_f.PlantRepo); services.AddSingleton(_f.SettingRepo); services.AddSingleton(_f.ProfileRepo); services.AddSingleton(_f.AccessRepo);
        services.AddSingleton(_f.Uow); services.AddSingleton(_f.BomRepo); services.AddSingleton(_f.RoutingRepo); services.AddSingleton(_f.PlanRepo); services.AddSingleton(_f.UnitClient); services.AddSingleton(_f.ConversionClient);
        services.AddSingleton(_f.WarehouseClient); services.AddSingleton(_f.ProductClient); services.AddSingleton(_f.Guard); services.AddSingleton(_f.Catalog); services.AddSingleton(_f.CalendarRepo);
        services.AddSingleton(_f.CrewRepo); services.AddSingleton(_f.ReasonRepo); services.AddSingleton(_f.References); services.AddSingleton(_f.EmployeeClient);
        services.AddSingleton<IWorkingTimeService>(_f.WorkingTime);
        services.AddSingleton<IProductionSettingProvider>(new FixedSettings(20));
        services.AddScoped<IPlantAccessService, PlantAccessService>();
        _sp = services.BuildServiceProvider();
    }

    private Task<Ds01SeedResult> RunAsync(Ds01Warehouses? wh = null)
        => _sp.GetRequiredService<IMediator>().Send(new SeedDs01Command("PL01", "T-", wh ?? new Ds01Warehouses(_f.AddWarehouse("RM"), _f.AddWarehouse("FG"), _f.AddWarehouse("QC"), _f.AddWarehouse("SC")), null));

    [Fact]
    public async Task Seeds_the_engineering_part_of_DS01_and_is_idempotent()
    {
        var first = await RunAsync();
        first.Created["shift"].Should().Be(3);
        first.Created["workCenter"].Should().Be(4);
        first.Created["machine"].Should().Be(4);
        first.Created["bom"].Should().Be(4);
        first.Created["routing"].Should().Be(2);
        _f.Boms.Should().HaveCount(4).And.OnlyContain(b => b.Status == VersionStatus.Approved);
        _f.Boms.Single(b => b.ItemCode == "T-TP-BAN01").Lines.Should().HaveCount(7);
        _f.Routings.Single(r => r.ItemCode == "T-TP-BAN01").Operations.Should().HaveCount(3);
        _f.Of<Machine>().Single(m => m.Code == "M-OLD-01").Status.Should().Be(MachineStatus.Retired);
        _f.Profiles.Should().HaveCount(12);

        var second = await RunAsync(null);
        second.Created.Should().NotContainKey("bom").And.NotContainKey("routing").And.NotContainKey("workCenter");
        second.Skipped["bom"].Should().Be(4);
        _f.Boms.Should().HaveCount(4);
    }

    [Fact]
    public async Task Seeded_BOM_explodes_to_the_DS01_numbers()
    {
        await RunAsync();
        var top = _f.Boms.Single(b => b.ItemCode == "T-TP-BAN01");
        var lines = await _sp.GetRequiredService<IMediator>().Send(new ExplodeBomQuery(top.PlantId, top.ItemId, null, 100, null));
        lines.Requirements.Should().HaveCount(7);
        lines.Requirements.Single(c => c.ComponentCode == "T-NVL-SON01").Quantity.Should().Be(52.50m);
    }

    [Fact]
    public async Task Missing_product_is_named_and_nothing_is_created()
    {
        _f.Products.Remove(_f.Products.Values.First(p => p.Sku == "T-NVL-XOP01").Id);
        var act = () => RunAsync();
        (await act.Should().ThrowAsync<ERP.Shared.Infrastructure.Exceptions.BusinessRuleException>()).WithMessage("*T-NVL-XOP01*");
        _f.Plants.Should().HaveCount(0);
    }
}
