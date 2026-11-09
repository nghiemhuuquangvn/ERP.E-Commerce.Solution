using Asp.Versioning;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Domain.Common;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// Production orders (BA spec 01 §5.10). Reading needs <c>manufacturing:order:view</c>; creating and changing a Draft <c>manufacturing:order:create</c>.
/// Release, cancel, hold and the other transitions follow in the next milestones.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/orders")]
public class OrdersController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, Guid ItemId, decimal Quantity, DateOnly DueDate, int? Priority, Guid? BomId, Guid? RoutingId, Guid? OutputWarehouseId, string? Notes);
    public sealed record UpdateBody(decimal Quantity, DateOnly DueDate, int? Priority, Guid? BomId, Guid? RoutingId, Guid? OutputWarehouseId, string? Notes);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] OrderStatus? status, [FromQuery] Guid? itemId, [FromQuery] DateOnly? dueFrom, [FromQuery] DateOnly? dueTo,
        [FromQuery] bool? isLate, [FromQuery] int? priority, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetOrdersQuery(plantId, status, itemId, dueFrom, dueTo, isLate, priority, search, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetOrderQuery(id), ct));

    [HttpGet("{id:guid}/history")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> History(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetOrderHistoryQuery(id), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderCreate)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created,
            await mediator.Send(new CreateOrderCommand(b.PlantId, b.ItemId, b.Quantity, b.DueDate, b.Priority, b.BomId, b.RoutingId, b.OutputWarehouseId, b.Notes), ct));

    /// <summary>Changes a Draft: quantity, BOM or routing give it a fresh snapshot (BR-PO-05); a new due date reschedules it.</summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderCreate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateOrderCommand(id, b.Quantity, b.DueDate, b.Priority, b.BomId, b.RoutingId, b.OutputWarehouseId, b.Notes), ct));
}
