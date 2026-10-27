using Asp.Versioning;
using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PAY.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public PaymentsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null,
        [FromQuery] string? provider = null, [FromQuery] bool needsReview = false, CancellationToken ct = default)
    {
        if (!_userContext.HasPermission("payments:view"))
            return Forbid();

        var result = await _mediator.Send(new ListPaymentsQuery { Page = page, PageSize = pageSize, Status = status, Provider = provider, NeedsReview = needsReview }, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:create"))
            return Forbid();

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:view"))
            return Forbid();

        var result = await _mediator.Send(new GetPaymentQuery { PaymentId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:view"))
            return Forbid();

        var result = await _mediator.Send(new GetPaymentByOrderQuery { OrderId = orderId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/authorize")]
    public async Task<IActionResult> Authorize(Guid id, [FromBody] AuthorizePaymentCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:authorize"))
            return Forbid();

        command.PaymentId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/capture")]
    public async Task<IActionResult> Capture(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:capture"))
            return Forbid();

        var result = await _mediator.Send(new CapturePaymentCommand { PaymentId = id }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundPaymentCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:refund"))
            return Forbid();

        command.PaymentId = id;
        try
        {
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // e.g. refund above the captured amount, or a payment that is not captured: a refusal, not a server error
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:cancel"))
            return Forbid();

        var result = await _mediator.Send(new CancelPaymentCommand { PaymentId = id }, ct);
        return Ok(result);
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/settlements")]
public class SettlementsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public SettlementsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:settlements:view"))
            return Forbid();

        var result = await _mediator.Send(new ListSettlementsQuery(), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Process([FromBody] ProcessSettlementCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:settlements:manage"))
            return Forbid();

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("reconciliation")]
    public async Task<IActionResult> Reconciliation([FromQuery] DateTime periodStart, [FromQuery] DateTime periodEnd, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:settlements:manage"))
            return Forbid();

        var result = await _mediator.Send(new GetReconciliationReportQuery { PeriodStart = periodStart, PeriodEnd = periodEnd }, ct);
        return Ok(result);
    }
}
