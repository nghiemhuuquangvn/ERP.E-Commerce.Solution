using ERP.PRD.Application.Interfaces;
using MediatR;

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
        private readonly IProductUnitOfWork _uow;

        public GetProductReviewsHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ProductReviewsResult> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken)
        {
            var (items, total, average) = await _uow.Reviews.GetPagedByProductIdAsync(
                request.ProductId, request.Skip, request.Take, cancellationToken);

            return new ProductReviewsResult
            {
                Items = items.Select(r => new ProductReviewDto
                {
                    Id = r.Id,
                    CustomerId = r.CustomerId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                }).ToList(),
                TotalCount = total,
                AverageRating = Math.Round(average, 2)
            };
        }
    }
}
