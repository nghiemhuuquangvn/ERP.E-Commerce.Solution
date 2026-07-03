using ERP.PRD.Infrastructure.Persistence;
using MediatR;

namespace ERP.PRD.Application.Features.Bundles.Commands
{
    public class RemoveBundleItemCommand : IRequest<Unit>
    {
        public Guid BundleItemId { get; set; }
    }

    public class RemoveBundleItemHandler : IRequestHandler<RemoveBundleItemCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public RemoveBundleItemHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(RemoveBundleItemCommand request, CancellationToken cancellationToken)
        {
            var item = await _context.ProductBundleItems.FindAsync(new object[] { request.BundleItemId }, cancellationToken);
            if (item == null)
                throw new KeyNotFoundException($"Bundle item with ID {request.BundleItemId} not found");

            _context.ProductBundleItems.Remove(item);
            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
