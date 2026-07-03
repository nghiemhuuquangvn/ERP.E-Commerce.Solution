using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PAY.API.Controllers;

[ApiController]
[Route("api/v1/payment-methods")]
public class PaymentMethodsController : ControllerBase
{
    private readonly IMediator _mediator;
    public PaymentMethodsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPaymentMethodsByCustomerQuery { CustomerId = customerId }, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SavePaymentMethodCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetByCustomer), new { customerId = result.CustomerId }, result);
    }

    [HttpPost("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, [FromBody] SetDefaultPaymentMethodCommand command, CancellationToken ct)
    {
        command.PaymentMethodId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new RemovePaymentMethodCommand { PaymentMethodId = id }, ct);
        return NoContent();
    }
}
