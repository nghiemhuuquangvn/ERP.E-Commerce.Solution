using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        private readonly ProductDbContext _context;

        public GetBundleItemsHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<List<BundleItemDto>> Handle(GetBundleItemsQuery request, CancellationToken cancellationToken)
        {
            return await (
                from item in _context.ProductBundleItems
                join component in _context.Products on item.ComponentProductId equals component.Id
                where item.BundleProductId == request.BundleProductId
                select new BundleItemDto
                {
                    Id = item.Id,
                    ComponentProductId = component.Id,
                    ComponentName = component.Name,
                    ComponentSku = component.Sku,
                    Quantity = item.Quantity
                }).ToListAsync(cancellationToken);
        }
    }
}
