using MediatR;
using Microsoft.AspNetCore.Mvc;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Application.Features.PurchaseOrders.Queries;
using ERP.Shared.Authorization;

namespace ERP.PO.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PurchaseOrdersController> _logger;
    private readonly IUserContextService _userContextService;

    public PurchaseOrdersController(
        IMediator mediator,
        ILogger<PurchaseOrdersController> logger,
        IUserContextService userContextService)
    {
        _mediator = mediator;
        _logger = logger;
        _userContextService = userContextService;
    }

    /// <summary>
    /// Create a new purchase order
    /// Yêu cầu quyền: purchase_order:create
    /// </summary>
    /// <param name="command">Purchase order creation details</param>
    /// <returns>Created purchase order</returns>
    [HttpPost]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderDto request)
    {
        var user = _userContextService.GetCurrentUser();
        _logger.LogInformation("User {UserId} from tenant {TenantId} creating PO:{PONumber}",
            user.UserId, user.TenantId, request.PONumber);

        var command = new CreatePurchaseOrderCommand
        {
            PONumber = request.PONumber,
            SupplierId = request.SupplierId,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Notes = request.Notes
        };

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetPurchaseOrderById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get purchase order by ID
    /// Yêu cầu quyền: purchase_order:read
    /// </summary>
    /// <param name="id">Purchase order ID</param>
    /// <returns>Purchase order details</returns>
    [HttpGet("{id}")]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPurchaseOrderById(Guid id)
    {
        _logger.LogInformation("Fetching purchase order {PurchaseOrderId}", id);

        var query = new GetPurchaseOrderByIdQuery { PurchaseOrderId = id };
        var result = await _mediator.Send(query);

        if (result == null)
        {
            _logger.LogWarning("Purchase order {PurchaseOrderId} not found", id);
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get all purchase orders with optional filtering
    /// Yêu cầu quyền: purchase_order:read
    /// </summary>
    /// <param name="supplierId">Filter by supplier ID (optional)</param>
    /// <param name="status">Filter by status (optional)</param>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    /// <returns>List of purchase orders</returns>
    [HttpGet]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPurchaseOrders(
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        _logger.LogInformation("Fetching purchase orders - SupplierId: {SupplierId}, Status: {Status}, Page: {PageNumber}, PageSize: {PageSize}",
            supplierId, status, pageNumber, pageSize);

        var query = new GetPurchaseOrdersQuery
        {
            SupplierId = supplierId,
            Status = status,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Submit a draft purchase order
    /// Yêu cầu quyền: purchase_order:update
    /// </summary>
    /// <param name="id">Purchase order ID</param>
    /// <returns>No content</returns>
    [HttpPost("{id}/submit")]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitPurchaseOrder(Guid id)
    {
        _logger.LogInformation("Submitting purchase order {PurchaseOrderId}", id);

        var command = new SubmitPurchaseOrderCommand
        {
            PurchaseOrderId = id,
            SubmittedBy = "system"
        };

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Approve a submitted purchase order
    /// Yêu cầu quyền: purchase_order:approve
    /// Roles: Admin, Manager
    /// </summary>
    /// <param name="id">Purchase order ID</param>
    /// <returns>No content</returns>
    [HttpPost("{id}/approve")]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Approve)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApprovePurchaseOrder(Guid id)
    {
        _logger.LogInformation("Approving purchase order {PurchaseOrderId}", id);

        var command = new ApprovePurchaseOrderCommand
        {
            PurchaseOrderId = id,
            ApprovedBy = "system"
        };

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Cancel a purchase order
    /// Yêu cầu quyền: purchase_order:cancel
    /// Roles: Admin, Manager
    /// </summary>
    /// <param name="id">Purchase order ID</param>
    /// <returns>No content</returns>
    [HttpPost("{id}/cancel")]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_Cancel)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelPurchaseOrder(Guid id)
    {
        _logger.LogInformation("Cancelling purchase order {PurchaseOrderId}", id);

        var command = new CancelPurchaseOrderCommand
        {
            PurchaseOrderId = id,
            CancelledBy = "system"
        };

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Add item to purchase order
    /// Yêu cầu quyền: purchase_order:add_item
    /// </summary>
    /// <param name="id">Purchase order ID</param>
    /// <param name="request">Item details</param>
    /// <returns>Updated purchase order</returns>
    [HttpPost("{id}/items")]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_AddItem)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddPurchaseOrderItemDto request)
    {
        _logger.LogInformation("Adding item to purchase order {PurchaseOrderId}", id);

        var command = new AddItemCommand
        {
            PurchaseOrderId = id,
            ProductCode = request.ProductCode,
            ProductName = request.ProductName,
            Quantity = request.Quantity,
            Unit = request.Unit,
            UnitPrice = request.UnitPrice,
            AddedBy = "system"
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Remove item from purchase order
    /// Yêu cầu quyền: purchase_order:remove_item
    /// </summary>
    /// <param name="id">Purchase order ID</param>
    /// <param name="itemId">Item ID</param>
    /// <returns>Updated purchase order</returns>
    [HttpDelete("{id}/items/{itemId}")]
    [AuthorizePermission(PermissionConstants.PurchaseOrder_RemoveItem)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveItem(Guid id, Guid itemId)
    {
        _logger.LogInformation("Removing item {ItemId} from purchase order {PurchaseOrderId}", itemId, id);

        var command = new RemoveItemCommand
        {
            PurchaseOrderId = id,
            ItemId = itemId,
            RemovedBy = "system"
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
