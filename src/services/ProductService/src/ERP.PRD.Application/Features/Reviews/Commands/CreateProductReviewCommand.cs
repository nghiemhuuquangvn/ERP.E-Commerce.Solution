using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Reviews.Commands
{
    public class CreateProductReviewCommand : IRequest<Guid>
    {
        public Guid ProductId { get; set; }
        public Guid CustomerId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }

    public class CreateProductReviewValidator : AbstractValidator<CreateProductReviewCommand>
    {
        public CreateProductReviewValidator()
        {
            RuleFor(x => x.ProductId).NotEmpty();
            RuleFor(x => x.CustomerId).NotEmpty();
            RuleFor(x => x.Rating).InclusiveBetween(1, 5);
            RuleFor(x => x.Comment).MaximumLength(2000);
        }
    }

    /// <summary>
    /// No moderation queue today — IsApproved defaults true on ProductReview.Create().
    /// A future moderation workflow (flag/Hide()) can be layered on without a schema
    /// change since the entity already carries IsApproved.
    /// </summary>
    public class CreateProductReviewHandler : IRequestHandler<CreateProductReviewCommand, Guid>
    {
        private readonly ProductDbContext _context;

        public CreateProductReviewHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> Handle(CreateProductReviewCommand request, CancellationToken cancellationToken)
        {
            var productExists = await _context.Products.AnyAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken);
            if (!productExists)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var review = ProductReview.Create(request.ProductId, request.CustomerId, request.Rating, request.Comment);
            await _context.ProductReviews.AddAsync(review, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return review.Id;
        }
    }
}
