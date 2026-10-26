using ERP.PO.Domain.Entities;

namespace ERP.PO.Application.Features.PurchaseRequests;

public interface IPurchaseRequestRepository
{
    Task<PurchaseRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PurchaseRequest?> GetActiveByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<List<PurchaseRequest>> ListAsync(PurchaseRequestStatus? status, CancellationToken cancellationToken);
    Task<List<PurchaseRequest>> GetOpenByIdsAsync(IReadOnlyCollection<Guid>? ids, CancellationToken cancellationToken);
    /// <summary>Open quantity (ordered - received) of the product on orders that are neither finished nor cancelled; drafts count so a second request is not made for goods already being ordered.</summary>
    Task<int> GetOnOrderQuantityAsync(string productCode, CancellationToken cancellationToken);
    Task<SupplierItem?> GetPreferredSupplierItemAsync(Guid productId, CancellationToken cancellationToken);
    /// <summary>Active suppliers that have a supplier item for any of the products.</summary>
    Task<List<Guid>> GetSupplierIdsForProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
    Task<List<Guid>> GetActiveSupplierIdsAsync(CancellationToken cancellationToken);
    Task<SupplierItem?> GetSupplierItemAsync(Guid supplierId, Guid itemId, CancellationToken cancellationToken);
    Task<List<SupplierItem>> GetSupplierItemsForProductAsync(Guid productId, CancellationToken cancellationToken);
    Task AddAsync(PurchaseRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(PurchaseRequest request, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
