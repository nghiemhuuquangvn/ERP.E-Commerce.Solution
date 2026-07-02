using ERP.PRD.Domain.Enums;
using ERP.PRD.Infrastructure.Persistence;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ERP.PRD.Application.Features.Attributes.Commands
{
    public class CreateProductAttributeCommand : IRequest<CreateProductAttributeResponse>
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DataType { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Unit { get; set; }
        public bool IsVariantOption { get; set; }
        public bool IsRequired { get; set; }
        public bool IsFilterable { get; set; } = true;
        public List<string>? Options { get; set; }
    }

    public class CreateProductAttributeResponse
    {
        public Guid AttributeId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class CreateProductAttributeValidator : AbstractValidator<CreateProductAttributeCommand>
    {
        public CreateProductAttributeValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Attribute code is required")
                .MaximumLength(100).WithMessage("Attribute code cannot exceed 100 characters")
                .Matches(@"^[a-z0-9_]+$").WithMessage("Attribute code must contain only lowercase letters, numbers, and underscores");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Attribute name is required")
                .MaximumLength(200).WithMessage("Attribute name cannot exceed 200 characters");

            RuleFor(x => x.DataType)
                .Must(v => Enum.IsDefined(typeof(AttributeDataType), v))
                .WithMessage("DataType must be a valid attribute data type");

            RuleFor(x => x.Options)
                .Must(o => o != null && o.Count > 0)
                .When(x => Enum.IsDefined(typeof(AttributeDataType), x.DataType) &&
                           ((AttributeDataType)x.DataType is AttributeDataType.SingleSelect or AttributeDataType.MultiSelect))
                .WithMessage("At least one option is required for SingleSelect/MultiSelect attributes");

            RuleFor(x => x.Options)
                .Must(o => o == null || o.Count == 0)
                .When(x => Enum.IsDefined(typeof(AttributeDataType), x.DataType) &&
                           ((AttributeDataType)x.DataType is not (AttributeDataType.SingleSelect or AttributeDataType.MultiSelect)))
                .WithMessage("Options only apply to SingleSelect/MultiSelect attributes");
        }
    }

    public class CreateProductAttributeHandler : IRequestHandler<CreateProductAttributeCommand, CreateProductAttributeResponse>
    {
        private readonly ProductDbContext _context;

        public CreateProductAttributeHandler(ProductDbContext context)
        {
            _context = context;
        }

        public async Task<CreateProductAttributeResponse> Handle(CreateProductAttributeCommand request, CancellationToken cancellationToken)
        {
            if (request.CategoryId.HasValue)
            {
                var categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == request.CategoryId && !c.IsDeleted, cancellationToken);

                if (!categoryExists)
                    throw new KeyNotFoundException($"Category with ID {request.CategoryId} not found");
            }

            var normalizedCode = request.Code.Trim().ToLowerInvariant();
            var codeExists = await _context.ProductAttributes
                .AnyAsync(a => a.Code == normalizedCode && !a.IsDeleted, cancellationToken);

            if (codeExists)
                throw new InvalidOperationException($"Attribute with code '{request.Code}' already exists");

            var dataType = (AttributeDataType)request.DataType;

            var attribute = Domain.Entities.ProductAttribute.Create(
                request.Code,
                request.Name,
                dataType,
                request.CategoryId,
                request.Unit,
                request.IsVariantOption,
                request.IsRequired,
                request.IsFilterable);

            if (request.Options != null)
            {
                foreach (var option in request.Options)
                    attribute.AddOption(option);
            }

            await _context.ProductAttributes.AddAsync(attribute, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return new CreateProductAttributeResponse
            {
                AttributeId = attribute.Id,
                Code = attribute.Code
            };
        }
    }
}
