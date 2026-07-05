using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

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
        private readonly IProductUnitOfWork _uow;

        public SetProductSeoHandler(IProductUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<Unit> Handle(SetProductSeoCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            if (!string.IsNullOrWhiteSpace(request.Slug))
            {
                var slugTaken = await _uow.Products.SlugTakenByAnotherAsync(
                    request.ProductId, request.Slug, cancellationToken);
                if (slugTaken)
                    throw new InvalidOperationException($"Slug '{request.Slug}' is already used by another product");
            }

            product.SetSeoFields(request.Slug, request.MetaTitle, request.MetaDescription);

            await _uow.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
