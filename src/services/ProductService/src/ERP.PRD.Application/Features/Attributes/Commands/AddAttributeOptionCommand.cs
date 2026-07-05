using ERP.PRD.Application.Interfaces;
using MediatR;
using FluentValidation;

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
        private readonly IProductUnitOfWork _uow;

        public AddAttributeOptionHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<AddAttributeOptionResponse> Handle(AddAttributeOptionCommand request, CancellationToken cancellationToken)
        {
            var attribute = await _uow.Attributes.GetByIdWithOptionsAsync(request.ProductAttributeId, cancellationToken);

            if (attribute == null)
                throw new KeyNotFoundException($"Attribute with ID {request.ProductAttributeId} not found");

            var option = attribute.AddOption(request.Value, request.SortOrder);

            await _uow.SaveChangesAsync(cancellationToken);

            return new AddAttributeOptionResponse
            {
                OptionId = option.Id,
                Value = option.Value
            };
        }
    }
}
