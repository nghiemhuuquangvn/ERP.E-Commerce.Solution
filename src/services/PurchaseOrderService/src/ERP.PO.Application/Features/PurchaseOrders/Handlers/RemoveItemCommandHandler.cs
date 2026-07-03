using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>
/// Was a complete no-op before 2026-07-03 (returned a garbage empty DTO, touched
/// nothing) — see AddItemCommandHandler's note for why the item's actual DELETE goes
/// through an explicit IRepository&lt;PurchaseOrderItem&gt;.Delete() call rather than
/// relying on collection-navigation change tracking against an AsNoTracking graph
/// (which would silently leave the orphaned row in place).
/// </summary>
public class RemoveItemCommandHandler : IRequestHandler<RemoveItemCommand, PurchaseOrderDto>
{
    private readonly IRepository<PurchaseOrder> _orderRepository;
    private readonly IRepository<PurchaseOrderItem> _itemRepository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public RemoveItemCommandHandler(
        IRepository<PurchaseOrder> orderRepository,
        IRepository<PurchaseOrderItem> itemRepository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<PurchaseOrderDto> Handle(RemoveItemCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _orderRepository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        var item = await _itemRepository.GetByIdAsync(request.ItemId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Item {request.ItemId} not found");

        order.RemoveItem(request.ItemId); // recalculates TotalAmount on the in-memory aggregate

        _itemRepository.Delete(item);
        _orderRepository.Update(order);
        await _orderRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<PurchaseOrderDto>(order);
    }
}
