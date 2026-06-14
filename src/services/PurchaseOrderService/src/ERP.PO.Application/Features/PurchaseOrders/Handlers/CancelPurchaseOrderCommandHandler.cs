using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Commands;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class CancelPurchaseOrderCommandHandler : IRequestHandler<CancelPurchaseOrderCommand, Unit>
{
    public CancelPurchaseOrderCommandHandler()
    {
    }

    public async Task<Unit> Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Unit.Value;
    }
}
