using ERP.Shared.Authorization.ServiceAuth;
using ERP.WH.Application.Queries.Inventory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WH.API.Controllers;

/// <summary>
/// Service-to-service inventory reads (internal network only, service token required — never
/// a user token or an anonymous caller). The token's tenant is the request tenant.
/// </summary>
[ApiController]
[Route("internal/v1/inventory")]
public class InternalInventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalInventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Stock availability for SaleOrderService's pre-check before charging payment
    /// (CreateOrderCommandHandler). Minimal on purpose: no product name, no reorder levels.
    /// </summary>
    [HttpGet("{productId}/availability")]
    [AuthorizeInternalService("salesorder")]
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
}
