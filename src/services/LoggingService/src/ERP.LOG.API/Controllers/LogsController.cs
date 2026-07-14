using ERP.LOG.Application.DTOs;
using ERP.LOG.Application.Interfaces;
using ERP.LOG.Application.Queries;
using ERP.LOG.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.LOG.API.Controllers;

/// <summary>
/// Client-submitted log entry (frontend apps don't have an EventId/OccurredAt yet — the
/// server assigns those on ingestion).
/// </summary>
public class ClientLogEntryDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Level { get; set; } = "Information";
    public string Kind { get; set; } = "Trace";
    public string Message { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
}

[ApiController]
[Route("api/v1/logs")]
public class LogsController : ControllerBase
{
    private const int MaxClientBatchSize = 100;

    private readonly IMediator _mediator;
    private readonly ILogQueryRepository _repository;

    public LogsController(IMediator mediator, ILogQueryRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    /// <summary>Authenticated query endpoint — gated by Bearer auth at the Ocelot Gateway route.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<LogEntryDto>>> GetLogs(
        [FromQuery] string? serviceName,
        [FromQuery] string? level,
        [FromQuery] string? kind,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? keyword,
        [FromQuery] string? requestId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLogsQuery
        {
            ServiceName = serviceName,
            Level = level,
            Kind = kind,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Keyword = keyword,
            RequestId = requestId,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Unauthenticated client-ingestion endpoint for frontend apps (Admin/Storefront/Mobile) —
    /// deliberately open so pre-login/session-expired errors are still captured. Rate-limited at
    /// the Ocelot Gateway route; batch size capped here as defense in depth.
    /// </summary>
    [HttpPost("client")]
    public async Task<IActionResult> ReportClientLogs(
        [FromBody] List<ClientLogEntryDto> entries,
        CancellationToken cancellationToken)
    {
        if (entries == null || entries.Count == 0)
        {
            return BadRequest("At least one log entry is required.");
        }

        if (entries.Count > MaxClientBatchSize)
        {
            return BadRequest($"A single batch cannot exceed {MaxClientBatchSize} entries.");
        }

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            await _repository.IndexAsync(new LogEntry
            {
                Id = Guid.NewGuid().ToString(),
                ServiceName = entry.ServiceName,
                Level = entry.Level,
                Kind = entry.Kind,
                Message = entry.Message,
                ExceptionType = entry.ExceptionType,
                StackTrace = entry.StackTrace,
                CorrelationId = entry.CorrelationId,
                Source = "Frontend",
                Properties = entry.Properties,
                OccurredAt = now,
                IndexedAt = now
            }, cancellationToken);
        }

        return Accepted();
    }
}
