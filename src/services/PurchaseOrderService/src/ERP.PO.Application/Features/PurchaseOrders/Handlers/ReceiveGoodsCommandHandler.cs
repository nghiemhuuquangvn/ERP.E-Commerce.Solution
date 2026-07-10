using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>
/// Goods Receipt Note (GRN) — completes the receiving flow the ❌ list called out as
/// entirely missing. PurchaseOrder.ReceiveGoods() mutates the already-loaded (and, per
/// AutoInclude, already-attached-via-navigation) PurchaseOrderItem instances directly,
/// so a plain IRepository&lt;PurchaseOrder&gt;.Update() + SaveChanges is enough here —
/// unlike AddItem/RemoveItem, no new/deleted rows are involved, only scalar field
/// changes on existing, already-tracked-in-this-query items.
/// </summary>
public class ReceiveGoodsCommandHandler : IRequestHandler<ReceiveGoodsCommand, PurchaseOrderDto>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IRepository<GoodsReceipt> _receiptRepository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public ReceiveGoodsCommandHandler(
        IRepository<PurchaseOrder> repository,
        IRepository<GoodsReceipt> receiptRepository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _repository = repository;
        _receiptRepository = receiptRepository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<PurchaseOrderDto> Handle(ReceiveGoodsCommand request, CancellationToken cancellationToken)
    {
        var user = _userContextService.GetCurrentUser();
        var tenantId = new Guid(user.TenantId);

        var order = await _repository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        var receivedLines = request.ReceivedQuantities.ToDictionary(
            kv => kv.Key,
            kv => new GoodsReceiptLineInput { QuantityAccepted = kv.Value.QuantityAccepted, QuantityRejected = kv.Value.QuantityRejected });

        order.ReceiveGoods(receivedLines);
        order.UpdatedBy = user.UserId;

        var receiptNumber = $"GRN-{order.PONumber}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var receipt = GoodsReceipt.Create(order.Id, tenantId, receiptNumber, user.UserId);
        _receiptRepository.Add(receipt);

        _repository.Update(order);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<PurchaseOrderDto>(order);
    }
}

public class ScheduleReceiptCommandHandler : IRequestHandler<ScheduleReceiptCommand, PurchaseOrderDto>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public ScheduleReceiptCommandHandler(IRepository<PurchaseOrder> repository, IUserContextService userContextService, IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<PurchaseOrderDto> Handle(ScheduleReceiptCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _repository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        order.ScheduleReceipt(request.ScheduledDate);

        _repository.Update(order);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<PurchaseOrderDto>(order);
    }
}
