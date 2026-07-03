using ERP.PRD.Application.Features.Reviews.Commands;
using ERP.PRD.Application.Features.Reviews.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    [ApiController]
    [Route("api/products/{productId}/reviews")]
    public class ProductReviewsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProductReviewsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetReviews(Guid productId, [FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            var result = await _mediator.Send(new GetProductReviewsQuery { ProductId = productId, Skip = skip, Take = Math.Min(100, Math.Max(1, take)) });
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReview(Guid productId, [FromBody] CreateProductReviewCommand command)
        {
            command.ProductId = productId;
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetReviews), new { productId }, new { id });
        }
    }
}
