using ERP.LOG.Application.Interfaces;
using ERP.LOG.Domain.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Configuration;

namespace ERP.LOG.Infrastructure.Search;

/// <summary>
/// Reads/writes the "service-logs-{yyyy.MM}" monthly rolling index family. Writes target the
/// index matching the entry's OccurredAt month; queries target the "service-logs-*" pattern so
/// a date-range spanning month boundaries still returns correct results.
/// </summary>
public class ElasticsearchLogRepository : ILogQueryRepository
{
    private const string IndexPattern = "service-logs-*";

    private readonly ElasticsearchClient _client;
    private readonly string _indexPrefix;

    public ElasticsearchLogRepository(ElasticsearchClient client, IConfiguration configuration)
    {
        _client = client;
        _indexPrefix = configuration.GetValue<string>("Elasticsearch:IndexPrefix") ?? "service-logs";
    }

    public async Task IndexAsync(LogEntry entry, CancellationToken cancellationToken)
    {
        var indexName = $"{_indexPrefix}-{entry.OccurredAt:yyyy.MM}";
        await _client.IndexAsync(entry, indexName, entry.Id, cancellationToken);
    }

    public async Task<LogQueryResult> QueryAsync(LogQueryCriteria criteria, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Max(1, Math.Min(criteria.PageSize, 200));

        var mustClauses = BuildMustClauses(criteria);

        var response = await _client.SearchAsync<LogEntry>(IndexPattern, s =>
        {
            s.Query(q => q.Bool(b => b.Must(mustClauses.ToArray())))
             .Sort(so => so.Field("occurredAt", f => f.Order(SortOrder.Desc)))
             .From((page - 1) * pageSize)
             .Size(pageSize);
        }, cancellationToken);

        if (!response.IsValidResponse)
        {
            return new LogQueryResult();
        }

        return new LogQueryResult
        {
            Items = response.Documents.ToList(),
            TotalCount = (int)response.Total
        };
    }

    private static List<Action<QueryDescriptor<LogEntry>>> BuildMustClauses(LogQueryCriteria criteria)
    {
        var mustClauses = new List<Action<QueryDescriptor<LogEntry>>>();

        if (!string.IsNullOrWhiteSpace(criteria.ServiceName))
        {
            var serviceName = criteria.ServiceName;
            mustClauses.Add(m => m.Term(t => t.Field("serviceName").Value(serviceName)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Level))
        {
            var level = criteria.Level;
            mustClauses.Add(m => m.Term(t => t.Field("level").Value(level)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Kind))
        {
            var kind = criteria.Kind;
            mustClauses.Add(m => m.Term(t => t.Field("kind").Value(kind)));
        }

        if (criteria.FromUtc.HasValue || criteria.ToUtc.HasValue)
        {
            var from = criteria.FromUtc;
            var to = criteria.ToUtc;
            mustClauses.Add(m => m.Range(r => r.DateRange(dr =>
            {
                dr.Field("occurredAt");
                if (from.HasValue) dr.Gte(DateMath.Anchored(from.Value));
                if (to.HasValue) dr.Lte(DateMath.Anchored(to.Value));
            })));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword;
            mustClauses.Add(m => m.Bool(b => b.Should(
                s => s.Match(mm => mm.Field("message").Query(keyword)),
                s => s.Match(mm => mm.Field("exceptionType").Query(keyword)),
                s => s.Match(mm => mm.Field("stackTrace").Query(keyword)))));
        }

        if (mustClauses.Count == 0)
        {
            mustClauses.Add(m => m.MatchAll(new MatchAllQuery()));
        }

        return mustClauses;
    }
}
