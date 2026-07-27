using ERP.PO.Application.Features.PurchaseRequests;
using ERP.PO.Domain.Entities;
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
