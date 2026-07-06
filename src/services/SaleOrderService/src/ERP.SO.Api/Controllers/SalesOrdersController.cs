using ERP.Shared.Authorization;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SO.Api.Controllers
{
    [ApiController]
    [Route("api/v1/orders")]
    public class SalesOrdersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public SalesOrdersController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        /// <summary>
        /// Create a new sales order
        /// Requires: sales_order:create permission
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CreateOrderRequest request,
            CancellationToken cancellationToken)
        {
            // Authorization check
            if (!_userContext.HasPermission("sales_order:create"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:create required" });
            }

            // Validation
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new CreateOrderCommand(request);
            var result = await _mediator.Send(command, cancellationToken);

            return CreatedAtAction(nameof(GetOrderById), new { id = result.OrderId }, result);
        }

        /// <summary>
        /// Get order by ID
        /// Requires: sales_order:read permission
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrderById(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:read"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });
            }

            try
            {
                var query = new GetOrderQuery(id);
                var result = await _mediator.Send(query, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// List all sales orders with pagination and filtering
        /// Requires: sales_order:read permission
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ListOrdersResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ListOrders(
            [FromQuery] Guid? customerId,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (!_userContext.HasPermission("sales_order:read"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });
            }

            var query = new ListOrdersQuery
            {
                CustomerId = customerId,
                Status = status,
                Skip = (page - 1) * pageSize,
                Take = pageSize
            };

            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Get all orders for a specific customer
        /// Requires: sales_order:read permission
        /// </summary>
        [HttpGet("customer/{customerId:guid}")]
        [ProducesResponseType(typeof(List<SalesOrderSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrdersByCustomer(
            [FromRoute] Guid customerId,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:read"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });
            }

            var query = new GetOrdersByCustomerQuery(customerId);
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Update address/notes before the order is approved
        /// Requires: sales_order:update permission
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateOrder(
            [FromRoute] Guid id,
            [FromBody] UpdateOrderCommand command,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });

            try
            {
                command.OrderId = id;
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        /// <summary>
        /// Add an internal (non-customer-facing) note to the order
        /// Requires: sales_order:update permission
        /// </summary>
        [HttpPost("{id:guid}/notes")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddNote(
            [FromRoute] Guid id,
            [FromBody] AddOrderNoteRequest request,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });

            await _mediator.Send(new AddOrderNoteCommand { OrderId = id, Note = request.Note, AddedBy = _userContext.UserId }, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Ship specific line-item quantities — partial fulfillment. Order moves to
        /// PartiallyShipped unless this ships everything remaining, in which case it
        /// moves to Shipped (same as the full-order /ship endpoint).
        /// Requires: sales_order:update permission
        /// </summary>
        [HttpPost("{id:guid}/ship-items")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ShipLineItems(
            [FromRoute] Guid id,
            [FromBody] ShipLineItemsRequest request,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });

            try
            {
                var command = new ShipLineItemsCommand
                {
                    OrderId = id,
                    Quantities = request.Quantities,
                    TrackingNumber = request.TrackingNumber,
                    Carrier = request.Carrier
                };
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        /// <summary>
        /// Customer requests a return for a delivered order
        /// Requires: sales_order:update permission
        /// </summary>
        [HttpPost("{id:guid}/return")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RequestReturn(
            [FromRoute] Guid id,
            [FromBody] RequestReturnRequest request,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });

            try
            {
                var result = await _mediator.Send(new RequestReturnCommand { OrderId = id, Reason = request.Reason }, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        /// <summary>
        /// Marks the return as completed and records the refund amount — publishes
        /// SalesOrderReturnedEvent for Payment/Accounting/Warehouse to react to.
        /// Requires: sales_order:approve permission
        /// </summary>
        [HttpPost("{id:guid}/return/complete")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CompleteReturn(
            [FromRoute] Guid id,
            [FromBody] CompleteReturnRequest request,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:approve"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:approve required" });

            try
            {
                var result = await _mediator.Send(new CompleteReturnCommand { OrderId = id, RefundAmount = request.RefundAmount }, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        /// <summary>
        /// Submit a draft order for approval
        /// Requires: sales_order:update permission
        /// </summary>
        [HttpPost("{id:guid}/submit")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SubmitOrder(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });
            }

            try
            {
                var command = new SubmitOrderCommand { OrderId = id };
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Approve a submitted order
        /// Requires: sales_order:approve permission
        /// </summary>
        [HttpPost("{id:guid}/approve")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ApproveOrder(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:approve"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:approve required" });
            }

            try
            {
                var command = new ApproveOrderCommand { OrderId = id };
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Ship an approved order
        /// Requires: sales_order:update permission
        /// </summary>
        [HttpPost("{id:guid}/ship")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ShipOrder(
            [FromRoute] Guid id,
            [FromBody] ShipOrderRequest request,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var command = new ShipOrderCommand
                {
                    OrderId = id,
                    TrackingNumber = request.TrackingNumber,
                    Carrier = request.Carrier
                };
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Cancel an order
        /// Requires: sales_order:cancel permission
        /// </summary>
        [HttpPost("{id:guid}/cancel")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelOrder(
            [FromRoute] Guid id,
            [FromBody] CancelOrderRequest request,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:cancel"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:cancel required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var command = new CancelOrderCommand
                {
                    OrderId = id,
                    Reason = request.Reason
                };
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Mark order as delivered
        /// Requires: sales_order:update permission
        /// </summary>
        [HttpPost("{id:guid}/mark-delivered")]
        [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> MarkAsDelivered(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:update required" });
            }

            try
            {
                var command = new MarkAsDeliveredCommand { OrderId = id };
                var result = await _mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> RejectOrder(
            [FromRoute] Guid id,
            [FromBody] RejectOrderRequest request,
            CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:approve"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions" });
            try
            {
                var result = await _mediator.Send(new RejectOrderCommand { OrderId = id, Reason = request.Reason }, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPost("{id:guid}/pick")]
        public async Task<IActionResult> MarkAsPicked([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            if (!_userContext.HasPermission("sales_order:update"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions" });
            try
            {
                var result = await _mediator.Send(new MarkAsPickedCommand { OrderId = id }, cancellationToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }
    }

    public class RejectOrderRequest { public string Reason { get; set; } = string.Empty; }

    public class ShipOrderRequest
    {
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
    }

    public class CancelOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class AddOrderNoteRequest
    {
        public string Note { get; set; } = string.Empty;
    }

    public class ShipLineItemsRequest
    {
        public Dictionary<Guid, int> Quantities { get; set; } = new();
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
    }

    public class RequestReturnRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class CompleteReturnRequest
    {
        public decimal RefundAmount { get; set; }
    }
}
