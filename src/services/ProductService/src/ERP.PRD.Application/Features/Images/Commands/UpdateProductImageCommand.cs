using ERP.PRD.Application.Interfaces;
using ERP.PRD.Application.Interfaces.Services;
using FluentValidation;
using MediatR;

namespace ERP.PRD.Application.Features.Images.Commands
{
    /// <summary>
    /// Edits an existing image's metadata, and optionally replaces its file entirely
    /// (the "Replace image" action) — the old blob is deleted only if it was managed by
    /// our storage service (never an externally-hosted URL registered via the legacy
    /// URL-only upload path).
    /// </summary>
    public class UpdateProductImageCommand : IRequest
    {
        public Guid ProductId { get; set; }
        public Guid ImageId { get; set; }
        public Stream? Content { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long Length { get; set; }
        public string? AltText { get; set; }
        public int SortOrder { get; set; }
    }

    public class UpdateProductImageValidator : AbstractValidator<UpdateProductImageCommand>
    {
        public UpdateProductImageValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEqual(Guid.Empty).WithMessage("Product ID is required");

            RuleFor(x => x.ImageId)
                .NotEqual(Guid.Empty).WithMessage("Image ID is required");

            RuleFor(x => x.AltText)
                .MaximumLength(200).WithMessage("Alt text cannot exceed 200 characters")
                .When(x => !string.IsNullOrEmpty(x.AltText));

            When(x => x.Content != null, () =>
            {
                RuleFor(x => x.ContentType!)
                    .Must(ct => ProductImageFileConstraints.AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
                    .WithMessage($"Content type must be one of: {string.Join(", ", ProductImageFileConstraints.AllowedContentTypes)}");

                RuleFor(x => x.Length)
                    .GreaterThan(0).WithMessage("File is empty")
                    .LessThanOrEqualTo(ProductImageFileConstraints.MaxSizeBytes)
                    .WithMessage($"File exceeds the maximum allowed size of {ProductImageFileConstraints.MaxSizeBytes / (1024 * 1024)} MB");
            });
        }
    }

    public class UpdateProductImageHandler : IRequestHandler<UpdateProductImageCommand>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<UpdateProductImageHandler> _logger;

        public UpdateProductImageHandler(IProductUnitOfWork uow, IFileStorageService fileStorage, ILogger<UpdateProductImageHandler> logger)
        {
            _uow = uow;
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task Handle(UpdateProductImageCommand request, CancellationToken cancellationToken)
        {
            var product = await _uow.Products.GetByIdWithImagesAsync(request.ProductId, cancellationToken);
            if (product == null)
                throw new KeyNotFoundException($"Product with ID {request.ProductId} not found");

            var image = product.Images.FirstOrDefault(i => i.Id == request.ImageId);
            if (image == null)
                throw new KeyNotFoundException($"Image with ID {request.ImageId} not found for product {request.ProductId}");

            var newUrl = image.ImageUrl;
            string? previousManagedBlobPath = null;

            if (request.Content != null)
            {
                previousManagedBlobPath = _fileStorage.TryGetManagedBlobPath(image.ImageUrl);

                var (url, _) = await _fileStorage.UploadAsync(
                    request.Content,
                    request.FileName ?? "image",
                    request.ContentType ?? "application/octet-stream",
                    request.ProductId.ToString(),
                    cancellationToken);

                newUrl = url;
            }

            image.UpdateMetadata(newUrl, request.AltText, request.SortOrder);
            await _uow.SaveChangesAsync(cancellationToken);

            // Best-effort cleanup of the replaced blob, after the DB is the source of
            // truth — an orphaned blob is a much smaller problem than a dangling
            // reference to a blob we just deleted before the save succeeded.
            if (previousManagedBlobPath != null)
                await _fileStorage.DeleteAsync(previousManagedBlobPath, cancellationToken);

            _logger.LogInformation("Image {ImageId} updated for product {ProductId}", request.ImageId, request.ProductId);
        }
    }
}
