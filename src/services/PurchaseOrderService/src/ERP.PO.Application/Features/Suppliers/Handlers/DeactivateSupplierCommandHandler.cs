using MediatR;
using ERP.PO.Application.Features.Suppliers.Commands;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class DeactivateSupplierCommandHandler : IRequestHandler<DeactivateSupplierCommand, Unit>
{
    public DeactivateSupplierCommandHandler()
    {
    }

    public async Task<Unit> Handle(DeactivateSupplierCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Unit.Value;
    }
}
