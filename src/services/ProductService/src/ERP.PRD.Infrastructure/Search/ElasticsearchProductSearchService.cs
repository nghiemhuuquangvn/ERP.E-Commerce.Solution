using ERP.PRD.Domain.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Configuration;

namespace ERP.PRD.Infrastructure.Search
{
    /// <summary>
    /// Executes a ProductSearchCriteria against the derived Elasticsearch "products" index:
    /// free-text match on name/description, term filters on category/attributes, range
    /// filters on price/numeric attributes, plus a terms aggregation per requested facet code.
    /// Deliberately simple — a working, correct query DSL for the documented filters, not a
    /// fully generic search framework.
    /// </summary>
    public class ElasticsearchProductSearchService : IProductSearchService
    {
        private readonly ElasticsearchClient _client;
        private readonly string _indexName;

        public ElasticsearchProductSearchService(ElasticsearchClient client, IConfiguration configuration)
        {
            _client = client;
            _indexName = configuration.GetValue<string>("Elasticsearch:IndexName") ?? "products";
        }

        public async Task<ProductSearchResultDto> SearchAsync(ProductSearchCriteria query, CancellationToken cancellationToken)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Max(1, Math.Min(query.PageSize, 100));

            var mustClauses = BuildMustClauses(query);
            var facetCodes = query.FacetCodes ?? new List<string>();

            var response = await _client.SearchAsync<ProductSearchDocument>(_indexName, s =>
            {
                s.Query(q => q.Bool(b => b.Must(mustClauses.ToArray())))
                 .From((page - 1) * pageSize)
                 .Size(pageSize);

                if (facetCodes.Count > 0)
                {
                    s.Aggregations(a =>
                    {
                        foreach (var code in facetCodes)
                        {
                            var field = $"attributes.{code}";
                            a.Add(code, agg => agg.Terms(t => t.Field(field).Size(50)));
                        }
                        return a;
                    });
                }
            }, cancellationToken);

            if (!response.IsValidResponse)
            {
                return new ProductSearchResultDto();
            }

            var items = response.Documents.Select(MapHit).ToList();

            var facets = new List<AttributeFacetDto>();
            foreach (var code in facetCodes)
            {
                var termsAgg = response.Aggregations?.GetStringTerms(code);
                if (termsAgg == null)
                    continue;

                facets.Add(new AttributeFacetDto
                {
                    Code = code,
                    Values = termsAgg.Buckets.Select(bucket => new FacetValueDto
                    {
                        Value = bucket.Key.ToString() ?? string.Empty,
                        Count = (int)bucket.DocCount
                    }).ToList()
                });
            }

            return new ProductSearchResultDto
            {
                Items = items,
                TotalCount = (int)response.Total,
                Facets = facets
            };
        }

        private static List<Action<QueryDescriptor<ProductSearchDocument>>> BuildMustClauses(ProductSearchCriteria query)
        {
            var mustClauses = new List<Action<QueryDescriptor<ProductSearchDocument>>>();

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var text = query.Q;
                mustClauses.Add(m => m.Bool(b => b.Should(
                    s => s.Match(mm => mm.Field(new Field("name")).Query(text)),
                    s => s.Match(mm => mm.Field(new Field("description")).Query(text)))));
            }

            if (query.CategoryId.HasValue)
            {
                var categoryId = query.CategoryId.Value.ToString();
                mustClauses.Add(m => m.Term(t => t.Field(new Field("categoryId")).Value(categoryId)));
            }

            if (query.PriceMin.HasValue || query.PriceMax.HasValue)
            {
                var priceMin = query.PriceMin.HasValue ? (double?)query.PriceMin.Value : null;
                var priceMax = query.PriceMax.HasValue ? (double?)query.PriceMax.Value : null;

                mustClauses.Add(m => m.Range(r => r.NumberRange(nr =>
                {
                    nr.Field(new Field("priceMin"));
                    if (priceMin.HasValue) nr.Gte(priceMin);
                    if (priceMax.HasValue) nr.Lte(priceMax);
                })));
            }

            if (query.AttributeFilters != null)
            {
                foreach (var (code, value) in query.AttributeFilters)
                {
                    var field = $"attributes.{code}";
                    mustClauses.Add(m => m.Term(t => t.Field(new Field(field)).Value(value)));
                }
            }

            if (query.AttributeRangeFilters != null)
            {
                foreach (var (code, range) in query.AttributeRangeFilters)
                {
                    var field = $"attributes.{code}";
                    var gte = range.Min.HasValue ? (double?)range.Min.Value : null;
                    var lte = range.Max.HasValue ? (double?)range.Max.Value : null;

                    mustClauses.Add(m => m.Range(r => r.NumberRange(nr =>
                    {
                        nr.Field(new Field(field));
                        if (gte.HasValue) nr.Gte(gte);
                        if (lte.HasValue) nr.Lte(lte);
                    })));
                }
            }

            if (mustClauses.Count == 0)
            {
                mustClauses.Add(m => m.MatchAll(new Elastic.Clients.Elasticsearch.QueryDsl.MatchAllQuery()));
            }

            return mustClauses;
        }

        private static ProductSearchHitDto MapHit(ProductSearchDocument doc)
        {
            return new ProductSearchHitDto
            {
                ProductId = doc.ProductId,
                Sku = doc.Sku,
                Name = doc.Name,
                CategoryName = doc.CategoryName,
                PriceMin = doc.PriceMin,
                PriceMax = doc.PriceMax,
                HasImage = doc.HasImage,
                Attributes = doc.Attributes
            };
        }
    }
}
