using Asp.Versioning;
using ERP.Shared.Authorization;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SO.Api.Controllers
{
    /// <summary>Sibling to SalesOrdersController — same route-versioning convention, real path api/v1/quotations (not the business doc's proposed /sales/quotations).</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/quotations")]
    public class QuotationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public QuotationsController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [HttpPost]
        [ProducesResponseType(typeof(SalesQuotationDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateQuotation([FromBody] CreateQuotationRequest request, CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:create"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:create required" });

            var result = await _mediator.Send(new CreateQuotationCommand(request), cancellationToken);
            return CreatedAtAction(nameof(GetQuotationById), new { id = result.QuotationId }, result);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SalesQuotationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetQuotationById([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:read"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });

            try
            {
                var result = await _mediator.Send(new GetQuotationQuery(id), cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpGet]
        [ProducesResponseType(typeof(ListQuotationsResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListQuotations(
            [FromQuery] Guid? customerId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (!_userContext.HasPermission("sales_order:read"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });

            var result = await _mediator.Send(new ListQuotationsQuery
            {
                CustomerId = customerId,
                Skip = (page - 1) * pageSize,
                Take = pageSize
            }, cancellationToken);

            return Ok(result);
        }

        [HttpPost("{id:guid}/send")]
        [ProducesResponseType(typeof(SalesQuotationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Send([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });

            try
            {
                var result = await _mediator.Send(new SendQuotationCommand { QuotationId = id }, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        /// <summary>Records the customer's approval — the trigger for eligibility to convert into a real SalesOrder.</summary>
        [HttpPost("{id:guid}/accept")]
        [ProducesResponseType(typeof(SalesQuotationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Accept([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });

            try
            {
                var result = await _mediator.Send(new AcceptQuotationCommand { QuotationId = id }, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPost("{id:guid}/reject")]
        [ProducesResponseType(typeof(SalesQuotationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reject([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });

            try
            {
                var result = await _mediator.Send(new RejectQuotationCommand { QuotationId = id }, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        /// <summary>Converts an Accepted quotation into a real SalesOrder, carrying its snapshotted pricing over as-is.</summary>
        [HttpPost("{id:guid}/convert-to-order")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ConvertToOrder([FromRoute] Guid id, [FromBody] ConvertQuotationToOrderRequest request, CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:create"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:create required" });

            try
            {
                var result = await _mediator.Send(new ConvertQuotationToOrderCommand { QuotationId = id, ShippingAddress = request.ShippingAddress }, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }
    }

    public class ConvertQuotationToOrderRequest
    {
        public AddressDto ShippingAddress { get; set; } = new();
    }
}
