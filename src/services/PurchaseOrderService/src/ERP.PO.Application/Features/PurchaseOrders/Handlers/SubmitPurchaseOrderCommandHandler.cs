using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Commands;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class SubmitPurchaseOrderCommandHandler : IRequestHandler<SubmitPurchaseOrderCommand, Unit>
{
    public SubmitPurchaseOrderCommandHandler()
    {
    }

    public async Task<Unit> Handle(SubmitPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Unit.Value;
    }
}
