using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.UomConversions.Queries
{
    public class GetProductUomConversionsQuery : IRequest<List<ProductUomConversionDto>>
    {
        public Guid ProductId { get; set; }
    }

    public class ProductUomConversionDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid FromUomId { get; set; }
        public Guid ToUomId { get; set; }
        public decimal ConversionFactor { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetProductUomConversionsHandler : IRequestHandler<GetProductUomConversionsQuery, List<ProductUomConversionDto>>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<GetProductUomConversionsHandler> _logger;

        public GetProductUomConversionsHandler(IProductUnitOfWork uow, ILogger<GetProductUomConversionsHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<List<ProductUomConversionDto>> Handle(
            GetProductUomConversionsQuery request, CancellationToken cancellationToken)
        {
            var productExists = await _uow.Products.ExistsAsync(request.ProductId, cancellationToken);
            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var conversions = await _uow.UomConversions.GetByProductIdAsync(request.ProductId, cancellationToken);

            var dtos = conversions.Select(c => new ProductUomConversionDto
            {
                Id = c.Id,
                ProductId = c.ProductId,
                FromUomId = c.FromUomId,
                ToUomId = c.ToUomId,
                ConversionFactor = c.ConversionFactor,
                CreatedAt = c.CreatedAt
            }).ToList();

            _logger.LogInformation("Retrieved {Count} UoM conversions for product {ProductId}", dtos.Count, request.ProductId);
            return dtos;
        }
    }
}
