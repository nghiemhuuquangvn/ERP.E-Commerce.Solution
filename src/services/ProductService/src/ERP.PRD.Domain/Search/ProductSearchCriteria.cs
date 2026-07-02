namespace ERP.PRD.Domain.Search
{
    /// <summary>
    /// Plain (non-MediatR) search criteria consumed by IProductSearchService. The
    /// Application-layer MediatR request (Features.Search.Queries.SearchProductsQuery) is
    /// mapped into this shape by its handler — kept separate so this Domain-level interface
    /// doesn't need to depend on Application/MediatR types.
    /// </summary>
    public class ProductSearchCriteria
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
}
