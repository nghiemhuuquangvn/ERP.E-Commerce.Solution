using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Reviews.Queries
{
    public class GetProductReviewsQuery : IRequest<ProductReviewsResult>
    {
        public Guid ProductId { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 20;
    }

    public class ProductReviewDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProductReviewsResult
    {
        public List<ProductReviewDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public double AverageRating { get; set; }
    }

    public class GetProductReviewsHandler : IRequestHandler<GetProductReviewsQuery, ProductReviewsResult>
    {
        private readonly ProductDbContext _context;

        public GetProductReviewsHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<ProductReviewsResult> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken)
        {
            var baseQuery = _context.ProductReviews
                .Where(r => r.ProductId == request.ProductId && r.IsApproved);

            var totalCount = await baseQuery.CountAsync(cancellationToken);
            var averageRating = totalCount == 0 ? 0 : await baseQuery.AverageAsync(r => r.Rating, cancellationToken);

            var items = await baseQuery
                .OrderByDescending(r => r.CreatedAt)
                .Skip(request.Skip)
                .Take(request.Take)
                .Select(r => new ProductReviewDto
                {
                    Id = r.Id,
                    CustomerId = r.CustomerId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new ProductReviewsResult { Items = items, TotalCount = totalCount, AverageRating = Math.Round(averageRating, 2) };
        }
    }
}
