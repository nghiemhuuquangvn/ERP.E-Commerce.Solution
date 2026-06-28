using ERP.POS.Application.Commands;
using ERP.POS.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
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
        var result = await _mediator.Send(new GetTransactionQuery { TransactionId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] AddTransactionItemCommand command, CancellationToken ct)
    {
        command.TransactionId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/discount")]
    public async Task<IActionResult> ApplyDiscount(Guid id, [FromBody] ApplyDiscountCommand command, CancellationToken ct)
    {
        command.TransactionId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteTransactionCommand command, CancellationToken ct)
    {
        command.TransactionId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/void")]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidTransactionCommand command, CancellationToken ct)
    {
        command.TransactionId = id;
        return Ok(await _mediator.Send(command, ct));
    }
}

[ApiController]
[Route("api/v1/pos/shifts")]
public class CashShiftsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashShiftsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashShiftCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseCashShiftCommand command, CancellationToken ct)
    {
        command.ShiftId = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpGet("active/{terminalId:guid}")]
    public async Task<IActionResult> GetActive(Guid terminalId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetActiveShiftQuery { TerminalId = terminalId }, ct);
        return result == null ? NotFound() : Ok(result);
    }
}
