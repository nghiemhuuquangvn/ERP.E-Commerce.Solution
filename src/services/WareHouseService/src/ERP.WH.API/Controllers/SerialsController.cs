using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.WH.Application.Commands.Serials;
using ERP.WH.Application.Queries.Serials;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.API.Controllers;

[ApiController]
[Route("api/v1/inventory/serials")]
public class SerialsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SerialsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("product/{productId}")]
    [ProducesResponseType(typeof(List<SerialDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SerialDto>>> GetByProduct(Guid productId, [FromQuery] SerialStatus? status = null)
    {
        var result = await _mediator.Send(new GetSerialsByProductQuery { ProductId = productId, Status = status });
        return Ok(result);
    }

    [HttpPost("{serialNumber}/status")]
    [ProducesResponseType(typeof(SerialDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SerialDto>> UpdateStatus(string serialNumber, [FromBody] UpdateSerialStatusRequest request)
    {
        try
        {
            var result = await _mediator.Send(new UpdateSerialStatusCommand { SerialNumber = serialNumber, Action = request.Action });
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

public class UpdateSerialStatusRequest
{
    public SerialAction Action { get; set; }
}
