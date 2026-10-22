using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Queries;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

/// <summary>
/// Was a stub that always returned an empty list. Lists the current tenant's suppliers,
/// optionally filtered by IsActive, ordered by name and paged.
/// </summary>
public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, IEnumerable<SupplierDto>>
{
    private const int MaxPageSize = 100;

    private readonly IRepository<Supplier> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetSuppliersQueryHandler(
        IRepository<Supplier> repository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var suppliers = (await _repository.GetAllAsync(tenantId, cancellationToken))
            .Where(s => request.IsActive == null || s.IsActive == request.IsActive)
            .OrderBy(s => s.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return _mapper.Map<IEnumerable<SupplierDto>>(suppliers);
    }
}
