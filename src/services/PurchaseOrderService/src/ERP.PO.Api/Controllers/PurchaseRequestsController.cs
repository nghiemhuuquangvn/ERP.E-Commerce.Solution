using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ERP.PO.Application.Features.PurchaseRequests;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;

namespace ERP.PO.Api.Controllers;

/// <summary>
/// Suggestion-only purchase requests, auto-created from WareHouseService's
/// StockAlertEvent (see WarehouseEventRabbitMqConsumer) — not a real PurchaseOrder
/// workflow. Convert only marks the suggestion as acted-upon; creating the actual
/// PurchaseOrder still goes through PurchaseOrdersController/CreatePurchaseOrderCommand.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/purchase-requests")]
public class PurchaseRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [AuthorizePermission(PermissionConstants.PurchaseRequest_View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PurchaseRequestStatus? status = null)
    {
        var result = await _mediator.Send(new GetPurchaseRequestsQuery { Status = status });
        return Ok(result);
    }

    /// <summary>Creates draft purchase orders (grouped by preferred supplier) and one RFQ (products without a preferred supplier) from the open requests, or from the given ids.</summary>
    [HttpPost("create-orders")]
    [AuthorizePermission(PermissionConstants.PurchaseRequest_Manage)]
    [ProducesResponseType(typeof(CreateOrdersResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrders([FromBody] CreateOrdersFromPurchaseRequestsCommand command) =>
        Ok(await _mediator.Send(command));

    [HttpPost("{id}/dismiss")]
    [AuthorizePermission(PermissionConstants.PurchaseRequest_Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Dismiss(Guid id)
    {
        try
        {
            var result = await _mediator.Send(new DismissPurchaseRequestCommand { PurchaseRequestId = id });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/convert")]
    [AuthorizePermission(PermissionConstants.PurchaseRequest_Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Convert(Guid id)
    {
        try
        {
            var result = await _mediator.Send(new ConvertPurchaseRequestCommand { PurchaseRequestId = id });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
