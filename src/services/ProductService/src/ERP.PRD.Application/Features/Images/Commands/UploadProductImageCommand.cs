using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Images.Commands
{
    public class UploadProductImageCommand : IRequest<UploadProductImageResponse>
    {
        public Guid ProductId { get; set; }

        /// <summary>
        /// Optional: scopes this image to one specific variant (e.g. a color swatch
        /// photo) rather than the whole product. Null means it's a product-level image.
        /// </summary>
        public Guid? VariantId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool SetAsPrimary { get; set; }
    }

    public class UploadProductImageResponse
    {
        public Guid ImageId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }

    public class UploadProductImageValidator : AbstractValidator<UploadProductImageCommand>
    {
        public UploadProductImageValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");

            RuleFor(x => x.ImageUrl)
                .NotEmpty().WithMessage("Image URL is required")
                .MaximumLength(500).WithMessage("Image URL cannot exceed 500 characters")
                .Must(url => url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("/"))
                    .WithMessage("Image URL must be a valid HTTP/HTTPS URL or relative path");

            RuleFor(x => x.AltText)
                .MaximumLength(200).WithMessage("Alt text cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.AltText));
        }
    }

    public class UploadProductImageHandler : IRequestHandler<UploadProductImageCommand, UploadProductImageResponse>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly ILogger<UploadProductImageHandler> _logger;

        public UploadProductImageHandler(IProductUnitOfWork uow, ILogger<UploadProductImageHandler> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<UploadProductImageResponse> Handle(UploadProductImageCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithImagesAsync(request.ProductId, cancellationToken);
            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            // If this is the first image or user wants it as primary, set as primary and clear others
            var existingImages = product.Images.Count;
            var isPrimary = request.SetAsPrimary || existingImages == 0;

            if (isPrimary)
            {
                foreach (var img in product.Images.Where(i => i.IsPrimary))
                {
                    img.IsPrimary = false;
                }
            }

            var image = new ProductImage(
                request.ProductId,
                request.ImageUrl,
                request.AltText,
                sortOrder: existingImages,
                isPrimary: isPrimary,
                variantId: request.VariantId
            );

            product.AddImage(image);

            // Explicit Add, not just Product.AddImage()'s navigation-fixup: ProductImage.Id
            // is a client-assigned Guid (set in its constructor), and EF Core's DetectChanges
            // heuristic for a graph-discovered entity whose store-generated key already has a
            // non-default value marks it Modified rather than Added — SaveChanges then tries
            // to UPDATE a row that doesn't exist yet (0 rows affected -> false
            // DbUpdateConcurrencyException). AddAsync forces the correct EntityState.Added.
            await _uow.Images.AddAsync(image, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Image uploaded for product {ProductId}: {ImageUrl}, IsPrimary: {IsPrimary}",
                request.ProductId, request.ImageUrl, isPrimary);

            return new UploadProductImageResponse
            {
                ImageId = image.Id,
                ImageUrl = image.ImageUrl,
                IsPrimary = image.IsPrimary
            };
        }
    }
}
