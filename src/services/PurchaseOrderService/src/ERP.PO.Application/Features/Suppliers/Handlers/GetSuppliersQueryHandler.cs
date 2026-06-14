using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Queries;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, IEnumerable<SupplierDto>>
{
    private readonly IMapper _mapper;

    public GetSuppliersQueryHandler(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<IEnumerable<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new List<SupplierDto>();
    }
}
