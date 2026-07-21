using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using ERP.Shared.Authorization;
using ERP.WH.Application.Queries.Lots;

namespace ERP.WH.API.Controllers;

[ApiController]
[Route("api/v1/inventory/lots")]
[Authorize]
public class LotsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContextService;

    public LotsController(IMediator mediator, IUserContextService userContextService)
    {
        _mediator = mediator;
        _userContextService = userContextService;
    }

    [HttpGet("product/{productId}")]
    [ProducesResponseType(typeof(List<LotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LotDto>>> GetByProduct(Guid productId, [FromQuery] bool includeDepleted = false)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var result = await _mediator.Send(new GetLotsByProductQuery { ProductId = productId, IncludeDepleted = includeDepleted });
        return Ok(result);
    }

    [HttpGet("expiring")]
    [ProducesResponseType(typeof(List<LotDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LotDto>>> GetExpiring([FromQuery] int withinDays = 30)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var result = await _mediator.Send(new GetExpiringLotsQuery { WithinDays = withinDays });
        return Ok(result);
    }
}
