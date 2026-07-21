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
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_View))
            return Forbid();
        return Ok(await _mediator.Send(new GetFlashSalesQuery { ActiveOnly = activeOnly }, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_View))
            return Forbid();
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

    /// <summary>
    /// Judgment call: Reserve decrements limited flash-sale stock, so per the task's
    /// "reuse Promotions_Manage for writes" instruction it is gated the same as
    /// Create/Start/Cancel. Unlike redeem-coupon, no frontend app in this repo (storefront,
    /// mobile customer/staff) calls this endpoint at all today, so there's no evidence of
    /// a customer-facing "reserve at checkout" flow to preserve — gating behind staff-only
    /// Promotions_Manage is the conservative choice that closes the hole now. If a
    /// customer-facing flash-sale checkout is wired up later, this will need the same
    /// "authenticated customer, no staff permission" treatment as redeem-coupon instead.
    /// </summary>
    [HttpPost("{id:guid}/reserve")]
    public async Task<IActionResult> Reserve(Guid id, [FromBody] ReserveFlashSaleItemCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Promotions_Manage))
            return Forbid();

        command.FlashSaleId = id;
        var reserved = await _mediator.Send(command, ct);
        return reserved ? Ok(new { reserved = true }) : Conflict(new { reserved = false, message = "Insufficient stock." });
    }
}
