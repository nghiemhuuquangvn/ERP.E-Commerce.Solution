using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Variants.Commands
{
    public class DeleteProductVariantCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public Guid VariantId { get; set; }
    }

    public class DeleteProductVariantHandler : IRequestHandler<DeleteProductVariantCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public DeleteProductVariantHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(DeleteProductVariantCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(x => x.Id == request.ProductId && !x.IsDeleted, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            // Check if this is the last variant (cannot remove - product must have at least one)
            if (product.Variants.Count == 1)
                throw new InvalidOperationException("Cannot delete the last variant. Product must have at least one variant.");

            // Remove variant
            product.RemoveVariant(request.VariantId);

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
