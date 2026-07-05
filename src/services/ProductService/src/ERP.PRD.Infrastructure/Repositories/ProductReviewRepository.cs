using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Infrastructure.Repositories
{
    internal sealed class ProductReviewRepository : IProductReviewRepository
    {
        private readonly ProductDbContext _db;

        public ProductReviewRepository(ProductDbContext db) => _db = db;

        public async Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct = default)
            => await _db.Products.AnyAsync(p => p.Id == productId && !p.IsDeleted, ct);

        public async Task<(List<ProductReviewProjection> Items, int Total, double Average)>
            GetPagedByProductIdAsync(Guid productId, int skip, int take, CancellationToken ct = default)
        {
            var baseQuery = _db.ProductReviews
                .Where(r => r.ProductId == productId && r.IsApproved);

            var total = await baseQuery.CountAsync(ct);
            var average = total == 0 ? 0 : await baseQuery.AverageAsync(r => r.Rating, ct);

            var items = await baseQuery
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .Select(r => new ProductReviewProjection(r.Id, r.CustomerId, r.Rating, r.Comment, r.CreatedAt))
                .ToListAsync(ct);

            return (items, total, average);
        }

        public async Task AddAsync(ProductReview review, CancellationToken ct = default)
            => await _db.ProductReviews.AddAsync(review, ct);
    }
}
