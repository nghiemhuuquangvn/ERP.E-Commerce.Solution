using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.WH.Application.Queries.Lots;

namespace ERP.WH.API.Controllers;

[ApiController]
[Route("api/v1/inventory/lots")]
public class LotsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LotsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("product/{productId}")]
    [ProducesResponseType(typeof(List<LotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LotDto>>> GetByProduct(Guid productId, [FromQuery] bool includeDepleted = false)
    {
        var result = await _mediator.Send(new GetLotsByProductQuery { ProductId = productId, IncludeDepleted = includeDepleted });
        return Ok(result);
    }

    [HttpGet("expiring")]
    [ProducesResponseType(typeof(List<LotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LotDto>>> GetExpiring([FromQuery] int withinDays = 30)
    {
        var result = await _mediator.Send(new GetExpiringLotsQuery { WithinDays = withinDays });
        return Ok(result);
    }
}
