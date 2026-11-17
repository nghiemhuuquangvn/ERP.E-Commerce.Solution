using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MFG.Infrastructure.Repositories;

public sealed class NcrRepository(MfgDbContext db) : INcrRepository
{
    private IQueryable<NonConformance> Full => db.NonConformances.Include(n => n.AllDispositions).Where(n => !n.IsDeleted);

    public Task<NonConformance?> GetAsync(Guid id, CancellationToken ct = default) => Full.FirstOrDefaultAsync(n => n.Id == id, ct);

    public Task<NonConformance?> GetByInspectionAsync(Guid inspectionId, CancellationToken ct = default) => Full.FirstOrDefaultAsync(n => n.InspectionId == inspectionId, ct);

    public Task<NonConformance?> GetByReworkOrderAsync(Guid orderId, CancellationToken ct = default)
        => Full.FirstOrDefaultAsync(n => n.AllDispositions.Any(d => !d.IsDeleted && d.ReworkOrderId == orderId), ct);

    public Task<List<NonConformance>> ListByInspectionsAsync(IReadOnlyCollection<Guid> inspectionIds, CancellationToken ct = default)
        => db.NonConformances.Where(n => !n.IsDeleted && n.InspectionId != null && inspectionIds.Contains(n.InspectionId.Value)).ToListAsync(ct);

    public async Task<(List<NonConformance> Items, int Total)> ListAsync(NcrFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        var q = db.NonConformances.Where(n => !n.IsDeleted && n.PlantId == f.PlantId && (f.Status == null || n.Status == f.Status) && (f.Source == null || n.SourceType == f.Source)
            && (f.OrderId == null || n.OrderId == f.OrderId) && (!f.OpenOnly || n.Status == NcrStatus.Open || n.Status == NcrStatus.InDisposition));
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            q = q.Where(n => n.NcrNo.ToLower().Contains(term) || n.OrderNo.ToLower().Contains(term) || n.ItemCode.ToLower().Contains(term) || (n.LotNumber != null && n.LotNumber.ToLower().Contains(term)));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<List<NonConformance>> ListForOrderAsync(Guid orderId, CancellationToken ct = default)
        => db.NonConformances.Where(n => !n.IsDeleted && n.OrderId == orderId).OrderByDescending(n => n.CreatedAt).ToListAsync(ct);

    public Task<int> CountOpenForOrderAsync(Guid orderId, CancellationToken ct = default)
        => db.NonConformances.CountAsync(n => !n.IsDeleted && n.OrderId == orderId && (n.Status == NcrStatus.Open || n.Status == NcrStatus.InDisposition), ct);

    public void Add(NonConformance ncr) => db.NonConformances.Add(ncr);
}
