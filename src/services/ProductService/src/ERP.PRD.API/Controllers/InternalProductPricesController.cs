using ERP.PRD.Application.Features.Prices;
using ERP.Shared.Authorization.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers;

/// <summary>Service-to-service: SaleOrderService asks what products sell for at retail, so an order line is priced by the server and never by the client (R113). Price only - no cost, no name.</summary>
[ApiController]
[Route("internal/v1/product-prices")]
public class InternalProductPricesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalProductPricesController(IMediator mediator) => _mediator = mediator;

    public class PricesRequest
    {
        public List<Guid> ProductIds { get; set; } = new();
    }

    [HttpPost]
    [AuthorizeInternalService("salesorder")]
    public async Task<IActionResult> Get([FromBody] PricesRequest request, CancellationToken ct)
    {
        if (request.ProductIds.Count > 200) return BadRequest(new { message = "At most 200 products per call." });
        return Ok(new { prices = await _mediator.Send(new GetProductRetailPricesQuery { ProductIds = request.ProductIds }, ct) });
    }
}
