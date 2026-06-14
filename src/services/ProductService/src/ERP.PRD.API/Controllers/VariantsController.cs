using ERP.PRD.Application.Features.Variants.Commands;
using ERP.PRD.Application.Features.Variants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    [ApiController]
    [Route("api/products/{productId}/variants")]
    [Authorize]
    public class VariantsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<VariantsController> _logger;

        public VariantsController(IMediator mediator, ILogger<VariantsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Create a new product variant (SKU)
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="command">Variant creation details</param>
        /// <returns>Created variant with ID</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateVariant(Guid productId, [FromBody] CreateProductVariantCommand command)
        {
            command.ProductId = productId;
            _logger.LogInformation("Creating variant for product {ProductId}: {Sku}", productId, command.Sku);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetVariant), new { productId, variantId = result.VariantId }, result);
        }

        /// <summary>
        /// Get all variants for a product
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>List of variants</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVariants(Guid productId)
        {
            _logger.LogInformation("Fetching variants for product {ProductId}", productId);

            var query = new GetProductVariantsQuery { ProductId = productId };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Get a specific variant by ID
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="variantId">Variant ID</param>
        /// <returns>Variant details</returns>
        [HttpGet("{variantId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVariant(Guid productId, Guid variantId)
        {
            _logger.LogInformation("Fetching variant {VariantId} for product {ProductId}", variantId, productId);

            var query = new GetProductVariantsQuery { ProductId = productId };
            var variants = await _mediator.Send(query);

            var variant = variants.FirstOrDefault(v => v.Id == variantId);
            if (variant == null)
                return NotFound($"Variant with ID {variantId} not found");

            return Ok(variant);
        }

        /// <summary>
        /// Update a product variant
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="variantId">Variant ID</param>
        /// <param name="command">Update details</param>
        /// <returns>No content</returns>
        [HttpPut("{variantId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateVariant(Guid productId, Guid variantId, [FromBody] UpdateProductVariantCommand command)
        {
            command.ProductId = productId;
            command.VariantId = variantId;
            _logger.LogInformation("Updating variant {VariantId} for product {ProductId}", variantId, productId);

            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Delete a product variant (cannot delete last variant)
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <param name="variantId">Variant ID</param>
        /// <returns>No content</returns>
        [HttpDelete("{variantId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteVariant(Guid productId, Guid variantId)
        {
            _logger.LogInformation("Deleting variant {VariantId} from product {ProductId}", variantId, productId);

            var command = new DeleteProductVariantCommand { ProductId = productId, VariantId = variantId };
            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Get variant by SKU (global lookup, anonymous access)
        /// </summary>
        /// <param name="sku">Product SKU</param>
        /// <returns>Variant details</returns>
        [HttpGet("sku/{sku}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVariantBySku(string sku)
        {
            _logger.LogInformation("Fetching variant by SKU: {Sku}", sku);

            var query = new GetVariantBySkuQuery { Sku = sku };
            var result = await _mediator.Send(query);

            return Ok(result);
        }
    }
}
