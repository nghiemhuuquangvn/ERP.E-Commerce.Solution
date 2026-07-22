using Asp.Versioning;
using ERP.PRD.Application.Features.Attributes.Commands;
using ERP.PRD.Application.Features.Attributes.Queries;
using ERP.PRD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    /// <summary>
    /// Manages ProductAttribute definitions/options (the EAV-lite catalog of category-varying
    /// specs) as well as the attribute values assigned to individual Products/ProductVariants.
    /// The class-level route covers the "/api/v{version}/product-attributes" definitions
    /// resource; actions for the "/api/v{version}/products/{productId}/..." values
    /// sub-resources use an absolute (leading "/") override so they don't inherit the
    /// class-level "product-attributes" prefix.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/product-attributes")]
    [Authorize]
    public class AttributesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AttributesController> _logger;

        public AttributesController(IMediator mediator, ILogger<AttributesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Create a new product attribute definition
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateAttribute([FromBody] CreateProductAttributeCommand command)
        {
            _logger.LogInformation("Creating product attribute: {Code}", command.Code);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetAttributes), new { }, result);
        }

        /// <summary>
        /// Get product attribute definitions, optionally scoped to a category
        /// (always includes global attributes where CategoryId is null)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAttributes([FromQuery] Guid? categoryId)
        {
            var query = new GetProductAttributesQuery { CategoryId = categoryId };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Update a product attribute definition (Code/DataType are immutable)
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateAttribute(Guid id, [FromBody] UpdateProductAttributeCommand command)
        {
            command.Id = id;
            _logger.LogInformation("Updating product attribute: {Id}", id);

            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Add a selectable option to a SingleSelect/MultiSelect attribute
        /// </summary>
        [HttpPost("{id}/options")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddOption(Guid id, [FromBody] AddAttributeOptionCommand command)
        {
            command.ProductAttributeId = id;
            _logger.LogInformation("Adding option to attribute {Id}: {Value}", id, command.Value);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetAttributes), new { }, result);
        }

        /// <summary>
        /// Delete (soft-delete) a product attribute definition. Fails if any values are assigned.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAttribute(Guid id)
        {
            _logger.LogInformation("Deleting product attribute: {Id}", id);

            await _mediator.Send(new DeleteProductAttributeCommand { Id = id });

            return NoContent();
        }

        /// <summary>
        /// Set (upsert) an attribute value on a Product. OwnerType/OwnerId are always forced
        /// server-side from the route — never trust a client-supplied OwnerType here.
        /// </summary>
        [HttpPut("/api/v{version:apiVersion}/products/{productId}/attributes/{attributeId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetProductAttributeValue(Guid productId, Guid attributeId, [FromBody] SetProductAttributeValueCommand command)
        {
            command.ProductAttributeId = attributeId;
            command.OwnerType = (int)AttributeOwnerType.Product;
            command.OwnerId = productId;

            _logger.LogInformation("Setting attribute {AttributeId} value on product {ProductId}", attributeId, productId);

            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Set (upsert) an attribute value on a ProductVariant. OwnerType/OwnerId are always
        /// forced server-side from the route — never trust a client-supplied OwnerType here.
        /// </summary>
        [HttpPut("/api/v{version:apiVersion}/products/{productId}/variants/{variantId}/attributes/{attributeId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetVariantAttributeValue(Guid productId, Guid variantId, Guid attributeId, [FromBody] SetProductAttributeValueCommand command)
        {
            command.ProductAttributeId = attributeId;
            command.OwnerType = (int)AttributeOwnerType.ProductVariant;
            command.OwnerId = variantId;

            _logger.LogInformation("Setting attribute {AttributeId} value on variant {VariantId} of product {ProductId}", attributeId, variantId, productId);

            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Get attribute values for a Product. When variantId is supplied, also includes
        /// that variant's own attribute values alongside the product's shared ones.
        /// </summary>
        [HttpGet("/api/v{version:apiVersion}/products/{productId}/attributes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAttributeValues(Guid productId, [FromQuery] Guid? variantId)
        {
            var query = new GetProductAttributeValuesQuery { ProductId = productId, VariantId = variantId };
            var result = await _mediator.Send(query);

            return Ok(result);
        }
    }
}
