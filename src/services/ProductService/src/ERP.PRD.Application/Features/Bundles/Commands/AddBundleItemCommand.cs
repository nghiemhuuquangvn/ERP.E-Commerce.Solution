using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Bundles.Commands
{
    public class AddBundleItemCommand : IRequest<Guid>
    {
        public Guid BundleProductId { get; set; }
        public Guid ComponentProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class AddBundleItemHandler : IRequestHandler<AddBundleItemCommand, Guid>
    {
        private readonly ProductDbContext _context;

        public AddBundleItemHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(AddBundleItemCommand request, CancellationToken cancellationToken)
        {
            var bundleExists = await _context.Products.AnyAsync(p => p.Id == request.BundleProductId && !p.IsDeleted, cancellationToken);
            if (!bundleExists)
                throw new KeyNotFoundException($"Product with ID {request.BundleProductId} not found");

            var componentExists = await _context.Products.AnyAsync(p => p.Id == request.ComponentProductId && !p.IsDeleted, cancellationToken);
            if (!componentExists)
                throw new KeyNotFoundException($"Component product with ID {request.ComponentProductId} not found");

            var item = ProductBundleItem.Create(request.BundleProductId, request.ComponentProductId, request.Quantity);
            await _context.ProductBundleItems.AddAsync(item, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return item.Id;
        }
    }
}
