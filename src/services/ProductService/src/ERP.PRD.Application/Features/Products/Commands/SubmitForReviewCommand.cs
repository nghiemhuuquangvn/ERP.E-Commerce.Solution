using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class SubmitForReviewCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
    }

    public class SubmitForReviewHandler : IRequestHandler<SubmitForReviewCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public SubmitForReviewHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(SubmitForReviewCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FirstOrDefaultAsync(
                x => x.Id == request.ProductId && !x.IsDeleted, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.SubmitForReview();

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
