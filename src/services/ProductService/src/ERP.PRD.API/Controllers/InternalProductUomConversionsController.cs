using ERP.PRD.Application.Features.Lookup;
using ERP.Shared.Authorization.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers;

/// <summary>Service-to-service: ManufacturingService reads the per-product unit conversions of the components of a bill of materials (service token, the caller's tenant).</summary>
[ApiController]
[Route("internal/v1/product-uom-conversions")]
public class InternalProductUomConversionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalProductUomConversionsController(IMediator mediator) => _mediator = mediator;

    public class LookupRequest
    {
        public List<Guid> ProductIds { get; set; } = new();
    }

    [HttpPost("lookup")]
    [AuthorizeInternalService("manufacturing")]
    public async Task<IActionResult> Lookup([FromBody] LookupRequest request, CancellationToken ct)
    {
        if (request.ProductIds.Count > 200) return BadRequest(new { message = "At most 200 products per call." });
        return Ok(new { items = await _mediator.Send(new GetProductUomConversionsLookupQuery { ProductIds = request.ProductIds }, ct) });
    }
}
