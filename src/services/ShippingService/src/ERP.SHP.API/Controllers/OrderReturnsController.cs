using Asp.Versioning;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Handlers;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Authorization.ServiceAuth;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SHP.API.Controllers;

public class BookOrderReturnRequest
{
    public Guid OrderId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Service-to-service (ROADMAP P1-11d): SaleOrderService approves a return and Shipping books the reverse parcels at the carrier. Asking again books only what is not booked.</summary>
[ApiController]
[Route("internal/v1/returns")]
public class InternalReturnsController : ControllerBase
{
    private readonly ReturnBookingService _returns;

    public InternalReturnsController(ReturnBookingService returns) => _returns = returns;

    [HttpPost]
    [AuthorizeInternalService("salesorder")]
    public async Task<IActionResult> Book([FromBody] BookOrderReturnRequest request, CancellationToken ct)
    {
        var booked = await _returns.BookReturnsAsync(request.OrderId, string.IsNullOrWhiteSpace(request.Reason) ? "customer return" : request.Reason!, ct);
        return Ok(booked.Select(InitiateReturnCommandHandler.MapToDto).ToList());
    }
}

/// <summary>The return parcels of an order (admin: order detail, Shipments screen).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipments/order-returns")]
public class OrderReturnsController : ControllerBase
{
    private readonly IShippingUnitOfWork _uow;

    public OrderReturnsController(IShippingUnitOfWork uow) => _uow = uow;

    [HttpGet("{orderId:guid}")]
    [AuthorizePermission("shipping:view")]
    public async Task<IActionResult> ForOrder(Guid orderId, CancellationToken ct) =>
        Ok((await _uow.ReturnShipments.GetTrackedByOrderIdAsync(orderId, ct)).Select(InitiateReturnCommandHandler.MapToDto).ToList());
}

/// <summary>Admin Returns screen (ROADMAP P2-1b): every return parcel of the shop, and the two actions the screen needs beyond the per-shipment ones.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipments/returns")]
public class ReturnsAdminController : ControllerBase
{
    private readonly MediatR.IMediator _mediator;

    public ReturnsAdminController(MediatR.IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [AuthorizePermission("shipping:view")]
    public async Task<IActionResult> List([FromQuery] ERP.SHP.Application.Queries.ListReturnsQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    [HttpPost("{returnId:guid}/rebook")]
    [AuthorizePermission("shipping:update")]
    public async Task<IActionResult> Rebook(Guid returnId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ERP.SHP.Application.Commands.RebookReturnCommand { ReturnShipmentId = returnId }, ct));

    [HttpPost("{returnId:guid}/cancel")]
    [AuthorizePermission("shipping:update")]
    public async Task<IActionResult> CancelAtCarrier(Guid returnId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ERP.SHP.Application.Commands.CancelReturnAtCarrierCommand { ReturnShipmentId = returnId }, ct));
}
