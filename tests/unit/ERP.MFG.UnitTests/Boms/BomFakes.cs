using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.UnitTests.Common;

namespace ERP.MFG.UnitTests.Boms;

internal sealed class BomFake(Fakes f) : IBomRepository
{
    public Task<Bom?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Boms.FirstOrDefault(b => b.Id == id && !b.IsDeleted));

    public Task<List<Bom>> ListAsync(Guid plantId, Guid? itemId, VersionStatus? status, CancellationToken ct = default)
        => Task.FromResult(f.Boms.Where(b => !b.IsDeleted && b.PlantId == plantId && (itemId == null || b.ItemId == itemId) && (status == null || b.Status == status)).ToList());

    public Task<List<Bom>> ListApprovedAsync(Guid plantId, CancellationToken ct = default)
        => Task.FromResult(f.Boms.Where(b => !b.IsDeleted && b.PlantId == plantId && b.Status == VersionStatus.Approved).ToList());

    public Task<int> MaxVersionAsync(Guid itemId, Guid plantId, CancellationToken ct = default)
        => Task.FromResult(f.Boms.Where(b => b.ItemId == itemId && b.PlantId == plantId).Select(b => b.Version).DefaultIfEmpty(0).Max());

    public void Add(Bom bom) => f.Boms.Add(bom);
}

internal sealed class RoutingFake(Fakes f) : IRoutingRepository
{
    public Task<Routing?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(f.Routings.FirstOrDefault(r => r.Id == id && !r.IsDeleted));

    public Task<List<Routing>> ListAsync(Guid plantId, Guid? itemId, VersionStatus? status, CancellationToken ct = default)
        => Task.FromResult(f.Routings.Where(r => !r.IsDeleted && r.PlantId == plantId && (itemId == null || r.ItemId == itemId) && (status == null || r.Status == status)).ToList());

    public Task<List<Routing>> ListApprovedAsync(Guid plantId, CancellationToken ct = default)
        => Task.FromResult(f.Routings.Where(r => !r.IsDeleted && r.PlantId == plantId && r.Status == VersionStatus.Approved).ToList());

    public Task<int> MaxVersionAsync(Guid itemId, Guid plantId, CancellationToken ct = default)
        => Task.FromResult(f.Routings.Where(r => r.ItemId == itemId && r.PlantId == plantId).Select(r => r.Version).DefaultIfEmpty(0).Max());

    public Task<bool> WorkCenterIsUsedAsync(Guid workCenterId, CancellationToken ct = default)
        => Task.FromResult(f.Routings.Any(r => !r.IsDeleted && r.Operations.Any(o => o.WorkCenterId == workCenterId)));

    public void Add(Routing routing) => f.Routings.Add(routing);
}

internal sealed class UnitFake(Fakes f) : IUnitOfMeasureClient
{
    public Task<IReadOnlyList<UnitInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnitInfo>>(ids.Where(f.Units.ContainsKey).Select(i => f.Units[i]).ToList());

    public Task<IReadOnlyList<UnitInfo>> LookupByCodeAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnitInfo>>(f.Units.Values.Where(u => codes.Contains(u.Code, StringComparer.OrdinalIgnoreCase)).ToList());

    public Task<ConvertedQuantity?> ConvertAsync(Guid fromUomId, Guid toUomId, decimal quantity, CancellationToken ct = default) => Task.FromResult<ConvertedQuantity?>(null);
}

internal sealed class ConversionFake(Fakes f) : IProductUomConversionClient
{
    public Task<IReadOnlyList<ERP.MFG.Domain.Engineering.ProductConversionFacts>> LookupAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ERP.MFG.Domain.Engineering.ProductConversionFacts>>(f.ProductConversions.Where(c => productIds.Contains(c.ProductId)).ToList());
}
