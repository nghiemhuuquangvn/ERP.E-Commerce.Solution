using ERP.PRD.Application.Features.Products.Commands;
using ERP.PRD.Application.Features.Products.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(IMediator mediator, ILogger<ProductsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Create a new product
        /// </summary>
        /// <param name="command">Product creation details</param>
        /// <returns>Created product with ID</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductCommand command)
        {
            _logger.LogInformation("Creating product: {ProductName}", command.Name);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetProduct), new { id = result.ProductId }, result);
        }

        /// <summary>
        /// Get product by ID
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>Product details</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProduct(Guid id)
        {
            _logger.LogInformation("Fetching product: {ProductId}", id);

            var query = new GetProductQuery { ProductId = id };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Update product description
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <param name="command">Update details</param>
        /// <returns>No content</returns>
        [HttpPut("{id}/description")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateProductDescription(Guid id, [FromBody] UpdateProductDescriptionCommand command)
        {
            command.ProductId = id;
            _logger.LogInformation("Updating description for product: {ProductId}", id);

            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Publish product (activate for sale)
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>No content</returns>
        [HttpPost("{id}/publish")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PublishProduct(Guid id)
        {
            _logger.LogInformation("Publishing product: {ProductId}", id);

            var command = new PublishProductCommand { ProductId = id };
            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// Discontinue product (remove from sale)
        /// </summary>
        /// <param name="id">Product ID</param>
        /// <returns>No content</returns>
        [HttpPost("{id}/discontinue")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DiscontinueProduct(Guid id)
        {
            _logger.LogInformation("Discontinuing product: {ProductId}", id);

            var command = new DiscontinueProductCommand { ProductId = id };
            await _mediator.Send(command);

            return NoContent();
        }

        /// <summary>
        /// List products with pagination
        /// </summary>
        /// <param name="skip">Number of records to skip (default: 0)</param>
        /// <param name="take">Number of records to return (default: 20, max: 100)</param>
        /// <param name="categoryId">Filter by category (optional)</param>
        /// <param name="status">Filter by status (optional)</param>
        /// <returns>Paginated list of products</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListProducts(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20,
            [FromQuery] Guid? categoryId = null,
            [FromQuery] int? status = null)
        {
            _logger.LogInformation("Listing products - Skip: {Skip}, Take: {Take}", skip, take);

            var query = new ListProductsQuery
            {
                Skip = Math.Max(0, skip),
                Take = Math.Min(100, Math.Max(1, take)),
                CategoryId = categoryId,
                Status = status
            };

            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Search products by name or SKU
        /// </summary>
        /// <param name="searchTerm">Search term (name or SKU)</param>
        /// <returns>Matching products</returns>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchProducts([FromQuery] string searchTerm)
        {
            _logger.LogInformation("Searching products: {SearchTerm}", searchTerm);

            var query = new SearchProductsQuery { SearchTerm = searchTerm };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Get product by SKU
        /// </summary>
        /// <param name="sku">Product SKU</param>
        /// <returns>Product details</returns>
        [HttpGet("sku/{sku}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductBySku(string sku)
        {
            _logger.LogInformation("Fetching product by SKU: {Sku}", sku);

            var query = new GetProductBySkuQuery { Sku = sku };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", service = "ProductService" });
        }
    }
}
