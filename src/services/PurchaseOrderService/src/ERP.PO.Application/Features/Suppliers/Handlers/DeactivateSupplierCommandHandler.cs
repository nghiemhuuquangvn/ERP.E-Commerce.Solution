using MediatR;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

/// <summary>
/// Was a no-op (the endpoint answered 204 without changing anything). Same
/// fetch/mutate/save shape as UpdateSupplierCommandHandler.
/// </summary>
public class DeactivateSupplierCommandHandler : IRequestHandler<DeactivateSupplierCommand, Unit>
{
    private readonly IRepository<Supplier> _repository;
    private readonly IUserContextService _userContextService;

    public DeactivateSupplierCommandHandler(
        IRepository<Supplier> repository,
        IUserContextService userContextService)
    {
        _repository = repository;
        _userContextService = userContextService;
    }

    public async Task<Unit> Handle(DeactivateSupplierCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var supplier = await _repository.GetByIdAsync(request.SupplierId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier {request.SupplierId} not found");

        supplier.Deactivate();
        supplier.UpdatedBy = request.DeactivatedBy;

        _repository.Update(supplier);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
