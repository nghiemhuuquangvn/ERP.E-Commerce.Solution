using ERP.SHP.Application.Commands;
using ERP.SHP.Application.Queries;
using ERP.SHP.Infrastructure.Carriers;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SHP.API.Controllers;

[ApiController]
[Route("api/v1/shipments")]
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
        var result = await _mediator.Send(new GetShipmentQuery { ShipmentId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/track")]
    public async Task<IActionResult> Track(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new TrackShipmentQuery { ShipmentId = id }, ct);
        return Ok(result);
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListShipmentsByOrderQuery { OrderId = orderId }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/tracking")]
    public async Task<IActionResult> AddTracking(Guid id, [FromBody] AddTrackingEventCommand command, CancellationToken ct)
    {
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
}

[ApiController]
[Route("api/v1/carriers")]
public class CarriersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICarrierClient _carrierClient;

    public CarriersController(IMediator mediator, ICarrierClient carrierClient)
    {
        _mediator = mediator;
        _carrierClient = carrierClient;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListCarriersQuery(), ct);
        return Ok(result);
    }

    [HttpPost("calculate-fee")]
    public async Task<IActionResult> CalculateFee([FromBody] FeeRequest request, CancellationToken ct)
    {
        var fees = await _carrierClient.CalculateFeeAsync(request, ct);
        return Ok(fees);
    }
}
