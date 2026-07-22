using Asp.Versioning;
using ERP.RPT.Application.Interfaces;
using ERP.RPT.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ERP.RPT.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IReportingRepository _repository;
    private readonly UserContext _userContext;

    public ReportsController(IMediator mediator, IReportingRepository repository, UserContext userContext)
    {
        _mediator = mediator;
        _repository = repository;
        _userContext = userContext;
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSalesDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (!_userContext.HasPermission("reports:sales:view"))
            return Forbid();

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
        if (!_userContext.HasPermission("reports:payments:view"))
            return Forbid();

        var result = await _mediator.Send(new GetPaymentSummaryQuery
        {
            From = from ?? DateTime.UtcNow.AddDays(-30),
            To = to ?? DateTime.UtcNow
        }, ct);
        return Ok(result);
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventoryReport(CancellationToken ct)
    {
        if (!_userContext.HasPermission("reports:inventory:view"))
            return Forbid();

        return Ok(await _mediator.Send(new GetInventoryReportQuery(), ct));
    }

    [HttpGet("hr")]
    public async Task<IActionResult> GetHRDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (!_userContext.HasPermission("reports:hr:view"))
            return Forbid();

        var result = await _mediator.Send(new GetHRDashboardQuery
        {
            From = from ?? DateTime.UtcNow.AddMonths(-6),
            To = to ?? DateTime.UtcNow
        }, ct);
        return Ok(result);
    }

    [HttpGet("pos")]
    public async Task<IActionResult> GetPOSDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (!_userContext.HasPermission("reports:pos:view"))
            return Forbid();

        var result = await _mediator.Send(new GetPOSDashboardQuery
        {
            From = from ?? DateTime.UtcNow.AddDays(-30),
            To = to ?? DateTime.UtcNow
        }, ct);
        return Ok(result);
    }

    [HttpGet("sales/export")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportSalesCSV(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (!_userContext.HasPermission("reports:sales:view"))
            return Forbid();

        var orders = await _repository.GetSalesOrdersForExportAsync(
            from ?? DateTime.UtcNow.AddDays(-30),
            to ?? DateTime.UtcNow,
            ct);

        var sb = new StringBuilder();
        sb.AppendLine("OrderId,CustomerId,CustomerName,Status,TotalAmount,ItemCount,OrderDate,ApprovedAt,ShippedAt,DeliveredAt");
        foreach (var o in orders)
        {
            sb.AppendLine(string.Join(",",
                o.OrderId, o.CustomerId,
                $"\"{o.CustomerName.Replace("\"", "\"\"")}\"",
                o.Status, o.TotalAmount, o.ItemCount,
                o.OrderDate.ToString("o"),
                o.ApprovedAt?.ToString("o") ?? "",
                o.ShippedAt?.ToString("o") ?? "",
                o.DeliveredAt?.ToString("o") ?? ""));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        Response.Headers.Append("Content-Disposition", $"attachment; filename=sales_{DateTime.UtcNow:yyyyMMdd}.csv");
        return File(bytes, "text/csv");
    }

    [HttpGet("payments/export")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportPaymentsCSV(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (!_userContext.HasPermission("reports:payments:view"))
            return Forbid();

        var payments = await _repository.GetPaymentsForExportAsync(
            from ?? DateTime.UtcNow.AddDays(-30),
            to ?? DateTime.UtcNow,
            ct);

        var sb = new StringBuilder();
        sb.AppendLine("PaymentId,OrderId,Amount,Currency,Method,Status,ProcessedAt");
        foreach (var p in payments)
        {
            sb.AppendLine(string.Join(",",
                p.PaymentId, p.OrderId,
                p.Amount, p.Currency, p.Method, p.Status,
                p.ProcessedAt?.ToString("o") ?? ""));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        Response.Headers.Append("Content-Disposition", $"attachment; filename=payments_{DateTime.UtcNow:yyyyMMdd}.csv");
        return File(bytes, "text/csv");
    }
}
