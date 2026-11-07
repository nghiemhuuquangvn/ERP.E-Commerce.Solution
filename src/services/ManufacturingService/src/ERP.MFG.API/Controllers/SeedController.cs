using Asp.Versioning;
using ERP.MFG.Application.Features.Seeding;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// Demo data (DS-01). Off unless <c>Manufacturing:EnableDemoSeed</c> is true, so a production tenant cannot receive it: with the switch off the route does not exist (404).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/seed")]
public class SeedController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    public sealed record Ds01Body(string? PlantCode, string? SkuPrefix, Ds01Warehouses? Warehouses, Ds01Units? Units);

    [HttpPost("ds01")]
    [AuthorizePermission(PermissionConstants.Manufacturing_SettingsManage)]
    public async Task<IActionResult> Ds01([FromBody] Ds01Body body, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Manufacturing:EnableDemoSeed")) return NotFound();
        return Ok(await mediator.Send(new SeedDs01Command(body.PlantCode ?? "PL01", body.SkuPrefix ?? "", body.Warehouses, body.Units), ct));
    }
}
