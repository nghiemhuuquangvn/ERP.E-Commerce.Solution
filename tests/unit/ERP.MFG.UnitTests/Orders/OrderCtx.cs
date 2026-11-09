using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Services;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Boms;

namespace ERP.MFG.UnitTests.Orders;

/// <summary>A clock the test sets; the default is 08:00 plant time (UTC+7) on Wednesday 2026-11-04, the "today" of the test-case book.</summary>
internal sealed class FakeTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 4, 1, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class OrderRepoFake : IProductionOrderRepository
{
    public readonly List<ProductionOrder> Orders = [];

    public Task<ProductionOrder?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Orders.FirstOrDefault(o => o.Id == id && !o.IsDeleted));

    public Task<(List<ProductionOrder> Items, int Total)> ListAsync(OrderFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Orders.Where(o => o.PlantId == f.PlantId && !o.IsDeleted
            && (f.Status == null || o.Status == f.Status) && (f.ItemId == null || o.ItemId == f.ItemId)
            && (f.DueFrom == null || o.DueDate >= f.DueFrom) && (f.DueTo == null || o.DueDate <= f.DueTo)
            && (f.IsLate == null || o.IsLate == f.IsLate) && (f.Priority == null || o.Priority == f.Priority)
            && (string.IsNullOrWhiteSpace(f.Search) || o.OrderNo.Contains(f.Search, StringComparison.OrdinalIgnoreCase) || o.ItemCode.Contains(f.Search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(o => o.DueDate).ThenBy(o => o.Priority).ThenBy(o => o.OrderNo).ToList();
        return Task.FromResult((query.Skip((page - 1) * pageSize).Take(pageSize).ToList(), query.Count));
    }

    public void Add(ProductionOrder order) => Orders.Add(order);
}

/// <summary>Hands out MO-yyMM-00001 ... per document type, plant and month like the database counter.</summary>
internal sealed class DocNumberFake : IDocumentNumberService
{
    private readonly Dictionary<(string, Guid, int), long> _last = [];

    public Task<string> NextAsync(string docType, Guid plantId, DateTime businessDate, CancellationToken ct = default)
    {
        var key = (docType, plantId, businessDate.Year * 100 + businessDate.Month);
        _last[key] = _last.GetValueOrDefault(key) + 1;
        return Task.FromResult(DocumentTypes.Format(docType, businessDate, _last[key]));
    }
}

/// <summary>DS-01 with routings, the plant calendar and the order handlers on in-memory stores (test cases §1, §3 TC-PO / TC-SCH).</summary>
internal class OrderCtx : RoutingCtx
{
    public FakeTime Clock { get; } = new();
    public PlantClock PlantTime { get; }
    public OrderRepoFake Repo { get; } = new();
    public DocNumberFake Numbers { get; } = new();
    public Dictionary<string, Guid> BomIds { get; private set; } = [];

    public OrderCtx()
    {
        PlantTime = new PlantClock(Clock);
        Plant.Update(Plant.Name, null, Plant.RawMaterialWarehouseId, Plant.FinishedGoodsWarehouseId, Plant.QuarantineWarehouseId, null, D.Cal5D.Id);
    }

    /// <summary>The engineering data of DS-01: items, approved BOMs, and the approved routing of TP-BAN01 (10 Assembly, 20 Painting, 30 Packing).</summary>
    public async Task SeedAllAsync()
    {
        BomIds = await SeedBomsAsync();
        await SeedTopRoutingAsync();
    }

    public OrderSnapshotBuilder Builder(IPlantAccessService? access = null) => new(F.ProductClient, F.UnitClient, F.ProfileRepo, F.BomRepo, new BomResolver(F.BomRepo), Explosion,
        F.RoutingRepo, new RoutingResolver(F.RoutingRepo), Time, F.WorkingTime, PlantTime);

    public CreateOrderHandler CreateHandler(IPlantAccessService? access = null) => new(access ?? Access, Builder(), Repo, Numbers, PlantTime, User, F.Uow);
    public UpdateOrderHandler UpdateHandler(IPlantAccessService? access = null) => new(access ?? Access, Builder(), Repo, PlantTime, User, F.Uow);

    public Task<OrderDto> CreateOrderAsync(string item = "TP-BAN01", decimal quantity = 100, DateOnly? due = null, int? priority = null, Guid? bomId = null, Guid? routingId = null,
        IPlantAccessService? access = null)
        => CreateHandler(access).Handle(new CreateOrderCommand(Plant.Id, Items[item].Id, quantity, due ?? new DateOnly(2026, 11, 13), priority, bomId, routingId, null, null), default);

    public Task<OrderDto> UpdateOrderAsync(Guid id, decimal quantity, DateOnly due, int? priority = null, Guid? bomId = null)
        => UpdateHandler().Handle(new UpdateOrderCommand(id, quantity, due, priority, bomId, null, null, null), default);

    public ProductionOrder Stored(Guid id) => Repo.Orders.Single(o => o.Id == id);

    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(PlantTime.ToLocal(Plant, utc));
    public DateTime Local(DateTime utc) => PlantTime.ToLocal(Plant, utc);

    /// <summary>Gives an item a profile with a fixed lead time (the item has no routing).</summary>
    public void SetLeadTime(string code, int days)
    {
        var item = Items[code];
        F.Profiles.RemoveAll(p => p.ItemId == item.Id);
        F.Profiles.Add(ItemManufacturingProfile.Create(item.Id, Plant.Id, item.Sku, item.Name, ItemManufacturingProfile.DefaultValues with { FixedLeadTimeDays = days }));
    }

    public void Replace(string code, Func<ERP.MFG.Application.Abstractions.ProductInfo, ERP.MFG.Application.Abstractions.ProductInfo> change)
    {
        var updated = change(Items[code]);
        Items[code] = updated;
        F.Products[updated.Id] = updated;
    }
}
