using Asp.Versioning;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Features.StockDocuments;
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
    public sealed record ReasonBody(Guid? ReasonCodeId, string? Note);
    public sealed record QuantityBody(decimal Quantity, string? Note);
    public sealed record MaterialBody(IReadOnlyList<MaterialLineBody> Lines, Guid? ReasonCodeId, string? Note);
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

    /// <summary>What the Warehouse can give for each material of the order (BR-PO-07): required, still needed, allocatable, shortage.</summary>
    [HttpGet("{id:guid}/availability")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> Availability(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetOrderAvailabilityQuery(id), ct));

    /// <summary>Draft -> Released: checks the item, components and work centers, reserves the materials (min of need and available) and makes the first operation ready.</summary>
    [HttpPost("{id:guid}/release")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderRelease)]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct) => Ok(await mediator.Send(new ReleaseOrderCommand(id), ct));

    /// <summary>Released -> Draft, only before any transaction; the reservation is released.</summary>
    [HttpPost("{id:guid}/unrelease")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderRelease)]
    public async Task<IActionResult> Unrelease(Guid id, CancellationToken ct) => Ok(await mediator.Send(new UnreleaseOrderCommand(id), ct));

    /// <summary>Draft or untouched Released order -> Cancelled, with a reason of type Cancel; the reservation is released.</summary>
    [HttpPost("{id:guid}/cancel")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderCancel)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] ReasonBody b, CancellationToken ct) => Ok(await mediator.Send(new CancelOrderCommand(id, b.ReasonCodeId, b.Note), ct));

    /// <summary>Released or InProgress -> OnHold, with a reason of type Hold.</summary>
    [HttpPost("{id:guid}/hold")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderHold)]
    public async Task<IActionResult> Hold(Guid id, [FromBody] ReasonBody b, CancellationToken ct) => Ok(await mediator.Send(new HoldOrderCommand(id, b.ReasonCodeId, b.Note), ct));

    /// <summary>OnHold -> the status the order was held from.</summary>
    [HttpPost("{id:guid}/resume")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderHold)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct) => Ok(await mediator.Send(new ResumeOrderCommand(id), ct));

    /// <summary>A new quantity for a released order (never below what is completed): required quantities, operation minutes, schedule and reservation follow.</summary>
    [HttpPost("{id:guid}/change-quantity")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderChangeQty)]
    public async Task<IActionResult> ChangeQuantity(Guid id, [FromBody] QuantityBody b, CancellationToken ct) => Ok(await mediator.Send(new ChangeOrderQuantityCommand(id, b.Quantity, b.Note), ct));

    // ── Material issue and return (P3-3) ────────────────────────────────────────────────────────────────────

    /// <summary>Issues material to the order (document MI). 201 when every line is in the Warehouse, 202 when the document is stored and the Warehouse is told again later. An optional Idempotency-Key repeats safely.</summary>
    [HttpPost("{id:guid}/material-issues")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MaterialIssue)]
    public async Task<IActionResult> IssueMaterial(Guid id, [FromBody] MaterialBody b, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
        => Answer(await mediator.Send(new IssueMaterialCommand(id, b.Lines, b.ReasonCodeId, b.Note, idempotencyKey), ct));

    /// <summary>Returns issued material from the shop floor to the warehouse (document MR), at the cost it was issued.</summary>
    [HttpPost("{id:guid}/material-returns")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MaterialReturn)]
    public async Task<IActionResult> ReturnMaterial(Guid id, [FromBody] MaterialBody b, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
        => Answer(await mediator.Send(new ReturnMaterialCommand(id, b.Lines, b.ReasonCodeId, b.Note, idempotencyKey), ct));

    /// <summary>First-expiry-first lots for a material line, and the lots the order's reservation already holds for it.</summary>
    [HttpGet("{id:guid}/material-issues/suggest-lots")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MaterialIssue)]
    public async Task<IActionResult> SuggestLots(Guid id, [FromQuery] int lineNo, [FromQuery] decimal quantity, CancellationToken ct)
        => Ok(await mediator.Send(new SuggestMaterialLotsQuery(id, lineNo, quantity), ct));

    /// <summary>Every stock document of the order (issues and returns), oldest first.</summary>
    [HttpGet("{id:guid}/stock-documents")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> StockDocuments(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetOrderStockDocumentsQuery(id), ct));

    private IActionResult Answer(StockDocumentResultDto result) => StatusCode(result.IsPending ? StatusCodes.Status202Accepted : StatusCodes.Status201Created, result.Document);

    /// <summary>Changes a Draft: quantity, BOM or routing give it a fresh snapshot (BR-PO-05); a new due date reschedules it.</summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderCreate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateOrderCommand(id, b.Quantity, b.DueDate, b.Priority, b.BomId, b.RoutingId, b.OutputWarehouseId, b.Notes), ct));
}
