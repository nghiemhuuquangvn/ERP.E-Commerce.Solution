using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Attributes.Commands
{
    public class AddAttributeOptionCommand : IRequest<AddAttributeOptionResponse>
    {
        public Guid ProductAttributeId { get; set; }
        public string Value { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class AddAttributeOptionResponse
    {
        public Guid OptionId { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    public class AddAttributeOptionValidator : AbstractValidator<AddAttributeOptionCommand>
    {
        public AddAttributeOptionValidator()
        {
            RuleFor(x => x.ProductAttributeId).NotEmpty().WithMessage("Attribute ID is required");

            RuleFor(x => x.Value)
                .NotEmpty().WithMessage("Option value is required")
                .MaximumLength(200).WithMessage("Option value cannot exceed 200 characters");
        }
    }

    public class AddAttributeOptionHandler : IRequestHandler<AddAttributeOptionCommand, AddAttributeOptionResponse>
    {
        private readonly ProductDbContext _context;

        public AddAttributeOptionHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<AddAttributeOptionResponse> Handle(AddAttributeOptionCommand request, CancellationToken cancellationToken)
        {
            var attribute = await _context.ProductAttributes
                .Include(a => a.Options)
                .FirstOrDefaultAsync(a => a.Id == request.ProductAttributeId && !a.IsDeleted, cancellationToken);

            if (attribute == null)
                throw new KeyNotFoundException($"Attribute with ID {request.ProductAttributeId} not found");

            var option = attribute.AddOption(request.Value, request.SortOrder);

            await _context.SaveChangesAsync(cancellationToken);

            return new AddAttributeOptionResponse
            {
                OptionId = option.Id,
                Value = option.Value
            };
        }
    }
}
