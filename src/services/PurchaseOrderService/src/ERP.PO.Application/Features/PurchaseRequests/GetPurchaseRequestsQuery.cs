using MediatR;
using ERP.PO.Domain.Entities;

namespace ERP.PO.Application.Features.PurchaseRequests;

public class GetPurchaseRequestsQuery : IRequest<List<PurchaseRequestDto>>
{
    public PurchaseRequestStatus? Status { get; set; }
}

public class GetPurchaseRequestsQueryHandler : IRequestHandler<GetPurchaseRequestsQuery, List<PurchaseRequestDto>>
{
    private readonly IPurchaseRequestRepository _repository;

    public GetPurchaseRequestsQueryHandler(IPurchaseRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PurchaseRequestDto>> Handle(GetPurchaseRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = await _repository.ListAsync(request.Status, cancellationToken);
        return requests.Select(PurchaseRequestDto.Map).ToList();
    }
}
