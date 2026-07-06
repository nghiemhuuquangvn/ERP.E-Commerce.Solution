using ERP.LOG.Application.DTOs;
using MediatR;

namespace ERP.LOG.Application.Queries;

public class GetLogsQuery : IRequest<PagedResultDto<LogEntryDto>>
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
