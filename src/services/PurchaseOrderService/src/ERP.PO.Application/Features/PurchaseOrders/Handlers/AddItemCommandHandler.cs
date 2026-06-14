using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class AddItemCommandHandler : IRequestHandler<AddItemCommand, PurchaseOrderDto>
{
    private readonly IMapper _mapper;

    public AddItemCommandHandler(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<PurchaseOrderDto> Handle(AddItemCommand request, CancellationToken cancellationToken)
    {
        var item = PurchaseOrderItem.Create(
            request.PurchaseOrderId,
            request.ProductCode,
            request.ProductName,
            request.Quantity,
            request.Unit,
            new Money(request.UnitPrice, "USD"));

        await Task.CompletedTask;

        return _mapper.Map<PurchaseOrderDto>(new object());
    }
}
