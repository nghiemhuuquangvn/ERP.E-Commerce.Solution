using ERP.PRD.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Products.Commands
{
    public class UpdateProductDescriptionCommand : IRequest<Unit>
    {
        public Guid ProductId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateProductDescriptionValidator : AbstractValidator<UpdateProductDescriptionCommand>
    {
        public UpdateProductDescriptionValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty().WithMessage("Product ID is required");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Description is required")
                .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters");
        }
    }

    public class UpdateProductDescriptionHandler : IRequestHandler<UpdateProductDescriptionCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public UpdateProductDescriptionHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(UpdateProductDescriptionCommand request, CancellationToken cancellationToken)
        {
            var product = await _context.Products.FirstOrDefaultAsync(
                x => x.Id == request.ProductId && !x.IsDeleted,
                cancellationToken
            );

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            product.UpdateDescription(request.Description);

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
