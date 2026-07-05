using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Variants.Queries
{
    public class GetVariantBySkuQuery : IRequest<ProductVariantDto>
    {
        public string Sku { get; set; } = string.Empty;
    }

    public class GetVariantBySkuHandler : IRequestHandler<GetVariantBySkuQuery, ProductVariantDto>
    {
        private readonly IProductUnitOfWork _uow;

        public GetVariantBySkuHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ProductVariantDto> Handle(GetVariantBySkuQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required");

            var variant = await _uow.Variants.GetBySkuAsync(request.Sku, cancellationToken);

            if (variant == null)
                throw new KeyNotFoundException($"Variant with SKU '{request.Sku}' not found");

            return new ProductVariantDto
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                Sku = variant.Sku,
                Barcode = variant.Barcode,
                Cost = variant.Cost,
                Weight = variant.Weight,
                Dimensions = variant.Dimensions,
                Length = variant.Length,
                Width = variant.Width,
                Height = variant.Height,
                DimensionUnit = variant.DimensionUnit,
                WeightUnit = variant.WeightUnit,
                CreatedAt = variant.CreatedAt,
                UpdatedAt = variant.UpdatedAt
            };
        }
    }
}
