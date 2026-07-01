using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class PublishProductCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
    }

    public class PublishProductHandler : IRequestHandler<PublishProductCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public PublishProductHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(PublishProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(x => x.Id == request.ProductId && !x.IsDeleted, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.Publish();

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
