using ERP.PRD.Domain.Search;
using MediatR;

namespace ERP.PRD.Application.Features.Search.Queries
{
    /// <summary>
    /// Faceted/filterable catalog search backed by the derived Elasticsearch "products" index
    /// (see ElasticsearchProductSearchService). Distinct from the older, simpler
    /// Features.Products.Queries.SearchProductsQuery (plain SQL LIKE match on name/SKU) — that
    /// query is left untouched; this one adds category/price/attribute filtering and facets.
    /// The MediatR request mirrors ProductSearchCriteria's shape; the handler maps into it
    /// rather than IProductSearchService depending on this Application/MediatR type directly
    /// (IProductSearchService lives in Domain — see its XML doc for why).
    /// </summary>
    public class SearchProductsQuery : IRequest<ProductSearchResultDto>
    {
        public string? Q { get; set; }
        public Guid? CategoryId { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }

        /// <summary>Exact-match attribute filters, keyed by attribute code.</summary>
        public Dictionary<string, string>? AttributeFilters { get; set; }

        /// <summary>Numeric range attribute filters, keyed by attribute code.</summary>
        public Dictionary<string, (decimal? Min, decimal? Max)>? AttributeRangeFilters { get; set; }

        /// <summary>Attribute codes to compute term facets for.</summary>
        public List<string>? FacetCodes { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>Thin CQRS pass-through to IProductSearchService, matching this codebase's
    /// existing style of delegating query handlers to a service where one exists.</summary>
    public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, ProductSearchResultDto>
    {
        private readonly IProductSearchService _searchService;

        public SearchProductsQueryHandler(IProductSearchService searchService)
        {
            _searchService = searchService;
        }

        public Task<ProductSearchResultDto> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
        {
            var criteria = new ProductSearchCriteria
            {
                Q = request.Q,
                CategoryId = request.CategoryId,
                PriceMin = request.PriceMin,
                PriceMax = request.PriceMax,
                AttributeFilters = request.AttributeFilters,
                AttributeRangeFilters = request.AttributeRangeFilters,
                FacetCodes = request.FacetCodes,
                Page = request.Page,
                PageSize = request.PageSize
            };

            return _searchService.SearchAsync(criteria, cancellationToken);
        }
    }
}
