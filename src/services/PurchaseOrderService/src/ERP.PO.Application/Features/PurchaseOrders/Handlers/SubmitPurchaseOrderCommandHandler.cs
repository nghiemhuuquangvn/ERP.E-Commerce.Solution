using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>Was a complete no-op before 2026-07-03 — see ApprovePurchaseOrderCommandHandler's note, same bug.</summary>
public class SubmitPurchaseOrderCommandHandler : IRequestHandler<SubmitPurchaseOrderCommand, Unit>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;

    public SubmitPurchaseOrderCommandHandler(IRepository<PurchaseOrder> repository, IUserContextService userContextService)
    {
        _repository = repository;
        _userContextService = userContextService;
    }

    public async Task<Unit> Handle(SubmitPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _repository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        order.Submit();
        order.UpdatedBy = string.IsNullOrWhiteSpace(request.SubmittedBy) || request.SubmittedBy == "system"
            ? _userContextService.GetUserId()
            : request.SubmittedBy;

        _repository.Update(order);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
