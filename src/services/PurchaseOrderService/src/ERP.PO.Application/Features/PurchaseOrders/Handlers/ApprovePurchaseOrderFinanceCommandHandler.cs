using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>Step 2 of the approval chain — see PurchaseOrder.ApproveFinance().</summary>
public class ApprovePurchaseOrderFinanceCommandHandler : IRequestHandler<ApprovePurchaseOrderFinanceCommand, Unit>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;

    public ApprovePurchaseOrderFinanceCommandHandler(IRepository<PurchaseOrder> repository, IUserContextService userContextService)
    {
        _repository = repository;
        _userContextService = userContextService;
    }

    public async Task<Unit> Handle(ApprovePurchaseOrderFinanceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _repository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        var approvedBy = string.IsNullOrWhiteSpace(request.ApprovedBy) || request.ApprovedBy == "system"
            ? _userContextService.GetUserId()
            : request.ApprovedBy;

        order.ApproveFinance(approvedBy);
        _repository.Update(order);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
