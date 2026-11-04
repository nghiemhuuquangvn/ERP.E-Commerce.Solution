using ERP.PO.Application.Features.PurchaseOrders;
using ERP.PO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.PO.Infrastructure.Data;

/// <summary>The purchase order list over the database: filter, count and one page in SQL, newest order date first.</summary>
public class PurchaseOrderSearch : IPurchaseOrderSearch
{
    private readonly PurchasingDbContext _context;

    public PurchaseOrderSearch(PurchasingDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int Total)> SearchAsync(PurchaseOrderFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var matching = filter.Apply(_context.PurchaseOrders.AsNoTracking());
        var total = await matching.CountAsync(cancellationToken);
        var items = await matching
            .OrderByDescending(po => po.OrderDate).ThenByDescending(po => po.PONumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
