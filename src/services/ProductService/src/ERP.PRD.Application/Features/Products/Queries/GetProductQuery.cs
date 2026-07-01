using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Products.Queries
{
    public class GetProductQuery : IRequest<ProductDto>
    {
        public Guid ProductId { get; set; }
        public string? LanguageCode { get; set; }
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ProductType { get; set; }
        public int Status { get; set; }
        public Guid? CategoryId { get; set; }
        public bool HasImage { get; set; }
        public int VariantCount { get; set; }
        public string LanguageCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class GetProductHandler : IRequestHandler<GetProductQuery, ProductDto>
    {
        private readonly ProductDbContext _context;

        public GetProductHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .Include(p => p.Translations)
                .FirstOrDefaultAsync(x => x.Id == request.ProductId && !x.IsDeleted, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            return ProductTranslationMapper.MapToDto(product, request.LanguageCode);
        }
    }
}
