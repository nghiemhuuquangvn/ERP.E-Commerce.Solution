using ERP.PO.Application.Features.PurchaseRequests;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ERP.PO.Infrastructure.Data;

public class PurchaseRequestRepository : IPurchaseRequestRepository
{
    private readonly PurchasingDbContext _context;

    public PurchaseRequestRepository(PurchasingDbContext context)
    {
        _context = context;
    }

    public async Task<PurchaseRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.PurchaseRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PurchaseRequest?> GetActiveByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        return await _context.PurchaseRequests
            .FirstOrDefaultAsync(r => r.ProductId == productId && r.Status == PurchaseRequestStatus.Suggested, cancellationToken);
    }

    public async Task<List<PurchaseRequest>> GetOpenByIdsAsync(IReadOnlyCollection<Guid>? ids, CancellationToken cancellationToken)
    {
        var query = _context.PurchaseRequests.Where(r => r.Status == PurchaseRequestStatus.Suggested);
        if (ids != null) query = query.Where(r => ids.Contains(r.Id));
        return await query.OrderBy(r => r.ProductName).ToListAsync(cancellationToken);
    }

    private static readonly PurchaseOrderStatus[] OpenOrderStatuses =
    {
        PurchaseOrderStatus.Draft, PurchaseOrderStatus.Submitted, PurchaseOrderStatus.PendingFinanceApproval,
        PurchaseOrderStatus.Approved, PurchaseOrderStatus.Ordered, PurchaseOrderStatus.PartiallyReceived
    };

    public async Task<int> GetOnOrderQuantityAsync(string productCode, CancellationToken cancellationToken)
    {
        var open = await _context.PurchaseOrders
            .Where(o => !o.IsDeleted && OpenOrderStatuses.Contains(o.Status))
            .SelectMany(o => o.Items)
            .Where(i => i.ProductCode == productCode && i.Status != PurchaseOrderItemStatus.Cancelled)
            .Select(i => i.Quantity - i.ReceivedQuantity)
            .ToListAsync(cancellationToken);
        return (int)Math.Ceiling(open.Where(q => q > 0).Sum());
    }

    public async Task<SupplierItem?> GetPreferredSupplierItemAsync(Guid productId, CancellationToken cancellationToken)
    {
        var activeSuppliers = _context.Suppliers.Where(s => !s.IsDeleted && s.IsActive).Select(s => s.Id);
        return await _context.SupplierItems
            .Where(i => !i.IsDeleted && i.IsPreferred && i.ProductId == productId && activeSuppliers.Contains(i.SupplierId))
            .OrderByDescending(i => i.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetSupplierIdsForProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var activeSuppliers = _context.Suppliers.Where(s => !s.IsDeleted && s.IsActive).Select(s => s.Id);
        return await _context.SupplierItems
            .Where(i => !i.IsDeleted && productIds.Contains(i.ProductId) && activeSuppliers.Contains(i.SupplierId))
            .Select(i => i.SupplierId).Distinct().ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetActiveSupplierIdsAsync(CancellationToken cancellationToken) =>
        await _context.Suppliers.Where(s => !s.IsDeleted && s.IsActive).OrderBy(s => s.Name).Select(s => s.Id).ToListAsync(cancellationToken);

    public Task<SupplierItem?> GetSupplierItemAsync(Guid supplierId, Guid itemId, CancellationToken cancellationToken) =>
        _context.SupplierItems.FirstOrDefaultAsync(i => i.Id == itemId && i.SupplierId == supplierId && !i.IsDeleted, cancellationToken);

    public Task<List<SupplierItem>> GetSupplierItemsForProductAsync(Guid productId, CancellationToken cancellationToken) =>
        _context.SupplierItems.Where(i => i.ProductId == productId && !i.IsDeleted).ToListAsync(cancellationToken);

    public async Task<List<PurchaseRequest>> ListAsync(PurchaseRequestStatus? status, CancellationToken cancellationToken)
    {
        var query = _context.PurchaseRequests.AsQueryable();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PurchaseRequest request, CancellationToken cancellationToken)
    {
        await _context.PurchaseRequests.AddAsync(request, cancellationToken);
    }

    public async Task UpdateAsync(PurchaseRequest request, CancellationToken cancellationToken)
    {
        _context.PurchaseRequests.Update(request);
        await Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
