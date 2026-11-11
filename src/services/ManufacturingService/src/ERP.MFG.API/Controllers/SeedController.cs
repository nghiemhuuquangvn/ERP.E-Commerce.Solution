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

    /// <summary>
    /// Plays one production-order cycle against the Warehouse's internal stock API (reserve, issue, return, scrap, receipt under QC hold, release, block, reverse)
    /// to prove the service-to-service contract on a live stack. Changes stock of the products named, writes nothing in Manufacturing. Same switch as DS-01.
    /// </summary>
    [HttpPost("stock-cycle")]
    [AuthorizePermission(PermissionConstants.Manufacturing_SettingsManage)]
    public async Task<IActionResult> StockCycle([FromBody] StockCycleRequest body, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Manufacturing:EnableDemoSeed")) return NotFound();
        return Ok(await mediator.Send(new RunStockCycleCommand(body), ct));
    }

    /// <summary>
    /// Warehouse W7 check through the real internal stock API: a held lot is split by a partial move, the sub-lot is released alone, the held rest is issued
    /// for rework (only with the flag) and a multi-lot reservation issue answers its lots. Changes stock of the products named, writes nothing in Manufacturing. Same switch as DS-01.
    /// </summary>
    [HttpPost("lot-cycle")]
    [AuthorizePermission(PermissionConstants.Manufacturing_SettingsManage)]
    public async Task<IActionResult> LotCycle([FromBody] LotCycleRequest body, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Manufacturing:EnableDemoSeed")) return NotFound();
        return Ok(await mediator.Send(new RunLotCycleCommand(body), ct));
    }
}
