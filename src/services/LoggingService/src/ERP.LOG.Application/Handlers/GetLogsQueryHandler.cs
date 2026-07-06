using ERP.LOG.Application.DTOs;
using ERP.LOG.Application.Interfaces;
using ERP.LOG.Application.Queries;
using MediatR;

namespace ERP.LOG.Application.Handlers;

public class GetLogsQueryHandler : IRequestHandler<GetLogsQuery, PagedResultDto<LogEntryDto>>
{
    private readonly ILogQueryRepository _repository;

    public GetLogsQueryHandler(ILogQueryRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<LogEntryDto>> Handle(GetLogsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Max(1, Math.Min(request.PageSize, 200));

        var result = await _repository.QueryAsync(new LogQueryCriteria
        {
            ServiceName = request.ServiceName,
            Level = request.Level,
            Kind = request.Kind,
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            Keyword = request.Keyword,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return new PagedResultDto<LogEntryDto>
        {
            Items = result.Items.Select(e => new LogEntryDto
            {
                Id = e.Id,
                ServiceName = e.ServiceName,
                Environment = e.Environment,
                Level = e.Level,
                Kind = e.Kind,
                Message = e.Message,
                ExceptionType = e.ExceptionType,
                StackTrace = e.StackTrace,
                CorrelationId = e.CorrelationId,
                Source = e.Source,
                Properties = e.Properties,
                OccurredAt = e.OccurredAt
            }).ToList(),
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
