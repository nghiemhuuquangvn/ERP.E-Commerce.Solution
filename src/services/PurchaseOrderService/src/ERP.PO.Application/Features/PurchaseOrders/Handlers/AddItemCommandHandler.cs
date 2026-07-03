using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>
/// Was a complete no-op before 2026-07-03: created the PurchaseOrderItem domain
/// object but never attached it to the order, never saved it, and returned
/// _mapper.Map&lt;PurchaseOrderDto&gt;(new object()) — a garbage empty DTO.
/// The order is loaded via IRepository&lt;PurchaseOrder&gt; (AsNoTracking, per that
/// interface) purely to run domain validation/recalculate TotalAmount; the new item
/// itself is persisted via an explicit IRepository&lt;PurchaseOrderItem&gt;.Add() call
/// so EF Core marks it Added rather than guessing Added/Modified off an
/// already-non-default Guid key on a re-attached, previously-untracked graph.
/// </summary>
public class AddItemCommandHandler : IRequestHandler<AddItemCommand, PurchaseOrderDto>
{
    private readonly IRepository<PurchaseOrder> _orderRepository;
    private readonly IRepository<PurchaseOrderItem> _itemRepository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public AddItemCommandHandler(
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

    public async Task<PurchaseOrderDto> Handle(AddItemCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _orderRepository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        var item = PurchaseOrderItem.Create(
            request.PurchaseOrderId,
            request.ProductCode,
            request.ProductName,
            request.Quantity,
            request.Unit,
            new Money(request.UnitPrice, "USD"));
        item.TenantId = tenantId;

        order.AddItem(item); // recalculates TotalAmount on the in-memory aggregate

        _itemRepository.Add(item);
        _orderRepository.Update(order);
        await _orderRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<PurchaseOrderDto>(order);
    }
}
