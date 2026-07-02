namespace ERP.PRD.Infrastructure.Persistence
{
    /// <summary>
    /// Internal processing-queue row — the ProductService-local counterpart to
    /// <see cref="ERP.Shared.Messaging.OutboxMessage"/>, but for the derived Elasticsearch
    /// "products" search index instead of Kafka. Not a domain concept (no audit columns,
    /// no soft-delete, no domain events): a plain row polled by SearchIndexProcessor, which
    /// rebuilds the ES document for ProductId and then stamps IndexedAt.
    /// Deduplicated by ProductId — ProductDbContext.SaveChangesAsync bumps RequestedAt on an
    /// existing unindexed row instead of inserting a new one for the same ProductId.
    /// </summary>
    public class SearchIndexQueueEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProductId { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? IndexedAt { get; set; }
        public string? Error { get; set; }
    }
}
