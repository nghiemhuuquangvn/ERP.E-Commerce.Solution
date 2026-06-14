using ERP.Shared.Authorization;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SO.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
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
                return Forbid("Insufficient permissions: sales_order:create required");
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
                return Forbid("Insufficient permissions: sales_order:read required");
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
            [FromQuery] int skip = 0,
            [FromQuery] int take = 20,
            CancellationToken cancellationToken = default)
        {
            if (!_userContext.HasPermission("sales_order:read"))
            {
                return Forbid("Insufficient permissions: sales_order:read required");
            }

            var query = new ListOrdersQuery
            {
                CustomerId = customerId,
                Status = status,
                Skip = skip,
                Take = take
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
                return Forbid("Insufficient permissions: sales_order:read required");
            }

            var query = new GetOrdersByCustomerQuery(customerId);
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
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
                return Forbid("Insufficient permissions: sales_order:update required");
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
                return Forbid("Insufficient permissions: sales_order:approve required");
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
                return Forbid("Insufficient permissions: sales_order:update required");
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
                return Forbid("Insufficient permissions: sales_order:cancel required");
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
                return Forbid("Insufficient permissions: sales_order:update required");
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
    }

    public class ShipOrderRequest
    {
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
    }

    public class CancelOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
