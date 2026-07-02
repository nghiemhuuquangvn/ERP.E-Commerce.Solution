using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Correlates a Product (and optionally a specific ProductVariant) with its identifier in
    /// an external system (e.g. a marketplace/channel SKU or a legacy-system ID), so sync jobs
    /// can look up which local record an external record maps to and vice versa.
    /// </summary>
    public class ProductExternalMapping : BaseEntity
    {
        public Guid ProductId { get; set; }
        public Guid? VariantId { get; set; }
        public string ExternalSystem { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public string? ExternalDataJson { get; set; }
        public DateTime? SyncedAt { get; set; }

        public static ProductExternalMapping Create(Guid productId, string externalSystem, string externalId, Guid? variantId = null) =>
            new()
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                VariantId = variantId,
                ExternalSystem = externalSystem,
                ExternalId = externalId,
                CreatedAt = DateTime.UtcNow
            };
    }
}
