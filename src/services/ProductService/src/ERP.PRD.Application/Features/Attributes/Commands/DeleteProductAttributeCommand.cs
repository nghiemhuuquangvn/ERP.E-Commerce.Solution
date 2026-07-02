using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Attributes.Commands
{
    public class DeleteProductAttributeCommand : IRequest<Unit>
    {
        public Guid Id { get; set; }
    }

    public class DeleteProductAttributeHandler : IRequestHandler<DeleteProductAttributeCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public DeleteProductAttributeHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(DeleteProductAttributeCommand request, CancellationToken cancellationToken)
        {
            var attribute = await _context.ProductAttributes
                .FirstOrDefaultAsync(a => a.Id == request.Id && !a.IsDeleted, cancellationToken);

            if (attribute == null)
                throw new KeyNotFoundException($"Attribute with ID {request.Id} not found");

            // Mirror Product.RemoveVariant's guard-rail style: refuse the operation outright
            // rather than cascading, since values already assigned to products/variants would
            // otherwise be silently orphaned.
            var hasValues = await _context.ProductAttributeValues
                .AnyAsync(v => v.ProductAttributeId == request.Id && !v.IsDeleted, cancellationToken);

            if (hasValues)
                throw new InvalidOperationException("Cannot delete an attribute that has values assigned to products or variants. Remove those values first.");

            attribute.Delete();

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
