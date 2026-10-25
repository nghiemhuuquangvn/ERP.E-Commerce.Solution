using ERP.MST.Application.Queries;
using ERP.Shared.Authorization.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

/// <summary>
/// Service-to-service exchange-rate reads (internal network only, service token required). The token's tenant is the request
/// tenant, so the rate is the one the calling tenant entered (ROADMAP P1-4: Accounting suggests it on a foreign document).
/// </summary>
[ApiController]
[Route("internal/v1/exchange-rates")]
public class InternalExchangeRatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalExchangeRatesController(IMediator mediator) => _mediator = mediator;

    /// <summary>The rate converting one unit of <c>from</c> into <c>to</c> on <c>asOf</c> (404 when none is entered).</summary>
    [HttpGet("effective")]
    [AuthorizeInternalService("accounting")]
    public async Task<IActionResult> Effective([FromQuery] string from, [FromQuery] string to, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return BadRequest(new { message = "from and to are required currency codes." });

        var rate = await _mediator.Send(new GetEffectiveRateByCodeQuery(from, to, asOf ?? DateTime.UtcNow), ct);
        return rate is null ? NotFound() : Ok(rate);
    }
}
