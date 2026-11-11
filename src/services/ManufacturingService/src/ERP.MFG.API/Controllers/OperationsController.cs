using Asp.Versioning;
using ERP.MFG.Application.Features.Operations;
using ERP.MFG.Application.Features.Reversals;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// Execution of the operations of an order (BA spec 01 §5.12): time logs, the operation report with its backflush, and manual complete / reopen.
/// Working needs <c>manufacturing:operation:execute</c>; completing or reopening by hand <c>manufacturing:operation:complete-manual</c>; reading <c>manufacturing:order:view</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/orders/{orderId:guid}")]
public class OperationsController(IMediator mediator) : ControllerBase
{
    public sealed record StartBody(Guid EmployeeId, Guid? MachineId);
    public sealed record ManualTimeBody(Guid EmployeeId, Guid? MachineId, DateTime StartAt, DateTime EndAt);
    public sealed record ReverseBody(string? Note);
    public sealed record ReportBody(decimal GoodQty, decimal ScrapQty, decimal ReworkQty, Guid? ScrapReasonId, Guid? ReworkReasonId, Guid? MachineId, string? Note);

    /// <summary>The operations of the order with their time logs.</summary>
    [HttpGet("operations")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> List(Guid orderId, CancellationToken ct) => Ok(await mediator.Send(new GetOrderOperationsQuery(orderId), ct));

    /// <summary>An employee starts working on a Ready, Paused or Running operation (several people may share it); the first start also starts the order.</summary>
    [HttpPost("operations/{seq:int}/start")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationExecute)]
    public async Task<IActionResult> Start(Guid orderId, int seq, [FromBody] StartBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new StartOperationCommand(orderId, seq, b.EmployeeId, b.MachineId), ct));

    /// <summary>A finished session entered afterwards (end after start, start not in the future, no overlap with the employee's other sessions).</summary>
    [HttpPost("operations/{seq:int}/time-logs")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationExecute)]
    public async Task<IActionResult> AddManualTime(Guid orderId, int seq, [FromBody] ManualTimeBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new AddManualTimeCommand(orderId, seq, b.EmployeeId, b.MachineId, b.StartAt, b.EndAt), ct));

    [HttpPost("operations/{seq:int}/time-logs/{logId:guid}/pause")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationExecute)]
    public async Task<IActionResult> Pause(Guid orderId, int seq, Guid logId, CancellationToken ct) => Ok(await mediator.Send(new PauseTimeLogCommand(logId), ct));

    [HttpPost("operations/{seq:int}/time-logs/{logId:guid}/resume")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationExecute)]
    public async Task<IActionResult> Resume(Guid orderId, int seq, Guid logId, CancellationToken ct) => Ok(await mediator.Send(new ResumeTimeLogCommand(logId), ct));

    [HttpPost("operations/{seq:int}/time-logs/{logId:guid}/stop")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationExecute)]
    public async Task<IActionResult> Stop(Guid orderId, int seq, Guid logId, CancellationToken ct) => Ok(await mediator.Send(new StopTimeLogCommand(logId), ct));

    /// <summary>
    /// Reports good, scrap and rework quantities of an operation (document OR). Backflush materials are taken out of the Warehouse with it: 201 when the report counts,
    /// 202 while its backflush document is on its way to the Warehouse, 400 when anything is refused (a stock shortage is <c>MFG_MAT_008</c>). An optional Idempotency-Key repeats safely.
    /// </summary>
    [HttpPost("operations/{seq:int}/report")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationExecute)]
    public async Task<IActionResult> Report(Guid orderId, int seq, [FromBody] ReportBody b, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var result = await mediator.Send(new ReportOperationCommand(orderId, seq, b.GoodQty, b.ScrapQty, b.ReworkQty, b.ScrapReasonId, b.ReworkReasonId, b.MachineId, b.Note, idempotencyKey), ct);
        return StatusCode(result.IsPending ? StatusCodes.Status202Accepted : StatusCodes.Status201Created, result);
    }

    /// <summary>A supervisor completes an operation by hand.</summary>
    [HttpPost("operations/{seq:int}/complete")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationCompleteManual)]
    public async Task<IActionResult> Complete(Guid orderId, int seq, CancellationToken ct) => Ok(await mediator.Send(new CompleteOperationCommand(orderId, seq), ct));

    /// <summary>A supervisor reopens a completed operation (only while the next operation has no work).</summary>
    [HttpPost("operations/{seq:int}/reopen")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationCompleteManual)]
    public async Task<IActionResult> Reopen(Guid orderId, int seq, CancellationToken ct) => Ok(await mediator.Send(new ReopenOperationCommand(orderId, seq), ct));

    /// <summary>
    /// Reverses a Posted operation report (P4-2, BR-OP-07) with a counter-report: its quantities leave the operation and the order, the material its backflush consumed goes back to the Warehouse.
    /// Refused when the output was already used downstream (<c>MFG_OP_007</c>) or the report is already reversed (<c>MFG_REV_001</c>). 201 when it counts, 202 while the material is on its way back.
    /// </summary>
    [HttpPost("operation-reports/{reportId:guid}/reverse")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OperationReverse)]
    public async Task<IActionResult> ReverseReport(Guid orderId, Guid reportId, [FromBody] ReverseBody b, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var result = await mediator.Send(new ReverseOperationReportCommand(orderId, reportId, b.Note, idempotencyKey), ct);
        return StatusCode(result.IsPending ? StatusCodes.Status202Accepted : StatusCodes.Status201Created, result);
    }

    /// <summary>The operation reports of the order, oldest first.</summary>
    [HttpGet("operation-reports")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> Reports(Guid orderId, CancellationToken ct) => Ok(await mediator.Send(new GetOperationReportsQuery(orderId), ct));
}
