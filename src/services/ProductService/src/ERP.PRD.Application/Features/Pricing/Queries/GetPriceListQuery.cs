using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Pricing.Queries
{
    public class GetPriceListQuery : IRequest<PriceListDetailDto>
    {
        public Guid PriceListId { get; set; }
    }

    public class PriceListDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public List<PriceListItemDto> Items { get; set; } = new();
    }

    public class PriceListItemDto
    {
        public Guid VariantId { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public decimal EffectivePrice { get; set; }
    }

    public class GetPriceListHandler : IRequestHandler<GetPriceListQuery, PriceListDetailDto>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<GetPriceListHandler> _logger;

        public GetPriceListHandler(IProductUnitOfWork uow, ILogger<GetPriceListHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<PriceListDetailDto> Handle(GetPriceListQuery request, CancellationToken cancellationToken)
        {
            var priceList = await _uow.PriceLists.GetByIdWithItemsAsync(request.PriceListId, cancellationToken);

            if (priceList == null)
                throw new KeyNotFoundException($"Price list with ID {request.PriceListId} not found");

            var dto = new PriceListDetailDto
            {
                Id = priceList.Id,
                Name = priceList.Name,
                Description = priceList.Description,
                EffectiveDate = priceList.EffectiveDate,
                ExpiryDate = priceList.ExpiryDate,
                IsActive = priceList.IsActive,
                Items = priceList.Items.Select(i => new PriceListItemDto
                {
                    VariantId = i.VariantId,
                    BasePrice = i.BasePrice,
                    DiscountedPrice = i.DiscountedPrice,
                    EffectivePrice = i.EffectivePrice
                }).ToList()
            };

            _logger.LogInformation("Retrieved price list {PriceListId}: {Name}", request.PriceListId, priceList.Name);
            return dto;
        }
    }
}
