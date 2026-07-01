using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class DiscontinueProductCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
    }

    public class DiscontinueProductHandler : IRequestHandler<DiscontinueProductCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public DiscontinueProductHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(DiscontinueProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FirstOrDefaultAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.Discontinue();

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
