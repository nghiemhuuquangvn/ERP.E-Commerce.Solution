using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>
/// Was a complete no-op before 2026-07-03 (Task.CompletedTask, return Unit.Value —
/// never loaded the order, never called Approve(), never saved) despite the command,
/// handler class, controller endpoint, and permission check all existing and looking
/// complete.
/// </summary>
public class ApprovePurchaseOrderCommandHandler : IRequestHandler<ApprovePurchaseOrderCommand, Unit>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;

    public ApprovePurchaseOrderCommandHandler(IRepository<PurchaseOrder> repository, IUserContextService userContextService)
    {
        _repository = repository;
        _userContextService = userContextService;
    }

    public async Task<Unit> Handle(ApprovePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _repository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        var approvedBy = string.IsNullOrWhiteSpace(request.ApprovedBy) || request.ApprovedBy == "system"
            ? _userContextService.GetUserId()
            : request.ApprovedBy;

        order.Approve(approvedBy);
        _repository.Update(order);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
