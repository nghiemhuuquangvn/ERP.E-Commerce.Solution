using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Commands;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class ApprovePurchaseOrderCommandHandler : IRequestHandler<ApprovePurchaseOrderCommand, Unit>
{
    public ApprovePurchaseOrderCommandHandler()
    {
    }

    public async Task<Unit> Handle(ApprovePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Unit.Value;
    }
}
