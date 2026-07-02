namespace ERP.PRD.Domain.Search
{
    public class ProductSearchResultDto
    {
        public List<ProductSearchHitDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public List<AttributeFacetDto> Facets { get; set; } = new();
    }

    public class ProductSearchHitDto
    {
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }
        public bool HasImage { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
    }

    public class AttributeFacetDto
    {
        public string Code { get; set; } = string.Empty;
        public List<FacetValueDto> Values { get; set; } = new();
    }

    public class FacetValueDto
    {
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
