using MediatR;

namespace ERP.PO.Application.Features.PurchaseRequests;

/// <summary>One preferred supplier per product: marking an item preferred clears the flag on the product's other items.</summary>
public class SetPreferredSupplierItemCommand : IRequest<PreferredSupplierItemResult>
{
    public Guid SupplierId { get; set; }
    public Guid SupplierItemId { get; set; }
    public bool Preferred { get; set; } = true;
}

public record PreferredSupplierItemResult(Guid SupplierItemId, Guid ProductId, bool IsPreferred);

public class SetPreferredSupplierItemCommandHandler : IRequestHandler<SetPreferredSupplierItemCommand, PreferredSupplierItemResult>
{
    private readonly IPurchaseRequestRepository _repository;

    public SetPreferredSupplierItemCommandHandler(IPurchaseRequestRepository repository) => _repository = repository;

    public async Task<PreferredSupplierItemResult> Handle(SetPreferredSupplierItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _repository.GetSupplierItemAsync(request.SupplierId, request.SupplierItemId, cancellationToken)
            ?? throw new KeyNotFoundException($"SupplierItem {request.SupplierItemId} not found for supplier {request.SupplierId}");

        if (request.Preferred)
            foreach (var other in await _repository.GetSupplierItemsForProductAsync(item.ProductId, cancellationToken))
                if (other.Id != item.Id && other.IsPreferred) other.SetPreferred(false);

        item.SetPreferred(request.Preferred);
        await _repository.SaveChangesAsync(cancellationToken);
        return new PreferredSupplierItemResult(item.Id, item.ProductId, item.IsPreferred);
    }
}
