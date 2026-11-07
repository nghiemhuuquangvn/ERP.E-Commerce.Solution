using ERP.PRD.Application.Features.Lookup;
using ERP.Shared.Authorization.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers;

/// <summary>Service-to-service: ManufacturingService checks the products it plans to make or use (service token, the caller's tenant). Identity and unit only.</summary>
[ApiController]
[Route("internal/v1/product-lookup")]
public class InternalProductLookupController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalProductLookupController(IMediator mediator) => _mediator = mediator;

    public class LookupRequest
    {
        public List<Guid> ProductIds { get; set; } = new();
    }

    public class SkuLookupRequest
    {
        public List<string> Skus { get; set; } = new();
    }

    [HttpPost("by-sku")]
    [AuthorizeInternalService("manufacturing")]
    public async Task<IActionResult> GetBySku([FromBody] SkuLookupRequest request, CancellationToken ct)
    {
        if (request.Skus.Count > 200) return BadRequest(new { message = "At most 200 SKUs per call." });
        return Ok(new { items = await _mediator.Send(new GetProductLookupBySkuQuery { Skus = request.Skus }, ct) });
    }

    [HttpPost]
    [AuthorizeInternalService("manufacturing")]
    public async Task<IActionResult> Get([FromBody] LookupRequest request, CancellationToken ct)
    {
        if (request.ProductIds.Count > 200) return BadRequest(new { message = "At most 200 products per call." });
        return Ok(new { items = await _mediator.Send(new GetProductLookupQuery { ProductIds = request.ProductIds }, ct) });
    }
}
