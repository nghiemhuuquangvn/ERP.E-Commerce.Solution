using ERP.MST.Application.Internal;
using ERP.Shared.Authorization.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

/// <summary>Service-to-service: ManufacturingService reads units of measure (decimal places) and converts quantities between units of one base (service token).</summary>
[ApiController]
[Route("internal/v1/units-of-measure")]
public class InternalUnitsOfMeasureController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalUnitsOfMeasureController(IMediator mediator) => _mediator = mediator;

    public class LookupRequest
    {
        public List<Guid> Ids { get; set; } = new();
    }

    public class ConvertRequest
    {
        public Guid FromUomId { get; set; }
        public Guid ToUomId { get; set; }
        public decimal Quantity { get; set; }
    }

    [HttpPost("lookup")]
    [AuthorizeInternalService("manufacturing")]
    public async Task<IActionResult> Lookup([FromBody] LookupRequest request, CancellationToken ct)
    {
        if (request.Ids.Count > 200) return BadRequest(new { message = "At most 200 units per call." });
        return Ok(new { items = await _mediator.Send(new LookupUnitsOfMeasureQuery(request.Ids), ct) });
    }

    public class CodeLookupRequest
    {
        public List<string> Codes { get; set; } = new();
    }

    [HttpPost("by-code")]
    [AuthorizeInternalService("manufacturing")]
    public async Task<IActionResult> ByCode([FromBody] CodeLookupRequest request, CancellationToken ct)
    {
        if (request.Codes.Count > 200) return BadRequest(new { message = "At most 200 codes per call." });
        return Ok(new { items = await _mediator.Send(new LookupUnitsByCodeQuery(request.Codes), ct) });
    }

    [HttpPost("convert")]
    [AuthorizeInternalService("manufacturing")]
    public async Task<IActionResult> Convert([FromBody] ConvertRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ConvertUnitQuery(request.FromUomId, request.ToUomId, request.Quantity), ct);
        return result is null ? NotFound(new { message = "Unit of measure not found." }) : Ok(result);
    }
}
