using ERP.PO.Domain.Entities;

namespace ERP.PO.Application.Features.PurchaseRequests;

public interface IPurchaseRequestRepository
{
    Task<PurchaseRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PurchaseRequest?> GetActiveByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<List<PurchaseRequest>> ListAsync(PurchaseRequestStatus? status, CancellationToken cancellationToken);
    Task AddAsync(PurchaseRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(PurchaseRequest request, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
