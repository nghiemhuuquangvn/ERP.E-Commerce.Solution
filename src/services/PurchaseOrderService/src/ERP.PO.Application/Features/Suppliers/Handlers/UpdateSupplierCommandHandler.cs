using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

/// <summary>
/// Was a complete no-op before this pass: never fetched the supplier, never called
/// Supplier.Update, never saved — just returned _mapper.Map&lt;SupplierDto&gt;(new object()),
/// a garbage empty DTO. Now follows the same fetch/mutate/save shape as
/// RateSupplierCommandHandler/AddItemCommandHandler.
/// </summary>
public class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, SupplierDto>
{
    private readonly IRepository<Supplier> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public UpdateSupplierCommandHandler(
        IRepository<Supplier> repository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<SupplierDto> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var supplier = await _repository.GetByIdAsync(request.SupplierId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier {request.SupplierId} not found");

        var address = request.Address != null
            ? new Address(request.Address.Street, request.Address.City, request.Address.State,
                         request.Address.PostalCode, request.Address.Country)
            : null;

        supplier.Update(
            request.Name,
            request.ContactPerson,
            request.Email,
            request.Phone,
            address,
            request.TaxCode,
            request.CurrencyCode);

        supplier.UpdatedBy = request.UpdatedBy;

        _repository.Update(supplier);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SupplierDto>(supplier);
    }
}
