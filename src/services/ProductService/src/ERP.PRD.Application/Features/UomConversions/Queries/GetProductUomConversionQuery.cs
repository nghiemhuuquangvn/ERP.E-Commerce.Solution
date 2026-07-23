using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.UomConversions.Queries
{
    public class GetProductUomConversionQuery : IRequest<ProductUomConversionDto>
    {
        public Guid ProductId { get; set; }
        public Guid UomConversionId { get; set; }
    }

    public class GetProductUomConversionHandler : IRequestHandler<GetProductUomConversionQuery, ProductUomConversionDto>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<GetProductUomConversionHandler> _logger;

        public GetProductUomConversionHandler(IProductUnitOfWork uow, ILogger<GetProductUomConversionHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<ProductUomConversionDto> Handle(
            GetProductUomConversionQuery request, CancellationToken cancellationToken)
        {
            var conversion = await _uow.UomConversions.GetByIdAsync(request.UomConversionId, cancellationToken);
            if (conversion == null || conversion.ProductId != request.ProductId)
                throw new KeyNotFoundException(
                    $"UoM conversion with ID {request.UomConversionId} not found for product {request.ProductId}");

            _logger.LogInformation("Retrieved UoM conversion {UomConversionId}", request.UomConversionId);

            return new ProductUomConversionDto
            {
                Id = conversion.Id,
                ProductId = conversion.ProductId,
                FromUomId = conversion.FromUomId,
                ToUomId = conversion.ToUomId,
                ConversionFactor = conversion.ConversionFactor,
                CreatedAt = conversion.CreatedAt
            };
        }
    }
}
