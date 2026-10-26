using MediatR;
using ERP.PO.Application.Procurement;
using ERP.PO.Domain.Entities;

namespace ERP.PO.Application.Features.PurchaseRequests;

public class GetPurchaseRequestsQuery : IRequest<List<PurchaseRequestDto>>
{
    public PurchaseRequestStatus? Status { get; set; }
}

public class GetPurchaseRequestsQueryHandler : IRequestHandler<GetPurchaseRequestsQuery, List<PurchaseRequestDto>>
{
    private readonly IPurchaseRequestRepository _repository;
    private readonly IProcurementStore _store;

    public GetPurchaseRequestsQueryHandler(IPurchaseRequestRepository repository, IProcurementStore store)
    {
        _repository = repository;
        _store = store;
    }

    public async Task<List<PurchaseRequestDto>> Handle(GetPurchaseRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = await _repository.ListAsync(request.Status, cancellationToken);
        var names = await _store.GetSupplierNamesAsync(requests.Where(r => r.PreferredSupplierId != null).Select(r => r.PreferredSupplierId!.Value), cancellationToken);

        return requests.Select(r =>
        {
            var dto = PurchaseRequestDto.Map(r);
            if (r.PreferredSupplierId != null && names.TryGetValue(r.PreferredSupplierId.Value, out var name)) dto.PreferredSupplierName = name;
            return dto;
        }).ToList();
    }
}
