using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Entities;

/// <summary>Supplier evaluation/rating — the ❌ list's "Supplier evaluation / rating" item, previously entirely absent.</summary>
public class SupplierRating : BaseEntity
{
    public Guid SupplierId { get; private set; }
    public int Rating { get; private set; } // 1-5
    public string? Comment { get; private set; }
    public string RatedBy { get; private set; } = string.Empty;

    private SupplierRating() { }

    public static SupplierRating Create(Guid supplierId, Guid tenantId, int rating, string? comment, string ratedBy)
    {
        if (supplierId == Guid.Empty)
            throw new ArgumentException("Supplier ID is required", nameof(supplierId));

        if (rating is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5", nameof(rating));

        return new SupplierRating
        {
            SupplierId = supplierId,
            TenantId = tenantId,
            Rating = rating,
            Comment = comment,
            RatedBy = ratedBy,
            CreatedAt = DateTime.UtcNow
        };
    }
}
