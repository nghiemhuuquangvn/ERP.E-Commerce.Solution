using Asp.Versioning;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/invoices")]
    public class InvoicesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public InvoicesController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] Guid? customerId,
            [FromQuery] int? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:invoices:view"))
                return Forbid();

            var result = await _mediator.Send(new ListInvoicesQuery
            {
                CustomerId = customerId,
                Status = status,
                PageNumber = pageNumber,
                PageSize = pageSize
            }, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:view"))
                return Forbid();

            var result = await _mediator.Send(new GetInvoiceQuery { InvoiceId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateInvoiceCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        public class AdjustVatRequest
        {
            public decimal TaxRatePercent { get; set; }
        }

        /// <summary>Re-splits the invoice's gross at another VAT rate and posts the difference (ROADMAP P1-3).</summary>
        [HttpPost("{id:guid}/adjust-vat")]
        public async Task<IActionResult> AdjustVat(Guid id, [FromBody] AdjustVatRequest request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();

            return Ok(await _mediator.Send(new AdjustInvoiceVatCommand { InvoiceId = id, TaxRatePercent = request.TaxRatePercent }, ct));
        }

        [HttpPost("{id:guid}/mark-paid")]
        public async Task<IActionResult> MarkPaid(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();

            var result = await _mediator.Send(new MarkInvoicePaidCommand { InvoiceId = id }, ct);
            return Ok(result);
        }

        [HttpGet("ar-aging")]
        public async Task<IActionResult> GetARAgeing([FromQuery] DateTime? asOfDate, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:view"))
                return Forbid();

            var result = await _mediator.Send(new GetARAgingQuery { AsOfDate = asOfDate }, ct);
            return Ok(result);
        }
    }
}
