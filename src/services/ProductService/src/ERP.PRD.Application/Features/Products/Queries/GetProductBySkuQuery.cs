using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class GetProductBySkuQuery : IRequest<ProductDto>
    {
        public string Sku { get; set; } = string.Empty;
        public string? LanguageCode { get; set; }
    }

    public class GetProductBySkuHandler : IRequestHandler<GetProductBySkuQuery, ProductDto>
    {
        private readonly ProductDbContext _context;

        public GetProductBySkuHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<ProductDto> Handle(GetProductBySkuQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sku))
                throw new ArgumentException("SKU is required");

            var product = await _context.Products
                .Include(p => p.Variants)
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(x => x.Sku == request.Sku.ToUpper() && !x.IsDeleted, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with SKU {request.Sku} not found");

            return ProductTranslationMapper.MapToDto(product, request.LanguageCode);
        }
    }
}
