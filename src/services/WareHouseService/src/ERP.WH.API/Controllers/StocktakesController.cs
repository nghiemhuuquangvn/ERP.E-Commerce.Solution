using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using ERP.Shared.Authorization;
using ERP.WH.Application.Commands.Stocktakes;
using ERP.WH.Application.Queries.Stocktakes;

namespace ERP.WH.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stocktakes")]
[Authorize]
public class StocktakesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContextService;

    public StocktakesController(IMediator mediator, IUserContextService userContextService)
    {
        _mediator = mediator;
        _userContextService = userContextService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<StocktakeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StocktakeDto>>> List()
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var result = await _mediator.Send(new ListStocktakesQuery());
        return Ok(result);
    }

    [HttpGet("{stocktakeId}")]
    [ProducesResponseType(typeof(StocktakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StocktakeDto>> GetById(Guid stocktakeId)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var result = await _mediator.Send(new GetStocktakeByIdQuery { StocktakeId = stocktakeId });
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StocktakeDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<StocktakeDto>> Start([FromBody] StartStocktakeCommand command)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { stocktakeId = result.StocktakeId }, result);
    }

    [HttpPost("{stocktakeId}/counts")]
    [ProducesResponseType(typeof(StocktakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StocktakeDto>> RecordCount(Guid stocktakeId, [FromBody] RecordStocktakeCountCommand command)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        command.StocktakeId = stocktakeId;

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

    [HttpPost("{stocktakeId}/complete")]
    [ProducesResponseType(typeof(StocktakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StocktakeDto>> Complete(Guid stocktakeId)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new CompleteStocktakeCommand { StocktakeId = stocktakeId });
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

    [HttpPost("{stocktakeId}/cancel")]
    [ProducesResponseType(typeof(StocktakeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StocktakeDto>> Cancel(Guid stocktakeId)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new CancelStocktakeCommand { StocktakeId = stocktakeId });
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
