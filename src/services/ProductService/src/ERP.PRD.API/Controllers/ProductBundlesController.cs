using ERP.PRD.Application.Features.Bundles.Commands;
using ERP.PRD.Application.Features.Bundles.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    [ApiController]
    [Route("api/products/{productId}/bundle-items")]
    [Authorize]
    public class ProductBundlesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProductBundlesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetBundleItems(Guid productId)
        {
            var result = await _mediator.Send(new GetBundleItemsQuery { BundleProductId = productId });
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> AddBundleItem(Guid productId, [FromBody] AddBundleItemCommand command)
        {
            command.BundleProductId = productId;
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetBundleItems), new { productId }, new { id });
        }

        [HttpDelete("{bundleItemId}")]
        public async Task<IActionResult> RemoveBundleItem(Guid productId, Guid bundleItemId)
        {
            await _mediator.Send(new RemoveBundleItemCommand { BundleItemId = bundleItemId });
            return NoContent();
        }
    }
}
