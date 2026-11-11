using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MFG.Infrastructure.Repositories;

public sealed class StockDocumentRepository(MfgDbContext db) : IStockDocumentRepository
{
    private IQueryable<MaterialTransaction> WithLines => db.MaterialTransactions.Include(d => d.AllLines.Where(l => !l.IsDeleted)).ThenInclude(l => l.AllLots.Where(x => !x.IsDeleted)).AsSplitQuery();

    public Task<MaterialTransaction?> GetAsync(Guid id, CancellationToken ct = default)
        => WithLines.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

    public Task<MaterialTransaction?> FindByKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => WithLines.FirstOrDefaultAsync(d => d.IdempotencyKey == idempotencyKey && !d.IsDeleted, ct);

    public async Task<(List<MaterialTransaction> Items, int Total)> ListAsync(StockDocumentFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.MaterialTransactions.AsNoTracking().Where(d => d.PlantId == f.PlantId && !d.IsDeleted);
        if (f.Status is { } status) query = query.Where(d => d.Status == status);
        if (f.Type is { } type) query = query.Where(d => d.Type == type);
        if (f.OrderId is { } order) query = query.Where(d => d.OrderId == order);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(d => d.DocNo.ToLower().Contains(term) || d.OrderNo.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query.Include(d => d.AllLines.Where(l => !l.IsDeleted)).ThenInclude(l => l.AllLots.Where(x => !x.IsDeleted)).AsSplitQuery()
            .OrderByDescending(d => d.CreatedAt).ThenBy(d => d.DocNo).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<List<MaterialTransaction>> ListForOrderAsync(Guid orderId, CancellationToken ct = default)
        => db.MaterialTransactions.AsNoTracking().Where(d => d.OrderId == orderId && !d.IsDeleted)
            .Include(d => d.AllLines.Where(l => !l.IsDeleted)).ThenInclude(l => l.AllLots.Where(x => !x.IsDeleted)).AsSplitQuery().OrderBy(d => d.CreatedAt).ThenBy(d => d.DocNo).ToListAsync(ct);

    public async Task<List<StockFlowLine>> FlowsAsync(Guid orderId, CancellationToken ct = default)
    {
        var rows = await db.MaterialTransactions.AsNoTracking()
            .Where(d => d.OrderId == orderId && !d.IsDeleted && (d.Status == StockDocumentStatus.Pending || d.Status == StockDocumentStatus.Posted))
            .SelectMany(d => d.AllLines.Where(l => !l.IsDeleted && (l.Status == StockLineStatus.Pending || l.Status == StockLineStatus.Posted)),
                (d, l) => new { l.OrderMaterialId, d.Type, DocumentStatus = d.Status, DocumentMode = d.Mode, LineStatus = l.Status, l.Qty, Lot = l.PostedLotId ?? l.LotId, l.UnitCost, l.Kind, l.OrderOutputId })
            .ToListAsync(ct);
        return rows.Select(r => new StockFlowLine(r.OrderMaterialId, r.Type, r.DocumentStatus, r.DocumentMode, r.LineStatus, r.Qty, r.Lot, r.UnitCost, r.Kind, r.OrderOutputId)).ToList();
    }

    public Task<List<string>> LotNumbersInUseAsync(Guid itemId, CancellationToken ct = default)
        => db.MaterialTransactions.Where(d => !d.IsDeleted && (d.Status == StockDocumentStatus.Pending || d.Status == StockDocumentStatus.Posted))
            .SelectMany(d => d.AllLines.Where(l => !l.IsDeleted && l.Kind == StockLineKind.Receipt && l.ItemId == itemId && l.LotNumber != null
                && (l.Status == StockLineStatus.Pending || l.Status == StockLineStatus.Posted)).Select(l => l.LotNumber!)).ToListAsync(ct);

    public Task<int> CountHeldLotsAsync(Guid orderId, CancellationToken ct = default)
        => db.MaterialTransactions.Where(d => d.OrderId == orderId && !d.IsDeleted && d.Status == StockDocumentStatus.Posted && !d.IsReversed)
            .SelectMany(d => d.AllLines.Where(l => !l.IsDeleted && l.Kind == StockLineKind.Receipt && l.IsQcHold && l.QcReleasedAt == null && l.Status == StockLineStatus.Posted)).CountAsync(ct);

    public Task<int> CountPendingAsync(Guid orderId, CancellationToken ct = default)
        => db.MaterialTransactions.CountAsync(d => d.OrderId == orderId && !d.IsDeleted && d.Status == StockDocumentStatus.Pending, ct);

    public Task<int> CountPendingReversalsAsync(Guid orderId, CancellationToken ct = default)
        => db.MaterialTransactions.CountAsync(d => d.OrderId == orderId && !d.IsDeleted && d.Type == StockDocumentType.Reversal && d.Status == StockDocumentStatus.Pending, ct);

    public Task<MaterialTransaction?> FindReversalOfAsync(Guid originalDocumentId, CancellationToken ct = default)
        => WithLines.FirstOrDefaultAsync(d => d.ReversalOfId == originalDocumentId && d.Type == StockDocumentType.Reversal && !d.IsDeleted && d.Status != StockDocumentStatus.Failed, ct);

    // across tenants on purpose (no user, no tenant scope): only ids leave this method, each document is then processed inside its own tenant scope
    public async Task<List<(Guid TenantId, Guid DocumentId)>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default)
    {
        var rows = await db.MaterialTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(d => !d.IsDeleted && d.Status == StockDocumentStatus.Pending && (d.NextAttemptAt == null || d.NextAttemptAt <= nowUtc))
            .OrderBy(d => d.NextAttemptAt).Take(take)
            .Select(d => new { d.TenantId, d.Id }).ToListAsync(ct);
        return rows.Select(r => (r.TenantId, r.Id)).ToList();
    }

    public void Add(MaterialTransaction document) => db.MaterialTransactions.Add(document);
}
