using ERP.Shared.Authorization.ServiceAuth;
using ERP.WH.Application.Queries.Warehouses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.WH.API.Controllers;

/// <summary>Service-to-service: ManufacturingService checks the warehouses a plant is configured with (service token, the caller's tenant). Identity and active flag only.</summary>
[ApiController]
[Route("internal/v1/warehouses")]
public class InternalWarehousesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalWarehousesController(IMediator mediator) => _mediator = mediator;

    public class LookupRequest
    {
        public List<Guid> Ids { get; set; } = new();
    }

    [HttpPost("lookup")]
    [AuthorizeInternalService("manufacturing")]
    public async Task<IActionResult> Lookup([FromBody] LookupRequest request, CancellationToken ct)
    {
        if (request.Ids.Count > 50) return BadRequest(new { message = "At most 50 warehouses per call." });
        return Ok(new { items = await _mediator.Send(new LookupWarehousesQuery { Ids = request.Ids }, ct) });
    }
}
