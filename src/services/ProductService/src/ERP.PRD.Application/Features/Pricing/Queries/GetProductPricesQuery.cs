using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Pricing.Queries
{
    public class GetProductPricesQuery : IRequest<List<VariantPriceDto>>
    {
        public Guid ProductId { get; set; }
    }

    public class VariantPriceDto
    {
        public Guid VariantId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class GetProductPricesHandler : IRequestHandler<GetProductPricesQuery, List<VariantPriceDto>>
    {
        private readonly ProductDbContext _context;
        private readonly ILogger<GetProductPricesHandler> _logger;

        public GetProductPricesHandler(ProductDbContext context, ILogger<GetProductPricesHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<VariantPriceDto>> Handle(GetProductPricesQuery request, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken: cancellationToken);
            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var prices = await _context.ProductVariants
                .Where(v => v.ProductId == request.ProductId)
                .OrderBy(v => v.Sku)
                .Select(v => new VariantPriceDto
                {
                    VariantId = v.Id,
                    Sku = v.Sku,
                    Cost = v.Cost,
                    UpdatedAt = v.UpdatedAt ?? v.CreatedAt
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved prices for product {ProductId}: {Count} variants", request.ProductId, prices.Count);
            return prices;
        }
    }
}
