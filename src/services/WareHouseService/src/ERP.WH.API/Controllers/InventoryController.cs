using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using ERP.Shared.Authorization;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Queries.Inventory;

namespace ERP.WH.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContextService;

    public InventoryController(IMediator mediator, IUserContextService userContextService)
    {
        _mediator = mediator;
        _userContextService = userContextService;
    }

    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(GetInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetInventoryResponse>> GetInventory(Guid productId)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        try
        {
            var query = new GetInventoryQuery { ProductId = productId };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListInventoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListInventoryResponse>> ListInventory(
        [FromQuery] string search = null,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var query = new ListInventoryQuery
        {
            Search = search,
            LowStockOnly = lowStockOnly,
            Skip = skip,
            Take = take
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(ListInventoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListInventoryResponse>> GetLowStockItems()
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var query = new ListInventoryQuery
        {
            LowStockOnly = true,
            Skip = 0,
            Take = 100
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("reserve")]
    [ProducesResponseType(typeof(ReserveInventoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReserveInventoryResponse>> ReserveInventory(
        [FromBody] ReserveInventoryCommand command)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetInventory), new { productId = result.OrderId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("release")]
    [ProducesResponseType(typeof(ReleaseReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReleaseReservationResponse>> ReleaseReservation(
        [FromBody] ReleaseReservationCommand command)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("receive")]
    [ProducesResponseType(typeof(ReceiveStockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReceiveStockResponse>> ReceiveStock([FromBody] ReceiveStockCommand command)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetInventory), new { productId = result.ProductId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{productId}/adjust")]
    [ProducesResponseType(typeof(AdjustStockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdjustStockResponse>> AdjustStock(Guid productId, [FromBody] AdjustStockCommand command)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        command.ProductId = productId;

        try
        {
            var result = await _mediator.Send(command);
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

    [HttpGet("{productId}/valuation")]
    [ProducesResponseType(typeof(GetInventoryValuationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetInventoryValuationResponse>> GetValuation(Guid productId)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new GetInventoryValuationQuery { ProductId = productId });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
