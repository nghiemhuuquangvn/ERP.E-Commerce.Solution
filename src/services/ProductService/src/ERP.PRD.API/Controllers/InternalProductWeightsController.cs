using ERP.PRD.Application.Features.Weights;
using ERP.Shared.Authorization.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers;

/// <summary>Service-to-service: ShippingService asks how much a parcel's products weigh (service token, the order's tenant). Weight only - no price, no name.</summary>
[ApiController]
[Route("internal/v1/product-weights")]
public class InternalProductWeightsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalProductWeightsController(IMediator mediator) => _mediator = mediator;

    public class WeightsRequest
    {
        public List<Guid> ProductIds { get; set; } = new();
    }

    [HttpPost]
    [AuthorizeInternalService("shipping")]
    public async Task<IActionResult> Get([FromBody] WeightsRequest request, CancellationToken ct)
    {
        if (request.ProductIds.Count > 200) return BadRequest(new { message = "At most 200 products per call." });
        return Ok(new { weights = await _mediator.Send(new GetProductWeightsQuery { ProductIds = request.ProductIds }, ct) });
    }
}
