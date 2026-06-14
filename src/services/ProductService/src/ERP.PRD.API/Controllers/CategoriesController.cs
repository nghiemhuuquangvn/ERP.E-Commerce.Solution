using ERP.PRD.Application.Features.Categories.Commands;
using ERP.PRD.Application.Features.Categories.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    [ApiController]
    [Route("api/categories")]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(IMediator mediator, ILogger<CategoriesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Create a new product category
        /// </summary>
        /// <param name="command">Category creation details</param>
        /// <returns>Created category with ID</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryCommand command)
        {
            _logger.LogInformation("Creating category: {Name}", command.Name);

            var result = await _mediator.Send(command);

            return CreatedAtAction(nameof(GetCategories), result);
        }

        /// <summary>
        /// Get all categories (root level or by parent)
        /// </summary>
        /// <param name="parentId">Parent category ID (optional)</param>
        /// <returns>List of categories</returns>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategories([FromQuery] Guid? parentId)
        {
            _logger.LogInformation("Fetching categories with parent: {ParentId}", parentId ?? Guid.Empty);

            var query = new GetCategoriesQuery { ParentCategoryId = parentId };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Update a category
        /// </summary>
        /// <param name="categoryId">Category ID</param>
        /// <param name="command">Update details</param>
        /// <returns>No content</returns>
        [HttpPut("{categoryId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCategory(Guid categoryId, [FromBody] UpdateCategoryCommand command)
        {
            command.CategoryId = categoryId;
            _logger.LogInformation("Updating category: {CategoryId}", categoryId);

            await _mediator.Send(command);

            return NoContent();
        }
    }
}
