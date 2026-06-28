using ERP.RPT.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.RPT.API.Controllers;

[ApiController]
[Route("api/v1/reports")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("sales")]
    public async Task<IActionResult> GetSalesDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSalesDashboardQuery
        {
            From = from ?? DateTime.UtcNow.AddDays(-30),
            To = to ?? DateTime.UtcNow
        }, ct);
        return Ok(result);
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPaymentSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPaymentSummaryQuery
        {
            From = from ?? DateTime.UtcNow.AddDays(-30),
            To = to ?? DateTime.UtcNow
        }, ct);
        return Ok(result);
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventoryReport(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetInventoryReportQuery(), ct));
}
