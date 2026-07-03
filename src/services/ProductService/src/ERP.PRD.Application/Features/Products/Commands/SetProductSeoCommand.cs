using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class SetProductSeoCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public string? Slug { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
    }

    public class SetProductSeoValidator : AbstractValidator<SetProductSeoCommand>
    {
        public SetProductSeoValidator()
        {
            RuleFor(x => x.Slug).MaximumLength(256);
            RuleFor(x => x.MetaTitle).MaximumLength(200);
            RuleFor(x => x.MetaDescription).MaximumLength(320);
        }
    }

    public class SetProductSeoHandler : IRequestHandler<SetProductSeoCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public SetProductSeoHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(SetProductSeoCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FirstOrDefaultAsync(
                x => x.Id == request.ProductId && !x.IsDeleted, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var slugTaken = await _context.Products.AnyAsync(
                    p => p.Id != request.ProductId && p.Slug == request.Slug.Trim().ToLower(), cancellationToken);
                if (slugTaken)
                    throw new InvalidOperationException($"Slug '{request.Slug}' is already used by another product");
            }

            product.SetSeoFields(request.Slug, request.MetaTitle, request.MetaDescription);

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
