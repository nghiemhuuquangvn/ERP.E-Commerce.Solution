using Asp.Versioning;
using ERP.MFG.Application.Features.Receipts;
using ERP.MFG.Application.Features.StockDocuments;
using ERP.MFG.Domain.Common;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>
/// Stock documents of a plant (material issues and returns now; receipts and backflush later): the list shows what is still on its way to the Warehouse (Pending)
/// and what was refused (Failed). Reading needs <c>manufacturing:order:view</c>; retrying or abandoning a pending document <c>manufacturing:stock-document:retry</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/stock-documents")]
public class StockDocumentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] StockDocumentStatus? status, [FromQuery] StockDocumentType? type, [FromQuery] Guid? orderId,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetStockDocumentsQuery(plantId, status, type, orderId, search, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_OrderView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetStockDocumentQuery(id), ct));

    /// <summary>Sends a pending document to the Warehouse again now (the job does the same on its own schedule).</summary>
    [HttpPost("{id:guid}/retry")]
    [AuthorizePermission(PermissionConstants.Manufacturing_StockDocumentRetry)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) => Answer(await mediator.Send(new RetryStockDocumentCommand(id), ct));

    /// <summary>Gives a pending document up: whatever it already booked in the Warehouse is reversed and the document ends Failed.</summary>
    [HttpPost("{id:guid}/abandon")]
    [AuthorizePermission(PermissionConstants.Manufacturing_StockDocumentRetry)]
    public async Task<IActionResult> Abandon(Guid id, CancellationToken ct) => Answer(await mediator.Send(new AbandonStockDocumentCommand(id), ct));

    /// <summary>Interim quality release (Phase 4 replaces it by the final inspection): the lots of a posted receipt on hold move to the finished-goods warehouse and become Available.</summary>
    [HttpPost("{id:guid}/qc-release")]
    [AuthorizePermission(PermissionConstants.Manufacturing_QcDisposition)]
    public async Task<IActionResult> QcRelease(Guid id, CancellationToken ct) => Ok(await mediator.Send(new QcReleaseReceiptCommand(id), ct));

    private IActionResult Answer(StockDocumentResultDto result) => StatusCode(result.IsPending ? StatusCodes.Status202Accepted : StatusCodes.Status200OK, result.Document);
}
