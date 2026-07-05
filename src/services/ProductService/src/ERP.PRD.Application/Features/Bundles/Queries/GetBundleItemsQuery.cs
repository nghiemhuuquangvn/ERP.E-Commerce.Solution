using ERP.PRD.Application.Interfaces;
using MediatR;

namespace ERP.PRD.Application.Features.Bundles.Queries
{
    public class GetBundleItemsQuery : IRequest<List<BundleItemDto>>
    {
        public Guid BundleProductId { get; set; }
    }

    public class BundleItemDto
    {
        public Guid Id { get; set; }
        public Guid ComponentProductId { get; set; }
        public string ComponentName { get; set; } = string.Empty;
        public string ComponentSku { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class GetBundleItemsHandler : IRequestHandler<GetBundleItemsQuery, List<BundleItemDto>>
    {
        private readonly IProductUnitOfWork _uow;

        public GetBundleItemsHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<List<BundleItemDto>> Handle(GetBundleItemsQuery request, CancellationToken cancellationToken)
        {
            var items = await _uow.Bundles.GetByBundleProductIdAsync(request.BundleProductId, cancellationToken);

            return items.Select(i => new BundleItemDto
            {
                Id = i.Id,
                ComponentProductId = i.ComponentProductId,
                ComponentName = i.ComponentName,
                ComponentSku = i.ComponentSku,
                Quantity = i.Quantity
            }).ToList();
        }
    }
}
