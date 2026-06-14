using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Queries.Inventory;

namespace ERP.WH.API.Controllers;

[ApiController]
[Route("api/v1/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(GetInventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetInventoryResponse>> GetInventory(Guid productId)
    {
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
}
