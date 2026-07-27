using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using ERP.Shared.Authorization;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Queries.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.API.Controllers;

/// <summary>
/// Replaces the previous direct-write `POST /inventory/{productId}/adjust` endpoint —
/// per the business rule that a stock adjustment must always be approved before it
/// touches Inventory, this is now a request/approve/reject workflow instead of an
/// immediate mutation.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock-adjustments")]
[Authorize]
public class StockAdjustmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContextService;

    public StockAdjustmentsController(IMediator mediator, IUserContextService userContextService)
    {
        _mediator = mediator;
        _userContextService = userContextService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<StockAdjustmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StockAdjustmentDto>>> List([FromQuery] StockAdjustmentStatus? status)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var result = await _mediator.Send(new ListStockAdjustmentsQuery { Status = status });
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StockAdjustmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockAdjustmentDto>> RequestAdjustment([FromBody] RequestStockAdjustmentCommand command)
    {
        if (!_userContextService.HasPermission("inventory:adjustment:create"))
            return Forbid();

        command.RequestedBy = _userContextService.GetCurrentUser().UserId;

        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(RequestAdjustment), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(AdjustStockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdjustStockResponse>> Approve(Guid id)
    {
        if (!_userContextService.HasPermission("inventory:adjustment:approve"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new ApproveStockAdjustmentCommand
            {
                AdjustmentId = id,
                ApprovedBy = _userContextService.GetCurrentUser().UserId
            });
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(StockAdjustmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockAdjustmentDto>> Reject(Guid id, [FromBody] RejectStockAdjustmentRequest request)
    {
        if (!_userContextService.HasPermission("inventory:adjustment:approve"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new RejectStockAdjustmentCommand
            {
                AdjustmentId = id,
                RejectedBy = _userContextService.GetCurrentUser().UserId,
                Reason = request.Reason
            });
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class RejectStockAdjustmentRequest
{
    public string Reason { get; set; } = string.Empty;
}
