using ERP.MST.Application.Internal;
using ERP.Shared.Authorization.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

/// <summary>Service-to-service: ManufacturingService checks the employees it puts into crews (service token, the caller's tenant). Number, name and employed flag only.</summary>
[ApiController]
[Route("internal/v1/employees")]
public class InternalEmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalEmployeesController(IMediator mediator) => _mediator = mediator;

    public class LookupRequest
    {
        public List<Guid> Ids { get; set; } = new();
    }

    [HttpPost("lookup")]
    [AuthorizeInternalService("manufacturing")]
    public async Task<IActionResult> Lookup([FromBody] LookupRequest request, CancellationToken ct)
    {
        if (request.Ids.Count > 200) return BadRequest(new { message = "At most 200 employees per call." });
        return Ok(new { items = await _mediator.Send(new LookupEmployeesQuery(request.Ids), ct) });
    }
}
