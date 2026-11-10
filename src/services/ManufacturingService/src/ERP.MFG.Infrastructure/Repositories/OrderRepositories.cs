using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MFG.Infrastructure.Repositories;

public sealed class ProductionOrderRepository(MfgDbContext db) : IProductionOrderRepository
{
    public Task<ProductionOrder?> GetAsync(Guid id, CancellationToken ct = default)
        => db.ProductionOrders
            .Include(o => o.AllMaterials.Where(m => !m.IsDeleted))
            .Include(o => o.AllOperations.Where(x => !x.IsDeleted))
            .Include(o => o.AllOutputs.Where(x => !x.IsDeleted))
            .Include(o => o.AllHistory)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted, ct);

    public async Task<(List<ProductionOrder> Items, int Total)> ListAsync(OrderFilter f, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.ProductionOrders.AsNoTracking().Where(o => o.PlantId == f.PlantId && !o.IsDeleted);
        if (f.Status is { } status) query = query.Where(o => o.Status == status);
        if (f.ItemId is { } item) query = query.Where(o => o.ItemId == item);
        if (f.DueFrom is { } from) query = query.Where(o => o.DueDate >= from);
        if (f.DueTo is { } to) query = query.Where(o => o.DueDate <= to);
        if (f.IsLate is { } late) query = query.Where(o => o.IsLate == late);
        if (f.Priority is { } priority) query = query.Where(o => o.Priority == priority);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(o => o.OrderNo.ToLower().Contains(term) || o.ItemCode.ToLower().Contains(term) || o.ItemName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(o => o.DueDate).ThenBy(o => o.Priority).ThenBy(o => o.OrderNo)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public void Add(ProductionOrder order) => db.ProductionOrders.Add(order);

    // across tenants on purpose (no user, no tenant scope): only ids leave this method, each order is then processed inside its own tenant scope
    public async Task<List<(Guid TenantId, Guid OrderId)>> ListPendingReservationSyncAsync(DateTime nowUtc, int take, CancellationToken ct = default)
    {
        var rows = await db.ProductionOrders.IgnoreQueryFilters().AsNoTracking()
            .Where(o => !o.IsDeleted && o.ReservationSyncPending && (o.ReservationNextAttemptAt == null || o.ReservationNextAttemptAt <= nowUtc))
            .OrderBy(o => o.ReservationNextAttemptAt).Take(take)
            .Select(o => new { o.TenantId, o.Id }).ToListAsync(ct);
        return rows.Select(r => (r.TenantId, r.Id)).ToList();
    }
}

/// <summary>
/// BR-SET-03 / BR-WC-03 over the real orders. An order that was released and is not closed or cancelled is "open": Released, InProgress, OnHold (it was
/// released) and Completed (not yet closed). A Draft blocks nothing.
/// </summary>
public sealed class OrderBackedOpenOrderGuard(MfgDbContext db) : IOpenOrderGuard
{
    public Task<bool> HasOpenOrdersAsync(Guid plantId, CancellationToken ct = default)
        => db.ProductionOrders.AnyAsync(o => o.PlantId == plantId && !o.IsDeleted
            && (o.Status == OrderStatus.Released || o.Status == OrderStatus.InProgress || o.Status == OrderStatus.OnHold || o.Status == OrderStatus.Completed), ct);

    public Task<bool> HasOpenOperationsAsync(Guid workCenterId, CancellationToken ct = default)
        => db.ProductionOrderOperations.AnyAsync(op => op.WorkCenterId == workCenterId && !op.IsDeleted && op.Status != OperationStatus.Completed
            && db.ProductionOrders.Any(o => o.Id == op.OrderId && !o.IsDeleted
                && (o.Status == OrderStatus.Released || o.Status == OrderStatus.InProgress || o.Status == OrderStatus.OnHold)), ct);
}
