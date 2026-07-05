using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Images.Commands
{
    public class SetPrimaryImageCommand : IRequest
    {
        public Guid ProductId { get; set; }
        public Guid ImageId { get; set; }
    }

    public class SetPrimaryImageValidator : AbstractValidator<SetPrimaryImageCommand>
    {
        public SetPrimaryImageValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");

            RuleFor(x => x.ImageId)
                .NotEqual(Guid.Empty).WithMessage("Image ID is required");
        }
    }

    public class SetPrimaryImageHandler : IRequestHandler<SetPrimaryImageCommand>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<SetPrimaryImageHandler> _logger;

        public SetPrimaryImageHandler(IProductUnitOfWork uow, ILogger<SetPrimaryImageHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task Handle(SetPrimaryImageCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithImagesAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var image = product.Images.FirstOrDefault(i => i.Id == request.ImageId);
            if (image == null)
                throw new KeyNotFoundException($"Image with ID {request.ImageId} not found for product {request.ProductId}");

            // Clear primary flag from all images
            foreach (var img in product.Images)
            {
                img.IsPrimary = false;
            }

            // Set new primary image
            image.SetAsPrimary();

            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Primary image set for product {ProductId}: {ImageId}", request.ProductId, request.ImageId);
        }
    }
}
