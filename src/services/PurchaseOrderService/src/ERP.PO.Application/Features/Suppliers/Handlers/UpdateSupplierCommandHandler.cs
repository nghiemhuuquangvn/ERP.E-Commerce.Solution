using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Domain.ValueObjects;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, SupplierDto>
{
    private readonly IMapper _mapper;

    public UpdateSupplierCommandHandler(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<SupplierDto> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var address = request.Address != null
            ? new Address(request.Address.Street, request.Address.City, request.Address.State,
                         request.Address.PostalCode, request.Address.Country)
            : null;

        await Task.CompletedTask;

        return _mapper.Map<SupplierDto>(new object());
    }
}
