using ERP.Shared.Authorization;
using Asp.Versioning;
using ERP.PRD.Application.Features.Reviews.Commands;
using ERP.PRD.Application.Features.Reviews.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/products/{productId}/reviews")]
    public class ProductReviewsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUserContextService _userContext;

        public ProductReviewsController(IMediator mediator, IUserContextService userContext)
        {
            _userContext = userContext;
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
        [AuthorizeAnyAuthenticatedUser]
        public async Task<IActionResult> CreateReview(Guid productId, [FromBody] CreateProductReviewCommand command)
        {
            command.ProductId = productId;
            // The reviewer is always the caller — a body-supplied CustomerId let anyone
            // post reviews in another customer's name.
            if (!Guid.TryParse(_userContext.GetUserId(), out var customerId))
                return Unauthorized();
            command.CustomerId = customerId;
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetReviews), new { productId }, new { id });
        }
    }
}
