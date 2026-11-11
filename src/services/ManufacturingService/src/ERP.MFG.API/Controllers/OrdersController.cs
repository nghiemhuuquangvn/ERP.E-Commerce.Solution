using Asp.Versioning;
using ERP.MFG.Application.Features.Orders;
using ERP.MFG.Application.Features.Receipts;
using ERP.MFG.Application.Features.Reversals;
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
    public sealed record ReceiptBody(IReadOnlyList<ReceiptLineBody> Lines, string? Note);
    public sealed record ReverseReceiptBody(string? Note);
    public sealed record CompleteBody(Guid? ReasonCodeId, string? Note);
    public sealed record CloseBody(bool ConfirmConsumeRemaining);
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

    /// <summary>The IPQC / FQC inspections of the order, newest first (P4-3).</summary>
    [HttpGet("{id:guid}/inspections")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcView)]
    public async Task<IActionResult> Inspections(Guid id, CancellationToken ct) => Ok(await mediator.Send(new ERP.MFG.Application.Features.Inspections.GetOrderInspectionsQuery(id), ct));

    // ── Finished goods, complete, reopen, close (P3-5) ───────────────────────────────────────────────────────

    /// <summary>
    /// Receives finished goods (document FR): the main product and optionally co- / by-products, with lots, quality hold for items that need final inspection and the backflush of
    /// materials no operation owns. 201 when every line is in the Warehouse, 202 while the document waits for it. Receiving all of the quantity completes the order.
    /// </summary>
    [HttpPost("{id:guid}/receipts")]
    [AuthorizePermission(PermissionConstants.Manufacturing_ReceiptPost)]
    public async Task<IActionResult> Receive(Guid id, [FromBody] ReceiptBody b, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
        => Answer(await mediator.Send(new ReceiveFinishedGoodsCommand(id, b.Lines, b.Note, idempotencyKey), ct));

    /// <summary>
    /// Reverses a Posted receipt (P4-2, BR-FG-07; document RV): the finished goods leave stock again, what its backflush consumed comes back, <c>CompletedQty</c> goes down and a Completed order below the
    /// completion threshold is reopened. Refused when the lot was released from quality hold or used (<c>MFG_FG_004</c>). 201 when Posted, 202 while the Warehouse is told again later.
    /// </summary>
    [HttpPost("{id:guid}/receipts/{documentId:guid}/reverse")]
    [AuthorizePermission(PermissionConstants.Manufacturing_ReceiptReverse)]
    public async Task<IActionResult> ReverseReceipt(Guid id, Guid documentId, [FromBody] ReverseReceiptBody b, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
        => Answer(await mediator.Send(new ReverseReceiptCommand(id, documentId, b.Note, idempotencyKey), ct));

    /// <summary>InProgress -> Completed by hand. At or above the under-completion tolerance no reason is needed; below it a short-close needs <c>order:short-close</c> and a ShortClose reason.</summary>
    [HttpPost("{id:guid}/complete")]
    [AuthorizeAnyPermission(PermissionConstants.Manufacturing_OperationCompleteManual, PermissionConstants.Manufacturing_OrderShortClose)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteBody b, CancellationToken ct) => Ok(await mediator.Send(new CompleteOrderCommand(id, b.ReasonCodeId, b.Note), ct));

    /// <summary>Completed -> InProgress.</summary>
    [HttpPost("{id:guid}/reopen")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderReopen)]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken ct) => Ok(await mediator.Send(new ReopenOrderCommand(id), ct));

    /// <summary>The conditions of BR-PO-16 and whether each is met (optionally as they would be if the rest of the issued material is carried to consumption).</summary>
    [HttpGet("{id:guid}/close-checklist")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> CloseChecklist(Guid id, [FromQuery] bool confirmConsumeRemaining = false, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetCloseChecklistQuery(id, confirmConsumeRemaining), ct));

    /// <summary>Completed -> Closed; read only afterwards. With <c>confirmConsumeRemaining</c> the issued material nobody returned is carried to consumption.</summary>
    [HttpPost("{id:guid}/close")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderClose)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseBody b, CancellationToken ct) => Ok(await mediator.Send(new CloseOrderCommand(id, b.ConfirmConsumeRemaining), ct));

    private IActionResult Answer(StockDocumentResultDto result) => StatusCode(result.IsPending ? StatusCodes.Status202Accepted : StatusCodes.Status201Created, result.Document);

    /// <summary>Changes a Draft: quantity, BOM or routing give it a fresh snapshot (BR-PO-05); a new due date reschedules it.</summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderCreate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateOrderCommand(id, b.Quantity, b.DueDate, b.Priority, b.BomId, b.RoutingId, b.OutputWarehouseId, b.Notes), ct));
}
