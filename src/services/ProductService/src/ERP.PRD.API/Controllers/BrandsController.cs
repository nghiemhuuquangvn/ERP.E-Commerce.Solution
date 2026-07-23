using Asp.Versioning;
using ERP.PRD.Application.Features.Brands.Commands;
using ERP.PRD.Application.Features.Brands.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    /// <summary>
    /// Product brand/manufacturer master data (flat reference list, no hierarchy).
    /// Gated with fine-grained permissions via IUserContextService.HasPermission(...),
    /// mirroring SettingsController's pattern — ProductService has no
    /// [AuthorizePermission] attribute infrastructure, only [Authorize].
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/brands")]
    [Authorize]
    public class BrandsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUserContextService _userContextService;
        private readonly ILogger<BrandsController> _logger;

        public BrandsController(IMediator mediator, IUserContextService userContextService, ILogger<BrandsController> logger)
        {
            _mediator = mediator;
            _userContextService = userContextService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new brand
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateBrand([FromBody] CreateBrandCommand command)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_BrandsManage))
                return Forbid();

            _logger.LogInformation("Creating brand: {Code}", command.Code);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetBrand), new { brandId = result.BrandId }, result);
        }

        /// <summary>
        /// Get all brands, optionally filtered by active status
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetBrands([FromQuery] bool? isActive = null)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_BrandsView))
                return Forbid();

            var result = await _mediator.Send(new GetBrandsQuery { IsActive = isActive });

            return Ok(result);
        }

        /// <summary>
        /// Get a single brand by ID
        /// </summary>
        [HttpGet("{brandId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBrand(Guid brandId)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_BrandsView))
                return Forbid();

            var result = await _mediator.Send(new GetBrandQuery { BrandId = brandId });

            return Ok(result);
        }

        /// <summary>
        /// Update a brand's name/logo
        /// </summary>
        [HttpPut("{brandId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateBrand(Guid brandId, [FromBody] UpdateBrandCommand command)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_BrandsManage))
                return Forbid();

            command.BrandId = brandId;
            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Activate a brand
        /// </summary>
        [HttpPost("{brandId}/activate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActivateBrand(Guid brandId)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_BrandsManage))
                return Forbid();

            await _mediator.Send(new ActivateBrandCommand { BrandId = brandId });

            return NoContent();
        }

        /// <summary>
        /// Deactivate a brand
        /// </summary>
        [HttpPost("{brandId}/deactivate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeactivateBrand(Guid brandId)
        {
            if (!_userContextService.HasPermission(PermissionConstants.Product_BrandsManage))
                return Forbid();

            await _mediator.Send(new DeactivateBrandCommand { BrandId = brandId });

            return NoContent();
        }
    }
}
