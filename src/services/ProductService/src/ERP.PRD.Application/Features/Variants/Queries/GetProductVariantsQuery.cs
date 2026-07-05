using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Variants.Queries
{
    public class GetProductVariantsQuery : IRequest<List<ProductVariantDto>>
    {
        public Guid ProductId { get; set; }
    }

    public class ProductVariantDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public decimal Cost { get; set; }
        public decimal? Weight { get; set; }
        public string? Dimensions { get; set; }
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public string? DimensionUnit { get; set; }
        public string? WeightUnit { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class GetProductVariantsHandler : IRequestHandler<GetProductVariantsQuery, List<ProductVariantDto>>
    {
        private readonly IProductUnitOfWork _uow;

        public GetProductVariantsHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<List<ProductVariantDto>> Handle(GetProductVariantsQuery request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithVariantsAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            return product.Variants
                .OrderBy(v => v.Sku)
                .Select(v => new ProductVariantDto
                {
                    Id = v.Id,
                    ProductId = v.ProductId,
                    Sku = v.Sku,
                    Barcode = v.Barcode,
                    Cost = v.Cost,
                    Weight = v.Weight,
                    Dimensions = v.Dimensions,
                    Length = v.Length,
                    Width = v.Width,
                    Height = v.Height,
                    DimensionUnit = v.DimensionUnit,
                    WeightUnit = v.WeightUnit,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt
                })
                .ToList();
        }
    }
}
