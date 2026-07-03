using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// A customer's rating/comment for a Product. CustomerId is a loose Guid reference
    /// (no FK) to whichever service owns the customer identity (CRM/Identity) — same
    /// cross-service-reference-by-Id convention used everywhere else in this system,
    /// since a database can't hold a real foreign key into another service's database.
    /// </summary>
    public class ProductReview : BaseEntity
    {
        public Guid ProductId { get; private set; }
        public Guid CustomerId { get; private set; }
        public int Rating { get; private set; } // 1-5
        public string? Comment { get; private set; }
        public bool IsApproved { get; private set; } = true; // no moderation queue today — see Application layer note

        private ProductReview() { }

        public static ProductReview Create(Guid productId, Guid customerId, int rating, string? comment)
        {
            if (rating is < 1 or > 5)
                throw new ArgumentException("Rating must be between 1 and 5", nameof(rating));

            return new ProductReview
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                CustomerId = customerId,
                Rating = rating,
                Comment = comment?.Trim(),
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Hide()
        {
            IsApproved = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
