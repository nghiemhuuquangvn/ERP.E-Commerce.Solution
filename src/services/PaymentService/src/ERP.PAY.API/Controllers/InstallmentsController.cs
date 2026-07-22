using Asp.Versioning;
using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PAY.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/installment-plans")]
public class InstallmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public InstallmentsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet("payment/{paymentId:guid}")]
    public async Task<IActionResult> GetByPayment(Guid paymentId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:view"))
            return Forbid();

        var result = await _mediator.Send(new GetInstallmentPlanByPaymentQuery { PaymentId = paymentId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstallmentPlanCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:create"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetByPayment), new { paymentId = result.PaymentId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/installments/{sequenceNo:int}/pay")]
    public async Task<IActionResult> RecordPayment(Guid id, int sequenceNo, CancellationToken ct)
    {
        if (!_userContext.HasPermission("payments:capture"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new RecordInstallmentPaymentCommand { InstallmentPlanId = id, SequenceNo = sequenceNo }, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
