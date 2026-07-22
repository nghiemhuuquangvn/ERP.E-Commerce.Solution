using Asp.Versioning;
using ERP.PRD.Application.Features.Relations.Commands;
using ERP.PRD.Application.Features.Relations.Queries;
using ERP.PRD.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    /// <summary>Cross-sell/upsell — "customers who bought this also bought..." / "consider this instead".</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/products/{productId}/relations")]
    [Authorize]
    public class ProductRelationsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProductRelationsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetRelations(Guid productId, [FromQuery] ProductRelationType? relationType = null)
        {
            var result = await _mediator.Send(new GetProductRelationsQuery { ProductId = productId, RelationType = relationType });
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> AddRelation(Guid productId, [FromBody] AddProductRelationCommand command)
        {
            command.ProductId = productId;
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetRelations), new { productId }, new { id });
        }

        [HttpDelete("{relationId}")]
        public async Task<IActionResult> RemoveRelation(Guid productId, Guid relationId)
        {
            await _mediator.Send(new RemoveProductRelationCommand { RelationId = relationId });
            return NoContent();
        }
    }
}
