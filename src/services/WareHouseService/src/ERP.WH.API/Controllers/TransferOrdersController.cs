using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.WH.Application.Commands.Transfers;
using ERP.WH.Application.Queries.Transfers;

namespace ERP.WH.API.Controllers;

[ApiController]
[Route("api/v1/transfer-orders")]
public class TransferOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransferOrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<TransferOrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TransferOrderDto>>> List()
    {
        var result = await _mediator.Send(new ListTransferOrdersQuery());
        return Ok(result);
    }

    [HttpGet("{transferOrderId}")]
    [ProducesResponseType(typeof(TransferOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransferOrderDto>> GetById(Guid transferOrderId)
    {
        var result = await _mediator.Send(new GetTransferOrderByIdQuery { TransferOrderId = transferOrderId });
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TransferOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransferOrderDto>> Create([FromBody] CreateTransferOrderCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { transferOrderId = result.TransferOrderId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{transferOrderId}/ship")]
    [ProducesResponseType(typeof(TransferOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransferOrderDto>> Ship(Guid transferOrderId)
    {
        try
        {
            var result = await _mediator.Send(new ShipTransferOrderCommand { TransferOrderId = transferOrderId });
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

    [HttpPost("{transferOrderId}/complete")]
    [ProducesResponseType(typeof(TransferOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransferOrderDto>> Complete(Guid transferOrderId)
    {
        try
        {
            var result = await _mediator.Send(new CompleteTransferOrderCommand { TransferOrderId = transferOrderId });
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

    [HttpPost("{transferOrderId}/cancel")]
    [ProducesResponseType(typeof(TransferOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransferOrderDto>> Cancel(Guid transferOrderId)
    {
        try
        {
            var result = await _mediator.Send(new CancelTransferOrderCommand { TransferOrderId = transferOrderId });
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
