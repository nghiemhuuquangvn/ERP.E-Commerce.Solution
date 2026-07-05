using ERP.PRD.Application.Interfaces;
using MediatR;

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
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<GetProductPricesHandler> _logger;

        public GetProductPricesHandler(IProductUnitOfWork uow, ILogger<GetProductPricesHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<List<VariantPriceDto>> Handle(GetProductPricesQuery request, CancellationToken cancellationToken)
        {
            var productExists = await _uow.Products.ExistsAsync(request.ProductId, cancellationToken);
            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var prices = await _uow.Variants.GetPricesByProductIdAsync(request.ProductId, cancellationToken);

            var dtos = prices.Select(v => new VariantPriceDto
            {
                VariantId = v.VariantId,
                Sku = v.Sku,
                Cost = v.Cost,
                UpdatedAt = v.UpdatedAt
            }).ToList();

            _logger.LogInformation("Retrieved prices for product {ProductId}: {Count} variants",
                request.ProductId, dtos.Count);
            return dtos;
        }
    }
}
