namespace ERP.PRD.Domain.Search
{
    /// <summary>
    /// Executes a ProductSearchCriteria against the derived Elasticsearch "products" index.
    /// Implemented in Infrastructure (ElasticsearchProductSearchService). See
    /// IProductSearchIndexer for why this lives in Domain rather than Application.
    /// </summary>
    public interface IProductSearchService
    {
        Task<ProductSearchResultDto> SearchAsync(ProductSearchCriteria criteria, CancellationToken cancellationToken);
    }
}
