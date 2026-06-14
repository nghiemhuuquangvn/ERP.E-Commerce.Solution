using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Images.Queries
{
    public class GetProductImagesQuery : IRequest<List<ProductImageDto>>
    {
        public Guid ProductId { get; set; }
    }

    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetProductImagesHandler : IRequestHandler<GetProductImagesQuery, List<ProductImageDto>>
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<GetProductImagesHandler> _logger;

        public GetProductImagesHandler(ProductDbContext context, ILogger<GetProductImagesHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ProductImageDto>> Handle(GetProductImagesQuery request, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken: cancellationToken);
            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var images = await _context.ProductImages
                .Where(i => i.ProductId == request.ProductId)
                .OrderBy(i => i.IsPrimary ? 0 : 1) // Primary first, then by sort order
                .ThenBy(i => i.SortOrder)
                .Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    AltText = i.AltText,
                    SortOrder = i.SortOrder,
                    IsPrimary = i.IsPrimary,
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {Count} images for product {ProductId}", images.Count, request.ProductId);
            return images;
        }
    }
}
