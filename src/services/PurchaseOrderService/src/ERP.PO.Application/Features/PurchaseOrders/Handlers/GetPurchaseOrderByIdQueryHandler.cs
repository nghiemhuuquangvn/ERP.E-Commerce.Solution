using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Queries;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>Was a complete no-op before 2026-07-03 (returned _mapper.Map&lt;PurchaseOrderDto&gt;(new object()) — a garbage empty DTO, never queried the database).</summary>
public class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto?>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetPurchaseOrderByIdQueryHandler(IRepository<PurchaseOrder> repository, IUserContextService userContextService, IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<PurchaseOrderDto?> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _repository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken);

        return order == null ? null : _mapper.Map<PurchaseOrderDto>(order);
    }
}
