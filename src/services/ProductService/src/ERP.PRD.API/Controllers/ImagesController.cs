using Asp.Versioning;
using ERP.PRD.Application.Features.Images.Commands;
using ERP.PRD.Application.Features.Images.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/products/{productId}/images")]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ImagesController> _logger;

        public ImagesController(IMediator mediator, ILogger<ImagesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Upload a new product image
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="command">Image upload details</param>
        /// <returns>Uploaded image with ID</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UploadImage(Guid productId, [FromBody] UploadProductImageCommand command)
        {
            command.ProductId = productId;
            _logger.LogInformation("Uploading image for product {ProductId}", productId);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetImages), new { productId }, result);
        }

        /// <summary>
        /// Upload a real image file (multipart/form-data), stored in Azure Blob Storage.
        /// Kept alongside the URL-registration endpoint above so externally-hosted/CDN
        /// images remain supported.
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="file">The image file</param>
        /// <param name="variantId">Optional: scopes the image to a specific variant</param>
        /// <param name="altText">Optional alt text</param>
        /// <param name="setAsPrimary">Whether to set this as the primary image</param>
        /// <returns>Uploaded image with ID</returns>
        [HttpPost("file")]
        [RequestSizeLimit(10_000_000)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UploadImageFile(
            Guid productId,
            [FromForm] IFormFile file,
            [FromForm] Guid? variantId,
            [FromForm] string? altText,
            [FromForm] bool setAsPrimary = false)
        {
            _logger.LogInformation("Uploading image file for product {ProductId}", productId);

            await using var stream = file.OpenReadStream();
            var command = new UploadProductImageFileCommand
            {
                ProductId = productId,
                VariantId = variantId,
                Content = stream,
                FileName = file.FileName,
                ContentType = file.ContentType,
                Length = file.Length,
                AltText = altText,
                SetAsPrimary = setAsPrimary,
            };

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetImages), new { productId }, result);
        }

        /// <summary>
        /// Update an existing image's metadata (alt text/sort order), optionally
        /// replacing its file entirely by including a new one.
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="imageId">Image ID</param>
        /// <param name="file">Optional replacement file</param>
        /// <param name="altText">Alt text</param>
        /// <param name="sortOrder">Display sort order</param>
        /// <returns>No content</returns>
        [HttpPut("{imageId}")]
        [RequestSizeLimit(10_000_000)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateImage(
            Guid productId,
            Guid imageId,
            [FromForm] IFormFile? file,
            [FromForm] string? altText,
            [FromForm] int sortOrder = 0)
        {
            _logger.LogInformation("Updating image {ImageId} for product {ProductId}", imageId, productId);

            Stream? stream = file?.OpenReadStream();
            try
            {
                var command = new UpdateProductImageCommand
                {
                    ProductId = productId,
                    ImageId = imageId,
                    Content = stream,
                    FileName = file?.FileName,
                    ContentType = file?.ContentType,
                    Length = file?.Length ?? 0,
                    AltText = altText,
                    SortOrder = sortOrder,
                };

                await _mediator.Send(command);

                return NoContent();
            }
            finally
            {
                if (stream != null)
                    await stream.DisposeAsync();
            }
        }

        /// <summary>
        /// Get all images for a product
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>List of product images</returns>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetImages(Guid productId)
        {
            _logger.LogInformation("Fetching images for product {ProductId}", productId);

            var query = new GetProductImagesQuery { ProductId = productId };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Remove an image from a product
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="imageId">Image ID</param>
        /// <returns>No content</returns>
        [HttpDelete("{imageId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveImage(Guid productId, Guid imageId)
        {
            _logger.LogInformation("Removing image {ImageId} from product {ProductId}", imageId, productId);

            var command = new RemoveProductImageCommand { ProductId = productId, ImageId = imageId };
            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Set an image as the primary product image
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="imageId">Image ID to set as primary</param>
        /// <returns>No content</returns>
        [HttpPost("{imageId}/primary")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetPrimaryImage(Guid productId, Guid imageId)
        {
            _logger.LogInformation("Setting image {ImageId} as primary for product {ProductId}", imageId, productId);

            var command = new SetPrimaryImageCommand { ProductId = productId, ImageId = imageId };
            await _mediator.Send(command);

            return NoContent();
        }
    }
}
