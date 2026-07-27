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
[Route("api/v{version:apiVersion}/inventory/valuation/snapshots")]
[Authorize]
public class InventoryValuationSnapshotsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContextService;

    public InventoryValuationSnapshotsController(IMediator mediator, IUserContextService userContextService)
    {
        _mediator = mediator;
        _userContextService = userContextService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(InventoryValuationSnapshotDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InventoryValuationSnapshotDto>> Close([FromBody] CloseInventoryValuationSnapshotCommand command)
    {
        if (!_userContextService.HasPermission("inventory:valuation:close"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetByPeriod), new { period = result.PeriodCode }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{period}")]
    [ProducesResponseType(typeof(InventoryValuationSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryValuationSnapshotDto>> GetByPeriod(string period, [FromQuery] Guid? warehouseId = null)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new GetInventoryValuationSnapshotQuery { PeriodCode = period, WarehouseId = warehouseId });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
