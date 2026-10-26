using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using ERP.Shared.Authorization;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Queries.Inventory;
using ERP.WH.Application.Services;

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
        [FromQuery] string? search = null,
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
            if (result == null)
                return BadRequest(new { message = "One or more line items could not be fully reserved." });

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

    /// <summary>Sets min / max / safety stock of one product (the replenishment policy, P1-9).</summary>
    [HttpPut("{productId}/stock-policy")]
    [AuthorizePermission("inventory:manage")]
    [ProducesResponseType(typeof(StockPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockPolicyResponse>> UpdateStockPolicy(Guid productId, [FromBody] UpdateStockPolicyCommand command)
    {
        command.ProductId = productId;
        try
        {
            return Ok(await _mediator.Send(command));
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

    /// <summary>Runs the replenishment scan now for the caller's tenant (the daily job does the same for every tenant).</summary>
    [HttpPost("replenishment/scan")]
    [AuthorizePermission("inventory:manage")]
    [ProducesResponseType(typeof(ReplenishmentScanResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReplenishmentScanResult>> ScanReplenishment([FromServices] IReplenishmentScanService scan, CancellationToken ct) =>
        Ok(await scan.ScanAsync(ct));

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

    /// <summary>
    /// Public stock availability for one product — the Storefront ChatBot's
    /// get_stock_availability tool calls it through the Gateway for signed-in customers and
    /// guests alike. [AllowAnonymous] and minimal (no product name, no reorder levels); the
    /// tenant comes from the caller's token or, for guests, the storefront host (Gateway).
    /// Service-to-service callers use internal/v1/inventory/{id}/availability instead.
    /// </summary>
    [HttpGet("{productId}/availability")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailability(Guid productId)
    {
        try
        {
            var result = await _mediator.Send(new GetInventoryQuery { ProductId = productId });
            return Ok(new { productId = result.ProductId, availableQuantity = result.AvailableQuantity });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{productId}/cost-consumption")]
    [ProducesResponseType(typeof(List<CostConsumptionLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CostConsumptionLogDto>>> GetCostConsumption(
        Guid productId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var result = await _mediator.Send(new GetCostConsumptionLogQuery
        {
            ProductId = productId,
            StartDate = startDate,
            EndDate = endDate
        });
        return Ok(result);
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
