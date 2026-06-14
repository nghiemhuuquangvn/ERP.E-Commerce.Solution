using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Queries;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto>
{
    private readonly IMapper _mapper;

    public GetSupplierByIdQueryHandler(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<SupplierDto> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return _mapper.Map<SupplierDto>(new object());
    }
}
