namespace ERP.PRD.Infrastructure.Search
{
    /// <summary>
    /// Derived, read-optimized document indexed into Elasticsearch's "products" index.
    /// SQL (ProductDbContext) remains the system-of-record; this document is rebuildable at
    /// any time from it via ElasticsearchProductIndexer/SearchIndexProcessor.
    /// Attribute values are stored with their natively-typed CLR value (decimal/bool/
    /// DateTime/string) rather than always as string, so Elasticsearch's default dynamic
    /// mapping infers a filterable/aggregatable field type per attribute code automatically.
    /// </summary>
    public class ProductSearchDocument
    {
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool HasImage { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
        public List<ProductVariantSearchDocument> Variants { get; set; } = new();
    }

    public class ProductVariantSearchDocument
    {
        public Guid VariantId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
    }
}
