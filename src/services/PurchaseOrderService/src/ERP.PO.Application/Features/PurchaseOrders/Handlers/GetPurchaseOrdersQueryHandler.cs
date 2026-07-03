using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Queries;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Enums;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>
/// Was a complete no-op before 2026-07-03 (returned `new List&lt;PurchaseOrderDto&gt;()`
/// unconditionally — never queried the database at all). Filters/paginates in-memory
/// after IRepository&lt;T&gt;.GetAllAsync(tenantId) since the shared generic repository
/// interface has no filtered-query overload — acceptable for this service's expected
/// data volumes; a real IQueryable-based query would be a separate, broader change to
/// the shared IRepository&lt;T&gt; contract affecting every service that uses it.
/// </summary>
public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, IEnumerable<PurchaseOrderDto>>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetPurchaseOrdersQueryHandler(IRepository<PurchaseOrder> repository, IUserContextService userContextService, IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var orders = (await _repository.GetAllAsync(tenantId, cancellationToken)).AsEnumerable();

        if (request.SupplierId.HasValue)
            orders = orders.Where(po => po.SupplierId == request.SupplierId.Value);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<PurchaseOrderStatus>(request.Status, true, out var status))
            orders = orders.Where(po => po.Status == status);

        var pageSize = Math.Min(100, Math.Max(1, request.PageSize));
        var pageNumber = Math.Max(1, request.PageNumber);

        var page = orders
            .OrderByDescending(po => po.OrderDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return _mapper.Map<IEnumerable<PurchaseOrderDto>>(page);
    }
}
