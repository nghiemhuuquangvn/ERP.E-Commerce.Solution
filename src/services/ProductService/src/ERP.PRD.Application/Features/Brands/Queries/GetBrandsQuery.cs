using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Brands.Queries
{
    public class GetBrandsQuery : IRequest<List<BrandDto>>
    {
        public bool? IsActive { get; set; }
    }

    public class BrandDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetBrandsHandler : IRequestHandler<GetBrandsQuery, List<BrandDto>>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<GetBrandsHandler> _logger;

        public GetBrandsHandler(IProductUnitOfWork uow, ILogger<GetBrandsHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<List<BrandDto>> Handle(GetBrandsQuery request, CancellationToken cancellationToken)
        {
            var brands = await _uow.Brands.GetAllAsync(request.IsActive, cancellationToken);

            var dtos = brands.Select(b => new BrandDto
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                LogoUrl = b.LogoUrl,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt
            }).ToList();

            _logger.LogInformation("Retrieved {Count} brands", dtos.Count);
            return dtos;
        }
    }
}
