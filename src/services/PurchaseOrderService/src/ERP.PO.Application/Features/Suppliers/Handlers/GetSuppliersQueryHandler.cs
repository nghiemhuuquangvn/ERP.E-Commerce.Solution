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
public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, PagedList<SupplierDto>>
{

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

    public async Task<PagedList<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);
        var (pageNumber, pageSize) = PagedList<SupplierDto>.Clamp(request.PageNumber, request.PageSize);

        var text = request.Search?.Trim();
        var matching = (await _repository.GetAllAsync(tenantId, cancellationToken))
            .Where(s => request.IsActive == null || s.IsActive == request.IsActive)
            .Where(s => string.IsNullOrEmpty(text)
                        || s.Code.Contains(text, StringComparison.OrdinalIgnoreCase)
                        || s.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                        || (s.Email != null && s.Email.Contains(text, StringComparison.OrdinalIgnoreCase))
                        || (s.TaxCode != null && s.TaxCode.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(s => s.Name)
            .ToList();

        var page = matching.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedList<SupplierDto>(_mapper.Map<List<SupplierDto>>(page), matching.Count, pageNumber, pageSize);
    }
}
