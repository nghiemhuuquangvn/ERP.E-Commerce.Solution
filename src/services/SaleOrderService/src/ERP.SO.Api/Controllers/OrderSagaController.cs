using Asp.Versioning;
using ERP.Shared.Authorization;
using ERP.SO.Application.Sagas;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SO.Api.Controllers
{
    /// <summary>How an order is going through payment, stock reservation, approval and shipment, and the retry of a stuck step (ROADMAP P1-5).</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/orders/{orderId:guid}/saga")]
    public class OrderSagaController : ControllerBase
    {
        private readonly IMediator _mediator;

        public OrderSagaController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [AuthorizePermission(PermissionConstants.SalesOrder_Read)]
        [ProducesResponseType(typeof(OrderSagaDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(Guid orderId, CancellationToken ct) =>
            Ok(await _mediator.Send(new GetOrderSagaQuery { OrderId = orderId }, ct));

        /// <summary>Publishes the command of the current step again now (payment, stock reservation or shipment); approval is never retried.</summary>
        [HttpPost("retry")]
        [AuthorizePermission(PermissionConstants.SalesOrder_Update)]
        [ProducesResponseType(typeof(OrderSagaDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Retry(Guid orderId, CancellationToken ct) =>
            Ok(await _mediator.Send(new RetryOrderSagaCommand { OrderId = orderId }, ct));
    }
}
