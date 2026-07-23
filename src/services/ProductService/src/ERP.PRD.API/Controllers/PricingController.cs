using Asp.Versioning;
using ERP.PRD.Application.Features.Pricing.Commands;
using ERP.PRD.Application.Features.Pricing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/pricing")]
    [Authorize]
    public class PricingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PricingController> _logger;

        public PricingController(IMediator mediator, ILogger<PricingController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Set price for a specific variant
        /// </summary>
        /// <param name="command">Pricing details</param>
        /// <returns>No content</returns>
        [HttpPost("variants")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetVariantPrice([FromBody] SetProductPriceCommand command)
        {
            _logger.LogInformation("Setting price for variant {VariantId}: Base={BasePrice}", command.VariantId, command.BasePrice);

            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Get all prices for a product
        /// </summary>
        /// <param name="productId">Product ID</param>
        /// <returns>List of variant prices</returns>
        [HttpGet("products/{productId}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductPrices(Guid productId)
        {
            _logger.LogInformation("Fetching prices for product {ProductId}", productId);

            var query = new GetProductPricesQuery { ProductId = productId };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Create a new price list
        /// </summary>
        /// <param name="command">Price list details with items</param>
        /// <returns>Created price list</returns>
        [HttpPost("lists")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreatePriceList([FromBody] CreatePriceListCommand command)
        {
            _logger.LogInformation("Creating price list: {Name} with {Count} items", command.Name, command.Items.Count);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetPriceList), new { priceListId = result.PriceListId }, result);
        }

        /// <summary>
        /// Get price list details
        /// </summary>
        /// <param name="priceListId">Price list ID</param>
        /// <returns>Price list with all items</returns>
        [HttpGet("lists/{priceListId}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPriceList(Guid priceListId)
        {
            _logger.LogInformation("Fetching price list {PriceListId}", priceListId);

            var query = new GetPriceListQuery { PriceListId = priceListId };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Set the sales channel and currency for a price list
        /// </summary>
        /// <param name="priceListId">Price list ID</param>
        /// <param name="command">Channel/currency details</param>
        /// <returns>No content</returns>
        [HttpPut("lists/{priceListId}/channel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetPriceListChannel(Guid priceListId, [FromBody] SetPriceListChannelCommand command)
        {
            command.PriceListId = priceListId;
            _logger.LogInformation("Setting channel for price list {PriceListId}: {Channel}", priceListId, command.Channel);

            await _mediator.Send(command);

            return NoContent();
        }
    }
}
