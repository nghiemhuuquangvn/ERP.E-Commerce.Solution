using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PAY.API.Controllers;

[ApiController]
[Route("api/v1/installment-plans")]
public class InstallmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public InstallmentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("payment/{paymentId:guid}")]
    public async Task<IActionResult> GetByPayment(Guid paymentId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInstallmentPlanByPaymentQuery { PaymentId = paymentId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstallmentPlanCommand command, CancellationToken ct)
    {
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
