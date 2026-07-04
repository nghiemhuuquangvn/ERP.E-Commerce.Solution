using ERP.PRM.Application.Commands;
using ERP.PRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRM.API.Controllers;

[ApiController]
[Route("api/v1/flash-sales")]
public class FlashSalesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public FlashSalesController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetFlashSalesQuery { ActiveOnly = activeOnly }, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFlashSaleByIdQuery { FlashSaleId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFlashSaleCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        var result = await _mediator.Send(new StartFlashSaleCommand { FlashSaleId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("promotions:manage"))
            return Forbid();
        var result = await _mediator.Send(new CancelFlashSaleCommand { FlashSaleId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reserve")]
    public async Task<IActionResult> Reserve(Guid id, [FromBody] ReserveFlashSaleItemCommand command, CancellationToken ct)
    {
        command.FlashSaleId = id;
        var reserved = await _mediator.Send(command, ct);
        return reserved ? Ok(new { reserved = true }) : Conflict(new { reserved = false, message = "Insufficient stock." });
    }
}
