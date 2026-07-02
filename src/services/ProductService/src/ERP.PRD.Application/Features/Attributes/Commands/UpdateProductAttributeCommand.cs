using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Attributes.Commands
{
    // Code and DataType are deliberately not exposed here — they are immutable once
    // an attribute is created (changing either would silently invalidate any existing
    // ProductAttributeValues), so update requests simply cannot request that change.
    public class UpdateProductAttributeCommand : IRequest<Unit>
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public bool? IsRequired { get; set; }
        public bool? IsFilterable { get; set; }
        public int? SortOrder { get; set; }
    }

    public class UpdateProductAttributeValidator : AbstractValidator<UpdateProductAttributeCommand>
    {
        public UpdateProductAttributeValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("Attribute ID is required");

            RuleFor(x => x.Name)
                .MaximumLength(200).WithMessage("Attribute name cannot exceed 200 characters")
                .When(x => !string.IsNullOrWhiteSpace(x.Name));
        }
    }

    public class UpdateProductAttributeHandler : IRequestHandler<UpdateProductAttributeCommand, Unit>
    {
        private readonly ProductDbContext _context;

        public UpdateProductAttributeHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<Unit> Handle(UpdateProductAttributeCommand request, CancellationToken cancellationToken)
        {
            var attribute = await _context.ProductAttributes
                .FirstOrDefaultAsync(a => a.Id == request.Id && !a.IsDeleted, cancellationToken);

            if (attribute == null)
                throw new KeyNotFoundException($"Attribute with ID {request.Id} not found");

            if (!string.IsNullOrWhiteSpace(request.Name))
                attribute.Name = request.Name;

            if (!string.IsNullOrWhiteSpace(request.Unit))
                attribute.Unit = request.Unit;

            if (request.IsRequired.HasValue)
                attribute.IsRequired = request.IsRequired.Value;

            if (request.IsFilterable.HasValue)
                attribute.IsFilterable = request.IsFilterable.Value;

            if (request.SortOrder.HasValue)
                attribute.SortOrder = request.SortOrder.Value;

            attribute.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
