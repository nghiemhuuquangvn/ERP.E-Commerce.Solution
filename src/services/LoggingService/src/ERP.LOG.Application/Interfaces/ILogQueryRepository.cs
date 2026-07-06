using ERP.LOG.Domain.Models;

namespace ERP.LOG.Application.Interfaces;

public class LogQueryCriteria
{
    public string? ServiceName { get; set; }
    public string? Level { get; set; }
    public string? Kind { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class LogQueryResult
{
    public List<LogEntry> Items { get; set; } = new();
    public int TotalCount { get; set; }
}

public interface ILogQueryRepository
{
    Task<LogQueryResult> QueryAsync(LogQueryCriteria criteria, CancellationToken cancellationToken);

    Task IndexAsync(LogEntry entry, CancellationToken cancellationToken);
}
