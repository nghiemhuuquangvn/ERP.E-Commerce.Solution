using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Services;
using ERP.WH.Domain.Aggregates.Inventory;
using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>
/// One production movement asked by another service (Manufacturing) through <c>internal/v1/stock/movements</c>. The idempotency key is the
/// movement reference "&lt;document number&gt;#&lt;line&gt;"; only the six production types are accepted here. Lot / serial flags are passed by the
/// caller (it reads them from Product). Validation lives here so the controller stays a thin shell (docs/business/Warehouse/ProductionIntegration.md).
/// </summary>
public class InternalProductionMovementCommand : IRequest<RecordStockMovementResponse>
{
    public required string MovementType { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>"MO-2611-00001#3": document number, '#', line. Booking the same key again answers the first result.</summary>
    public required string IdempotencyKey { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ToWarehouseId { get; set; }
    public Guid? LotId { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public decimal? UnitCost { get; set; }
    public string? InitialLotStatus { get; set; }
    public bool AllowNegative { get; set; }
    public string? Reason { get; set; }
    /// <summary>The production order id (kept on the movement as its source).</summary>
    public Guid? SourceOrderId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? SourceLineId { get; set; }
    public bool RequiresLot { get; set; }
    public bool RequiresSerial { get; set; }
}

public class InternalProductionMovementCommandHandler(IMediator mediator) : IRequestHandler<InternalProductionMovementCommand, RecordStockMovementResponse>
{
    public const int MaxKeyLength = 100;

    public Task<RecordStockMovementResponse> Handle(InternalProductionMovementCommand r, CancellationToken ct)
    {
        if (!Enum.TryParse<MovementType>(r.MovementType, ignoreCase: true, out var type) || !Enum.IsDefined(type) || !type.IsProduction())
            throw new BusinessRuleException("WH_MOVEMENT_TYPE", $"{r.MovementType} is not a production movement type.");

        var key = r.IdempotencyKey?.Trim() ?? string.Empty;
        var hash = key.IndexOf('#');
        if (key.Length > MaxKeyLength || hash <= 0 || hash == key.Length - 1)
            throw new BusinessRuleException("WH_REFERENCE_FORMAT", "The idempotency key is '<document number>#<line>' (e.g. MO-2611-00001#3), at most 100 characters.");

        return mediator.Send(new RecordStockMovementCommand
        {
            ProductId = r.ProductId,
            Quantity = r.Quantity,
            MovementType = type.ToString(),
            Reference = key,
            Reason = r.Reason,
            SourceOrderId = r.SourceOrderId,
            WarehouseId = r.WarehouseId,
            ToWarehouseId = r.ToWarehouseId,
            LotId = r.LotId,
            LotNumber = r.LotNumber,
            ExpiryDate = r.ExpiryDate,
            ManufactureDate = r.ManufactureDate,
            UnitCost = r.UnitCost,
            InitialLotStatus = r.InitialLotStatus,
            AllowNegative = r.AllowNegative,
            ReservationId = r.ReservationId,
            SourceLineId = r.SourceLineId,
            RequiresLot = r.RequiresLot,
            RequiresSerial = r.RequiresSerial
        }, ct);
    }
}

/// <summary>Puts a lot on quality hold or blocks it (FQC failed, NCR). The way back to Available is the QcRelease movement, which keeps the history.</summary>
public class SetLotStatusCommand : IRequest<SetLotStatusResponse>
{
    public Guid LotId { get; set; }
    /// <summary>"QcHold" or "Blocked".</summary>
    public required string Status { get; set; }
}

public class SetLotStatusResponse
{
    public Guid LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>True when the lot already had this status: nothing changed.</summary>
    public bool Unchanged { get; set; }
}

public class SetLotStatusCommandHandler(IWarehouseUnitOfWork uow, IStockLedger ledger, ICacheService cache) : IRequestHandler<SetLotStatusCommand, SetLotStatusResponse>
{
    public async Task<SetLotStatusResponse> Handle(SetLotStatusCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<LotStatus>(request.Status, ignoreCase: true, out var status) || status == LotStatus.Available)
            throw new BusinessRuleException("WH_LOT_STATUS", "A lot can be put on QcHold or Blocked here; releasing it to Available is a QcRelease movement.");

        var lot = await uow.InventoryLots.GetByIdAsync(request.LotId, ct) ?? throw new EntityNotFoundException("InventoryLot", request.LotId);
        var unchanged = lot.Status == status;
        if (!unchanged)
        {
            await ledger.SetLotStatusAsync(lot, status, ct);
            await uow.SaveChangesAsync(ct);
            await cache.RemoveAsync($"wh:inventory:{lot.ProductId}", ct);
        }

        return new SetLotStatusResponse { LotId = lot.LotId, LotNumber = lot.LotNumber, Status = lot.Status.ToString(), Unchanged = unchanged };
    }
}
