using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Boms;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Services;
using ERP.MFG.UnitTests.Common;
using ERP.Shared.Authorization;

namespace ERP.MFG.UnitTests.Boms;

internal sealed class FixedSettings(int maxBomLevels) : IProductionSettingProvider
{
    public Task<ProductionSettingValues> GetAsync(Guid plantId, CancellationToken ct = default) => Task.FromResult(ProductionSettingValues.Defaults with { MaxBomLevels = maxBomLevels });
    public void Invalidate(Guid plantId) { }
}

/// <summary>
/// The engineering part of the QA data set DS-01 (test cases §1.4-1.5) on the real handlers and calculators with in-memory stores:
/// units, the twelve items, their profiles and the approved BOMs of TP-BAN01, BTP-MAT01, BTP-CHAN01 and KIT-PKG01.
/// </summary>
internal class BomCtx
{
    public static readonly DateOnly Jan1 = new(2026, 1, 1);
    public static readonly DateOnly Nov4 = new(2026, 11, 4);

    public Fakes F { get; } = new();
    public Plant Plant { get; }
    public PlantAccessService Access { get; }
    public IUserContextService User { get; } = new FakeUser(Guid.NewGuid(), PermissionConstants.Manufacturing_SettingsManage);
    public IProductionSettingProvider Settings { get; set; } = new FixedSettings(20);

    public Guid Piece { get; } = Guid.NewGuid();
    public Guid Set { get; } = Guid.NewGuid();
    public Guid M3 { get; } = Guid.NewGuid();
    public Guid Litre { get; } = Guid.NewGuid();
    public Guid Kg { get; } = Guid.NewGuid();
    public Guid Box { get; } = Guid.NewGuid();
    public Guid Gram { get; } = Guid.NewGuid();

    public Dictionary<string, ProductInfo> Items { get; } = [];

    public BomCtx()
    {
        Plant = F.AddPlant("PL01");
        Access = new PlantAccessService(F.PlantRepo, F.AccessRepo, User);
        Unit(Piece, "PCS", 0);
        Unit(Set, "SET", 0);
        Unit(M3, "M3", 3);
        Unit(Litre, "L", 2);
        Unit(Kg, "KG", 2);
        F.Units[Gram] = new UnitInfo(Gram, "G", "Gram", 0, Kg, 0.001m);
        F.Units[Box] = new UnitInfo(Box, "BOX", "Box", 0, Piece, 100m); // 1 box = 100 pieces
    }

    private void Unit(Guid id, string code, int decimals) => F.Units[id] = new UnitInfo(id, code, code, decimals, null, 1m);

    public ProductInfo Item(string code, Guid uom, bool active = true)
    {
        var product = new ProductInfo(Guid.NewGuid(), code, "Product " + code, active, uom);
        F.Products[product.Id] = product;
        Items[code] = product;
        return product;
    }

    public void Profile(string code, MakeOrBuy makeOrBuy)
    {
        var item = Items[code];
        F.Profiles.Add(ItemManufacturingProfile.Create(item.Id, Plant.Id, item.Sku, item.Name, ItemManufacturingProfile.DefaultValues with { MakeOrBuy = makeOrBuy }));
    }

    // ── Handlers wired to the stores ────────────────────────────────────────────────────────────────────────
    public BomSnapshotFactory Snapshots => new(F.ProductClient, F.UnitClient, F.ConversionClient);
    public BomExplosionService Explosion => new(F.BomRepo, Snapshots, Settings);

    public Task<BomDto> CreateAsync(string item, DateOnly? from = null, DateOnly? to = null, decimal? baseQty = null, decimal? yield = null)
        => new CreateBomHandler(Access, F.BomRepo, F.ProfileRepo, F.ProductClient, F.Uow)
            .Handle(new CreateBomCommand(Plant.Id, Items[item].Id, baseQty, yield, from ?? Jan1, to, null), default);

    public static BomLineBody Line(ProductInfo component, decimal quantity, Guid uom, decimal scrap = 0, int? op = null, IssueMethod issue = IssueMethod.Manual, bool phantom = false,
        bool fixedQty = false, string? group = null, int? priority = null, BomLineType type = BomLineType.Component, decimal? allocation = null)
        => new(type, component.Id, quantity, uom, scrap, fixedQty, phantom, issue, op, group, priority, allocation, null);

    public Task<BomDto> AddAsync(Guid bomId, string component, decimal quantity, Guid uom, decimal scrap = 0, int? op = null, IssueMethod issue = IssueMethod.Manual, bool phantom = false,
        bool fixedQty = false, string? group = null, int? priority = null, BomLineType type = BomLineType.Component, decimal? allocation = null)
        => new AddBomLineHandler(Access, F.BomRepo, Snapshots, F.Uow)
            .Handle(new AddBomLineCommand(bomId, Line(Items[component], quantity, uom, scrap, op, issue, phantom, fixedQty, group, priority, type, allocation)), default);

    public Task<BomDto> ApproveAsync(Guid bomId)
        => new ApproveBomHandler(Access, F.BomRepo, F.ProfileRepo, Settings, F.RoutingRepo, User, F.Uow).Handle(new ApproveBomCommand(bomId), default);

    public Task<BomDto> ObsoleteAsync(Guid bomId, DateOnly? to = null)
        => new ObsoleteBomHandler(Access, F.BomRepo, F.ProfileRepo, F.Uow).Handle(new ObsoleteBomCommand(bomId, to), default);

    public Task<BomDto> CopyAsync(Guid bomId, DateOnly from)
        => new CopyBomHandler(Access, F.BomRepo, F.Uow).Handle(new CopyBomCommand(bomId, from), default);

    /// <summary>Creates the DS-01 items and profiles; the BOMs are built by <see cref="SeedBomsAsync"/>.</summary>
    public void SeedItems()
    {
        Item("TP-BAN01", Piece); Item("BTP-MAT01", Piece); Item("BTP-CHAN01", Piece); Item("KIT-PKG01", Set);
        Item("NVL-GO01", M3); Item("NVL-VIT01", Piece); Item("NVL-SON01", Litre); Item("NVL-KEO01", Kg); Item("NVL-KEO02", Kg);
        Item("NVL-THUNG01", Piece); Item("NVL-XOP01", Piece); Item("PP-MUN01", Kg);
        foreach (var made in new[] { "TP-BAN01", "BTP-MAT01", "BTP-CHAN01", "KIT-PKG01" }) Profile(made, MakeOrBuy.Make);
        foreach (var bought in new[] { "NVL-GO01", "NVL-VIT01", "NVL-SON01", "NVL-KEO01", "NVL-KEO02", "NVL-THUNG01", "NVL-XOP01", "PP-MUN01" }) Profile(bought, MakeOrBuy.Buy);
    }

    /// <summary>The approved BOMs of DS-01 §1.5 (effective 2026-01-01 without end). Returns the BOM ids by item code.</summary>
    public async Task<Dictionary<string, Guid>> SeedBomsAsync()
    {
        SeedItems();
        var ids = new Dictionary<string, Guid>();

        var kit = await CreateAsync("KIT-PKG01");
        await AddAsync(kit.Id, "NVL-THUNG01", 1, Piece, issue: IssueMethod.Backflush);
        await AddAsync(kit.Id, "NVL-XOP01", 2, Piece, issue: IssueMethod.Backflush);
        await ApproveAsync(kit.Id);
        ids["KIT-PKG01"] = kit.Id;

        var mat = await CreateAsync("BTP-MAT01");
        await AddAsync(mat.Id, "NVL-GO01", 0.02m, M3, scrap: 10, op: 10);
        await AddAsync(mat.Id, "PP-MUN01", 0.5m, Kg, type: BomLineType.ByProduct);
        await ApproveAsync(mat.Id);
        ids["BTP-MAT01"] = mat.Id;

        var chan = await CreateAsync("BTP-CHAN01");
        await AddAsync(chan.Id, "NVL-GO01", 0.005m, M3, scrap: 10);
        await ApproveAsync(chan.Id);
        ids["BTP-CHAN01"] = chan.Id;

        var top = await CreateAsync("TP-BAN01");
        await AddAsync(top.Id, "BTP-MAT01", 1, Piece, op: 10);
        await AddAsync(top.Id, "BTP-CHAN01", 4, Piece, op: 10);
        await AddAsync(top.Id, "NVL-VIT01", 16, Piece, op: 10, issue: IssueMethod.Backflush);
        await AddAsync(top.Id, "NVL-SON01", 0.5m, Litre, scrap: 5, op: 20);
        await AddAsync(top.Id, "NVL-KEO01", 0.1m, Kg, op: 10, group: "G1", priority: 1);
        await AddAsync(top.Id, "NVL-KEO02", 0.12m, Kg, op: 10, group: "G1", priority: 2);
        await AddAsync(top.Id, "KIT-PKG01", 1, Set, op: 30, phantom: true);
        await ApproveAsync(top.Id);
        ids["TP-BAN01"] = top.Id;
        return ids;
    }
}
