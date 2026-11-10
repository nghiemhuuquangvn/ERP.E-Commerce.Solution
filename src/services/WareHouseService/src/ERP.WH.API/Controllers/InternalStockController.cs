using ERP.Shared.Authorization.ServiceAuth;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Queries.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WH.API.Controllers;

/// <summary>
/// Service-to-service stock API for ManufacturingService (service token, caller "manufacturing", the token's tenant is the request tenant).
/// Reads: availability, lot suggestion, reservation. Writes: hold components for a production order, release, production movements
/// (issue / return / receipt / scrap / quality release / subcontract transfer) and putting a lot on hold. Every write is idempotent:
/// movements by their key "&lt;document number&gt;#&lt;line&gt;" (same key + same payload = the first result again, other payload = 409
/// WH_IDEMPOTENCY_CONFLICT), a reservation by its source document, a release by state, a lot status by value.
/// Never routed through the Gateway. docs/business/Warehouse/ProductionIntegration.md.
/// </summary>
[ApiController]
[Route("internal/v1/stock")]
public class InternalStockController(IMediator mediator) : ControllerBase
{
    [HttpGet("{productId:guid}/availability")]
    [AuthorizeInternalService("manufacturing")]
    [ProducesResponseType(typeof(StockAvailabilityDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Availability(Guid productId, [FromQuery] Guid? warehouseId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetStockAvailabilityQuery { ProductId = productId, WarehouseId = warehouseId }, ct));

    [HttpGet("{productId:guid}/suggest-lots")]
    [AuthorizeInternalService("manufacturing")]
    [ProducesResponseType(typeof(SuggestLotsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SuggestLots(Guid productId, [FromQuery] decimal quantity, [FromQuery] Guid? warehouseId, CancellationToken ct) =>
        Ok(await mediator.Send(new SuggestLotsQuery { ProductId = productId, Quantity = quantity, WarehouseId = warehouseId }, ct));

    /// <summary>Every lot of the item, depleted ones included (P3-5: unique lot numbers of finished goods).</summary>
    [HttpGet("{productId:guid}/lots")]
    [AuthorizeInternalService("manufacturing")]
    [ProducesResponseType(typeof(List<ProductLotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Lots(Guid productId, CancellationToken ct) =>
        Ok(await mediator.Send(new ListProductLotsQuery { ProductId = productId }, ct));

    [HttpPost("reservations")]
    [AuthorizeInternalService("manufacturing")]
    [ProducesResponseType(typeof(SourceReservationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reserve([FromBody] ReserveForSourceCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpGet("reservations/{sourceType}/{sourceId:guid}")]
    [AuthorizeInternalService("manufacturing")]
    [ProducesResponseType(typeof(SourceReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservation(string sourceType, Guid sourceId, CancellationToken ct)
    {
        var found = await mediator.Send(new GetSourceReservationQuery { SourceType = sourceType, SourceId = sourceId }, ct);
        return found == null ? NotFound(new { message = "No reservation for this source." }) : Ok(found);
    }

    public class ReleaseRequest
    {
        public string? Reason { get; set; }
    }

    [HttpPost("reservations/{reservationId:guid}/release")]
    [AuthorizeInternalService("manufacturing")]
    [ProducesResponseType(typeof(ReleaseReservationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Release(Guid reservationId, [FromBody] ReleaseRequest? body, CancellationToken ct) =>
        Ok(await mediator.Send(new ReleaseReservationCommand { ReservationId = reservationId, Reason = string.IsNullOrWhiteSpace(body?.Reason) ? "Released by manufacturing" : body.Reason }, ct));

    [HttpPost("movements")]
    [AuthorizeInternalService("manufacturing")]
    [ProducesResponseType(typeof(RecordStockMovementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Movement([FromBody] InternalProductionMovementCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    public class LotStatusRequest
    {
        public required string Status { get; set; }
    }

    [HttpPut("lots/{lotId:guid}/status")]
    [AuthorizeInternalService("manufacturing")]
    [ProducesResponseType(typeof(SetLotStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetLotStatus(Guid lotId, [FromBody] LotStatusRequest body, CancellationToken ct) =>
        Ok(await mediator.Send(new SetLotStatusCommand { LotId = lotId, Status = body.Status }, ct));
}
