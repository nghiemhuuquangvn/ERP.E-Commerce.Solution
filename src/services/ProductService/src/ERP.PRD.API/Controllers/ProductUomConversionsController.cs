using Asp.Versioning;
using ERP.PRD.Application.Features.UomConversions.Commands;
using ERP.PRD.Application.Features.UomConversions.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    /// <summary>
    /// Per-product unit-of-measure conversions (e.g. 1 Case = 24 Can). FromUomId/ToUomId
    /// reference SharedMasterDataService's UnitOfMeasure by Id only (cross-service,
    /// no database FK). Gated with fine-grained permissions via
    /// IUserContextService.HasPermission(...), mirroring SettingsController's pattern.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/products/{productId}/uom-conversions")]
    [Authorize]
    public class ProductUomConversionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUserContextService _userContextService;
        private readonly ILogger<ProductUomConversionsController> _logger;

        public ProductUomConversionsController(
            IMediator mediator, IUserContextService userContextService, ILogger<ProductUomConversionsController> logger)
        {
            _mediator = mediator;
            _userContextService = userContextService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new UoM conversion for a product
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateUomConversion(Guid productId, [FromBody] CreateProductUomConversionCommand command)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_UomConversionsManage))
                return Forbid();

            command.ProductId = productId;
            _logger.LogInformation("Creating UoM conversion for product {ProductId}", productId);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetUomConversion), new { productId, uomConversionId = result.UomConversionId }, result);
        }

        /// <summary>
        /// Get all UoM conversions for a product
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUomConversions(Guid productId)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_UomConversionsView))
                return Forbid();

            var result = await _mediator.Send(new GetProductUomConversionsQuery { ProductId = productId });

            return Ok(result);
        }

        /// <summary>
        /// Get a single UoM conversion by ID
        /// </summary>
        [HttpGet("{uomConversionId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUomConversion(Guid productId, Guid uomConversionId)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_UomConversionsView))
                return Forbid();

            var result = await _mediator.Send(
                new GetProductUomConversionQuery { ProductId = productId, UomConversionId = uomConversionId });

            return Ok(result);
        }

        /// <summary>
        /// Update a UoM conversion's factor
        /// </summary>
        [HttpPut("{uomConversionId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUomConversion(
            Guid productId, Guid uomConversionId, [FromBody] UpdateProductUomConversionCommand command)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_UomConversionsManage))
                return Forbid();

            command.ProductId = productId;
            command.UomConversionId = uomConversionId;

            await _mediator.Send(command);

            return NoContent();
        }
    }
}
