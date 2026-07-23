using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Brands.Queries
{
    public class GetBrandQuery : IRequest<BrandDto>
    {
        public Guid BrandId { get; set; }
    }

    public class GetBrandHandler : IRequestHandler<GetBrandQuery, BrandDto>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<GetBrandHandler> _logger;

        public GetBrandHandler(IProductUnitOfWork uow, ILogger<GetBrandHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<BrandDto> Handle(GetBrandQuery request, CancellationToken cancellationToken)
        {
            var brand = await _uow.Brands.GetByIdAsync(request.BrandId, cancellationToken);
            if (brand == null)
                throw new KeyNotFoundException($"Brand with ID {request.BrandId} not found");

            _logger.LogInformation("Retrieved brand {BrandId}: {Name}", request.BrandId, brand.Name);

            return new BrandDto
            {
                Id = brand.Id,
                Code = brand.Code,
                Name = brand.Name,
                LogoUrl = brand.LogoUrl,
                IsActive = brand.IsActive,
                CreatedAt = brand.CreatedAt
            };
        }
    }
}
