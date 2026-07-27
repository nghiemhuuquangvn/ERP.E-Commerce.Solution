using MediatR;

namespace ERP.PO.Application.Features.PurchaseRequests;

public class DismissPurchaseRequestCommand : IRequest<PurchaseRequestDto>
{
    public Guid PurchaseRequestId { get; set; }
}

public class DismissPurchaseRequestCommandHandler : IRequestHandler<DismissPurchaseRequestCommand, PurchaseRequestDto>
{
    private readonly IPurchaseRequestRepository _repository;

    public DismissPurchaseRequestCommandHandler(IPurchaseRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PurchaseRequestDto> Handle(DismissPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await _repository.GetByIdAsync(request.PurchaseRequestId, cancellationToken)
            ?? throw new InvalidOperationException($"Purchase request {request.PurchaseRequestId} not found");

        purchaseRequest.Dismiss();
        await _repository.UpdateAsync(purchaseRequest, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return PurchaseRequestDto.Map(purchaseRequest);
    }
}

public class ConvertPurchaseRequestCommand : IRequest<PurchaseRequestDto>
{
    public Guid PurchaseRequestId { get; set; }
}

/// <summary>
/// Marks a suggestion as acted-upon — deliberately does NOT auto-create a real
/// PurchaseOrder or pick a supplier. A Purchasing user still uses the existing
/// CreatePurchaseOrderCommand flow themselves; this just records that the suggestion
/// led to a real order rather than being dismissed.
/// </summary>
public class ConvertPurchaseRequestCommandHandler : IRequestHandler<ConvertPurchaseRequestCommand, PurchaseRequestDto>
{
    private readonly IPurchaseRequestRepository _repository;

    public ConvertPurchaseRequestCommandHandler(IPurchaseRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PurchaseRequestDto> Handle(ConvertPurchaseRequestCommand request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await _repository.GetByIdAsync(request.PurchaseRequestId, cancellationToken)
            ?? throw new InvalidOperationException($"Purchase request {request.PurchaseRequestId} not found");

        purchaseRequest.Convert();
        await _repository.UpdateAsync(purchaseRequest, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return PurchaseRequestDto.Map(purchaseRequest);
    }
}
