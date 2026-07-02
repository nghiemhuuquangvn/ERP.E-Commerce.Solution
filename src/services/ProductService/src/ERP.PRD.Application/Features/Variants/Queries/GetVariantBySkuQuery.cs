using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Variants.Queries
{
    public class GetVariantBySkuQuery : IRequest<ProductVariantDto>
    {
        public string Sku { get; set; } = string.Empty;
    }

    public class GetVariantBySkuHandler : IRequestHandler<GetVariantBySkuQuery, ProductVariantDto>
    {
        private readonly ProductDbContext _context;

        public GetVariantBySkuHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<ProductVariantDto> Handle(GetVariantBySkuQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required");

            var sku = request.Sku.ToUpper().Trim();

            // Search all products for variant with matching SKU
            var allProducts = await _context.Products
                .Include(p => p.Variants)
                .Where(x => !x.IsDeleted)
                .ToListAsync(cancellationToken);

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
