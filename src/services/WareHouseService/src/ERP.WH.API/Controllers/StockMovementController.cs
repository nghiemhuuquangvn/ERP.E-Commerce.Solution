using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Queries.Inventory;

namespace ERP.WH.API.Controllers;

[ApiController]
[Route("api/v1/movements")]
public class StockMovementController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockMovementController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(RecordStockMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordStockMovementResponse>> RecordMovement(
        [FromBody] RecordStockMovementCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetMovementHistory), new { productId = command.ProductId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(GetMovementHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetMovementHistoryResponse>> GetMovementHistory(
        Guid productId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var query = new GetMovementHistoryQuery
        {
            ProductId = productId,
            StartDate = startDate,
            EndDate = endDate,
            Skip = skip,
            Take = take
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
