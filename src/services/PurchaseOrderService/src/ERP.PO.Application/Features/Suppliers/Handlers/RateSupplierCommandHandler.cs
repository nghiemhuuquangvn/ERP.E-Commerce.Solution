using MediatR;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class RateSupplierCommandHandler : IRequestHandler<RateSupplierCommand, Guid>
{
    private readonly IRepository<Supplier> _supplierRepository;
    private readonly IRepository<SupplierRating> _ratingRepository;
    private readonly IUserContextService _userContextService;

    public RateSupplierCommandHandler(
        IRepository<Supplier> supplierRepository,
        IRepository<SupplierRating> ratingRepository,
        IUserContextService userContextService)
    {
        _supplierRepository = supplierRepository;
        _ratingRepository = ratingRepository;
        _userContextService = userContextService;
    }

    public async Task<Guid> Handle(RateSupplierCommand request, CancellationToken cancellationToken)
    {
        var user = _userContextService.GetCurrentUser();
        var tenantId = new Guid(user.TenantId);

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier {request.SupplierId} not found");

        var rating = SupplierRating.Create(supplier.Id, tenantId, request.Rating, request.Comment, user.UserId);

        _ratingRepository.Add(rating);
        await _ratingRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return rating.Id;
    }
}
