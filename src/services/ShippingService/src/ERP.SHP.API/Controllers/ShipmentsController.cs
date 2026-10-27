using Asp.Versioning;
using ERP.SHP.Application.Commands;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SHP.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipments")]
public class ShipmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public ShipmentsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShipmentCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:create"))
            return Forbid();

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        var result = await _mediator.Send(new GetShipmentQuery { ShipmentId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/track")]
    public async Task<IActionResult> Track(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        var result = await _mediator.Send(new TrackShipmentQuery { ShipmentId = id }, ct);
        return Ok(result);
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        var result = await _mediator.Send(new ListShipmentsByOrderQuery { OrderId = orderId }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/tracking")]
    public async Task<IActionResult> AddTracking(Guid id, [FromBody] AddTrackingEventCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        command.ShipmentId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/tracking-number")]
    public async Task<IActionResult> AssignTrackingNumber(Guid id, [FromBody] AssignTrackingNumberCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        command.ShipmentId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deliver")]
    public async Task<IActionResult> ConfirmDelivery(Guid id, [FromBody] ConfirmDeliveryCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        command.ShipmentId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/delivery-failed")]
    public async Task<IActionResult> MarkDeliveryFailed(Guid id, [FromBody] MarkDeliveryFailedCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        command.ShipmentId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/retry-delivery")]
    public async Task<IActionResult> RetryDelivery(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        var result = await _mediator.Send(new RetryDeliveryCommand { ShipmentId = id }, ct);
        return Ok(result);
    }

    [HttpPost("bulk-tracking")]
    public async Task<IActionResult> BulkUpdateTracking([FromBody] BulkUpdateTrackingCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/label")]
    public async Task<IActionResult> GetLabel(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        var pdfBytes = await _mediator.Send(new GenerateShippingLabelQuery { ShipmentId = id }, ct);
        return File(pdfBytes, "application/pdf", $"shipping-label-{id:N}.pdf");
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipments/{shipmentId:guid}/returns")]
public class ReturnShipmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public ReturnShipmentsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid shipmentId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        var result = await _mediator.Send(new ListReturnsByShipmentQuery { OriginalShipmentId = shipmentId }, ct);
        return Ok(result);
    }

    [HttpGet("{returnId:guid}")]
    public async Task<IActionResult> GetById(Guid shipmentId, Guid returnId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        var result = await _mediator.Send(new GetReturnShipmentByIdQuery { ReturnShipmentId = returnId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Initiate(Guid shipmentId, [FromBody] InitiateReturnCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        command.OriginalShipmentId = shipmentId;

        try
        {
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { shipmentId, returnId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{returnId:guid}/tracking-number")]
    public async Task<IActionResult> AssignTrackingNumber(Guid shipmentId, Guid returnId, [FromBody] AssignReturnTrackingNumberCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        command.ReturnShipmentId = returnId;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{returnId:guid}/picked-up")]
    public async Task<IActionResult> MarkPickedUp(Guid shipmentId, Guid returnId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        var result = await _mediator.Send(new MarkReturnPickedUpCommand { ReturnShipmentId = returnId }, ct);
        return Ok(result);
    }

    [HttpPost("{returnId:guid}/received")]
    public async Task<IActionResult> MarkReceived(Guid shipmentId, Guid returnId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        var result = await _mediator.Send(new MarkReturnReceivedCommand { ReturnShipmentId = returnId }, ct);
        return Ok(result);
    }

    [HttpPost("{returnId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid shipmentId, Guid returnId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:update"))
            return Forbid();

        var result = await _mediator.Send(new CancelReturnCommand { ReturnShipmentId = returnId }, ct);
        return Ok(result);
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/carriers")]
public class CarriersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ERP.SHP.Application.Services.CarrierBookingService _booking;
    private readonly UserContext _userContext;

    public CarriersController(IMediator mediator, ERP.SHP.Application.Services.CarrierBookingService booking, UserContext userContext)
    {
        _mediator = mediator;
        _booking = booking;
        _userContext = userContext;
    }

    // Carrier configuration is admin-facing (not called by any customer-facing
    // surface in this repo) — reads use shipping:view, same as the other two
    // controllers in this file; there is currently no create/update/delete action
    // here (ICarrierRepository supports Add/Update but nothing wires it up yet), so
    // shipping:carriers:manage isn't consumed by this controller today. It's still
    // defined/granted now so the first carrier-CRUD action added later has a
    // permission ready instead of repeating the phantom-permission bug this pass is
    // fixing elsewhere.
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        var result = await _mediator.Send(new ListCarriersQuery(), ct);
        return Ok(result);
    }

    [HttpPost("calculate-fee")]
    public async Task<IActionResult> CalculateFee([FromBody] ERP.SHP.Application.DTOs.QuoteParcelRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        var sender = await _booking.SenderAsync(ct);
        var to = new ERP.SHP.Application.Interfaces.CarrierAddress("Recipient", "0900000000", request.Street ?? string.Empty, request.Ward, request.District, request.Province);
        var quotes = await _booking.QuoteAllAsync(new ERP.SHP.Application.Interfaces.CarrierQuoteRequest(sender.Address, to, request.WeightGrams, request.DeclaredValue, request.CodAmount), ct);
        return Ok(quotes);
    }

    [HttpGet("{id:guid}/quote")]
    public async Task<IActionResult> GetQuote(Guid id, [FromQuery] decimal weightKg, [FromQuery] string? destinationProvince, CancellationToken ct)
    {
        if (!_userContext.HasPermission("shipping:view"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new GetCarrierQuoteQuery { CarrierId = id, WeightKg = weightKg, DestinationProvince = destinationProvince }, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
