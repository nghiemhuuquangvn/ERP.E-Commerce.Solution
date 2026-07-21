using ERP.POS.Application.Commands;
using ERP.POS.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.POS.API.Controllers;

[ApiController]
[Route("api/v1/pos/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public TransactionsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpPost]
    public async Task<IActionResult> Open([FromBody] OpenTransactionCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        var result = await _mediator.Send(new GetTransactionQuery { TransactionId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddTransactionItemCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        command.TransactionId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/discount")]
    public async Task<IActionResult> ApplyDiscount(Guid id, [FromBody] ApplyDiscountCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        command.TransactionId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteTransactionCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        command.TransactionId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidTransactionCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        command.TransactionId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpGet("{id:guid}/receipt")]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        var receipt = await _mediator.Send(new GetReceiptQuery { TransactionId = id }, ct);
        return Content(receipt, "text/plain");
    }

    [HttpPost("sync-offline")]
    public async Task<IActionResult> SyncOffline([FromBody] SyncOfflineTransactionsCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        return Ok(await _mediator.Send(command, ct));
    }
}

[ApiController]
[Route("api/v1/pos/products")]
public class PosProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public PosProductsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    // Barcode lookup is a step within the cashier's authenticated checkout flow
    // (scan -> lookup -> add item), the same as every action in
    // TransactionsController above (all gated by "pos:transactions") — it is not
    // called by any customer-facing surface in this repo (no storefront/admin/staff
    // client references it outside the logged-in staff app), so it gets the same
    // gate as its sibling checkout-flow actions rather than being left open.
    [HttpGet("lookup/{barcode}")]
    public async Task<IActionResult> LookupByBarcode(string barcode, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:transactions"))
            return Forbid();

        var result = await _mediator.Send(new GetProductByBarcodeQuery { Barcode = barcode }, ct);
        return result == null ? NotFound() : Ok(result);
    }
}

[ApiController]
[Route("api/v1/pos/terminals")]
public class TerminalsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public TerminalsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? isActive, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:terminals:manage"))
            return Forbid();

        return Ok(await _mediator.Send(new ListTerminalsQuery { IsActive = isActive }, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTerminalCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:terminals:manage"))
            return Forbid();

        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:terminals:manage"))
            return Forbid();

        return Ok(await _mediator.Send(new SetTerminalActiveCommand { TerminalId = id, IsActive = true }, ct));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:terminals:manage"))
            return Forbid();

        return Ok(await _mediator.Send(new SetTerminalActiveCommand { TerminalId = id, IsActive = false }, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameTerminalCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:terminals:manage"))
            return Forbid();

        command.TerminalId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    // Deliberately public: this is the secondary, customer-facing screen at a
    // checkout counter (mirrors whatever line items/total the cashier's terminal is
    // currently showing) — that hardware has no staff login of its own and no
    // frontend client in this repo drives it from an authenticated staff session, so
    // it cannot carry a JWT. It is scoped to a single known terminalId and is
    // read-only/no side effects, so there is nothing gained by forcing staff auth
    // here that the customer standing at the register can't already see directly.
    [AllowAnonymous]
    [HttpGet("{id:guid}/customer-display")]
    public async Task<IActionResult> CustomerDisplay(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCustomerDisplayQuery { TerminalId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }
}

[ApiController]
[Route("api/v1/pos/reports")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public ReportsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet("z-report")]
    public async Task<IActionResult> GetDailyZReport([FromQuery] Guid? terminalId, [FromQuery] DateTime date, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:reports:view"))
            return Forbid();

        return Ok(await _mediator.Send(new GetDailyZReportQuery { TerminalId = terminalId, Date = date }, ct));
    }
}

[ApiController]
[Route("api/v1/pos/shifts")]
public class CashShiftsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public CashShiftsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashShiftCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:shifts:manage"))
            return Forbid();

        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseCashShiftCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:shifts:manage"))
            return Forbid();

        command.ShiftId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpGet("active/{terminalId:guid}")]
    public async Task<IActionResult> GetActive(Guid terminalId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:shifts:view"))
            return Forbid();

        var result = await _mediator.Send(new GetActiveShiftQuery { TerminalId = terminalId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("pos:shifts:view"))
            return Forbid();

        var result = await _mediator.Send(new GetShiftSummaryQuery { ShiftId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }
}
