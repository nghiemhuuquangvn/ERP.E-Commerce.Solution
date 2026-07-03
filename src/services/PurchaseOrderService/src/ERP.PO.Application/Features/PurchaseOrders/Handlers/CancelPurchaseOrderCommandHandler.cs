using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>Was a complete no-op before 2026-07-03 — see ApprovePurchaseOrderCommandHandler's note, same bug.</summary>
public class CancelPurchaseOrderCommandHandler : IRequestHandler<CancelPurchaseOrderCommand, Unit>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;

    public CancelPurchaseOrderCommandHandler(IRepository<PurchaseOrder> repository, IUserContextService userContextService)
    {
        _repository = repository;
        _userContextService = userContextService;
    }

    public async Task<Unit> Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _repository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        var cancelledBy = string.IsNullOrWhiteSpace(request.CancelledBy) || request.CancelledBy == "system"
            ? _userContextService.GetUserId()
            : request.CancelledBy;

        order.Cancel(cancelledBy);
        _repository.Update(order);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
