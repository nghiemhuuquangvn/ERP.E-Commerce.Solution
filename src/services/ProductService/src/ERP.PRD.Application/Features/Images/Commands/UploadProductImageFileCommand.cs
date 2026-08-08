using ERP.PRD.Application.Interfaces;
using ERP.PRD.Application.Interfaces.Services;
using ERP.PRD.Domain.Entities;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Images.Commands
{
    /// <summary>
    /// Uploads a real image file (as opposed to UploadProductImageCommand, which only
    /// registers a URL the admin already hosts elsewhere — kept side by side so
    /// externally-hosted/CDN images remain supported).
    /// </summary>
    public class UploadProductImageFileCommand : IRequest<UploadProductImageResponse>
    {
        public Guid ProductId { get; set; }
        public Guid? VariantId { get; set; }
        public Stream Content { get; set; } = Stream.Null;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
        public string? AltText { get; set; }
        public bool SetAsPrimary { get; set; }
    }

    public class UploadProductImageFileValidator : AbstractValidator<UploadProductImageFileCommand>
    {
        public UploadProductImageFileValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");

            RuleFor(x => x.FileName)
                .NotEmpty().WithMessage("File name is required");

            RuleFor(x => x.ContentType)
                .Must(ct => ProductImageFileConstraints.AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
                .WithMessage($"Content type must be one of: {string.Join(", ", ProductImageFileConstraints.AllowedContentTypes)}");

            RuleFor(x => x.Length)
                .GreaterThan(0).WithMessage("File is empty")
                .LessThanOrEqualTo(ProductImageFileConstraints.MaxSizeBytes)
                .WithMessage($"File exceeds the maximum allowed size of {ProductImageFileConstraints.MaxSizeBytes / (1024 * 1024)} MB");

            RuleFor(x => x.AltText)
                .MaximumLength(200).WithMessage("Alt text cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.AltText));
        }
    }

    public class UploadProductImageFileHandler : IRequestHandler<UploadProductImageFileCommand, UploadProductImageResponse>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<UploadProductImageFileHandler> _logger;

        public UploadProductImageFileHandler(IProductUnitOfWork uow, IFileStorageService fileStorage, ILogger<UploadProductImageFileHandler> logger)
        {
            _uow = uow;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task<UploadProductImageResponse> Handle(UploadProductImageFileCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithImagesAsync(request.ProductId, cancellationToken);
            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var (url, _) = await _fileStorage.UploadAsync(
                request.Content,
                request.FileName,
                request.ContentType,
                request.ProductId.ToString(),
                cancellationToken);

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
                url,
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

            _logger.LogInformation("Image file uploaded for product {ProductId}: {ImageUrl}, IsPrimary: {IsPrimary}",
                request.ProductId, url, isPrimary);

            return new UploadProductImageResponse
            {
                ImageId = image.Id,
                ImageUrl = image.ImageUrl,
                IsPrimary = image.IsPrimary
            };
        }
    }
}
