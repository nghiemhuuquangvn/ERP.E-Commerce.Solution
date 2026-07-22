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
[Route("api/v{version:apiVersion}/movements")]
[Authorize]
public class StockMovementController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContextService;

    public StockMovementController(IMediator mediator, IUserContextService userContextService)
    {
        _mediator = mediator;
        _userContextService = userContextService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(RecordStockMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecordStockMovementResponse>> RecordMovement(
        [FromBody] RecordStockMovementCommand command)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

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
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

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
