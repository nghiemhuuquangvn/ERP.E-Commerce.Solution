using MediatR;

namespace ERP.PRD.Application.Features.Variants.Queries
{
    public class GetVariantBySkuQuery : IRequest<ProductVariantDto>
    {
        public string Sku { get; set; } = string.Empty;
    }

    public class GetVariantBySkuHandler : IRequestHandler<GetVariantBySkuQuery, ProductVariantDto>
    {
        private readonly IRepository<Domain.Entities.Product> _productRepository;

        public GetVariantBySkuHandler(IRepository<Domain.Entities.Product> productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<ProductVariantDto> Handle(GetVariantBySkuQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required");

            var sku = request.Sku.ToUpper().Trim();

            // Search all products for variant with matching SKU
            var allProducts = await _productRepository.GetAllAsync(
                x => !x.IsDeleted,
                cancellationToken
            );

            var product = allProducts.FirstOrDefault(p => p.Variants.Any(v => v.Sku == sku));

            if (product == null)
                throw new KeyNotFoundException($"Variant with SKU '{request.Sku}' not found");

            var variant = product.GetVariantBySku(sku)
                ?? throw new KeyNotFoundException($"Variant with SKU '{request.Sku}' not found");

            return new ProductVariantDto
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                Sku = variant.Sku,
                Barcode = variant.Barcode,
                Cost = variant.Cost,
                Weight = variant.Weight,
                Dimensions = variant.Dimensions,
                CreatedAt = variant.CreatedAt,
                UpdatedAt = variant.UpdatedAt
            };
        }
    }
}
