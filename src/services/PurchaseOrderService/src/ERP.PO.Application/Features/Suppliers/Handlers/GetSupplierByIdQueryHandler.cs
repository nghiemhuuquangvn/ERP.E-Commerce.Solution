using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Queries;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

/// <summary>
/// Was a stub returning _mapper.Map&lt;SupplierDto&gt;(new object()). Returns null when the
/// supplier doesn't exist in the current tenant (the controller answers 404).
/// </summary>
public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto?>
{
    private readonly IRepository<Supplier> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetSupplierByIdQueryHandler(
        IRepository<Supplier> repository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<SupplierDto?> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var supplier = await _repository.GetByIdAsync(request.SupplierId, tenantId, cancellationToken);
        return supplier == null ? null : _mapper.Map<SupplierDto>(supplier);
    }
}
