namespace ERP.PRD.Domain.Search
{
    /// <summary>
    /// Rebuilds the derived Elasticsearch "products" search document for a single Product
    /// (denormalizing its variants and resolved attribute values). Implemented in
    /// Infrastructure (ElasticsearchProductIndexer) and driven by SearchIndexProcessor polling
    /// SearchIndexQueue. SQL remains the system-of-record; the ES index is rebuildable.
    ///
    /// Lives in Domain (not Application) so both Infrastructure and Application can depend on
    /// it without a circular project reference: ERP.PRD.Application already has a direct
    /// ProjectReference to ERP.PRD.Infrastructure (existing convention — handlers use
    /// ProductDbContext directly), so Infrastructure cannot also reference Application.
    /// </summary>
    public interface IProductSearchIndexer
    {
        Task IndexAsync(Guid productId, CancellationToken cancellationToken);
    }
}
