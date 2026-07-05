using ERP.PRD.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Images.Commands
{
    public class RemoveProductImageCommand : IRequest
    {
        public Guid ProductId { get; set; }
        public Guid ImageId { get; set; }
    }

    public class RemoveProductImageValidator : AbstractValidator<RemoveProductImageCommand>
    {
        public RemoveProductImageValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");

            RuleFor(x => x.ImageId)
                .NotEqual(Guid.Empty).WithMessage("Image ID is required");
        }
    }

    public class RemoveProductImageHandler : IRequestHandler<RemoveProductImageCommand>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<RemoveProductImageHandler> _logger;

        public RemoveProductImageHandler(IProductUnitOfWork uow, ILogger<RemoveProductImageHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task Handle(RemoveProductImageCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithImagesAsync(request.ProductId, cancellationToken);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var image = product.Images.FirstOrDefault(i => i.Id == request.ImageId);
            if (image == null)
                throw new KeyNotFoundException($"Image with ID {request.ImageId} not found for product {request.ProductId}");

            var wasPrimary = image.IsPrimary;
            product.RemoveImage(request.ImageId);

            // If removed image was primary, set the first remaining image as primary
            if (wasPrimary && product.Images.Any())
            {
                product.Images.First().SetAsPrimary();
            }

            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Image removed from product {ProductId}: {ImageId}", request.ProductId, request.ImageId);
        }
    }
}
