using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Images.Queries
{
    public class GetProductImagesQuery : IRequest<List<ProductImageDto>>
    {
        public Guid ProductId { get; set; }
    }

    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public Guid? VariantId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetProductImagesHandler : IRequestHandler<GetProductImagesQuery, List<ProductImageDto>>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<GetProductImagesHandler> _logger;

        public GetProductImagesHandler(IProductUnitOfWork uow, ILogger<GetProductImagesHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<List<ProductImageDto>> Handle(GetProductImagesQuery request, CancellationToken cancellationToken)
        {
            var productExists = await _uow.Products.ExistsAsync(request.ProductId, cancellationToken);
            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var images = await _uow.Images.GetByProductIdAsync(request.ProductId, cancellationToken);

            var dtos = images.Select(i => new ProductImageDto
            {
                Id = i.Id,
                VariantId = i.VariantId,
                ImageUrl = i.ImageUrl,
                AltText = i.AltText,
                SortOrder = i.SortOrder,
                IsPrimary = i.IsPrimary,
                CreatedAt = i.CreatedAt
            }).ToList();

            _logger.LogInformation("Retrieved {Count} images for product {ProductId}", dtos.Count, request.ProductId);
            return dtos;
        }
    }
}
