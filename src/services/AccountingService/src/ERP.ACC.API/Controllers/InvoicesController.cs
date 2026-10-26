using Asp.Versioning;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.EInvoicing;
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
            public List<ERP.ACC.Application.Posting.VatLineInput> Lines { get; set; } = new();
        }

        [HttpGet("{id:guid}/vat-lines")]
        public async Task<IActionResult> VatLines(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:view"))
                return Forbid();
            return Ok(await _mediator.Send(new GetVatLinesQuery { DocumentType = 1, DocumentId = id }, ct));
        }

        /// <summary>Re-splits the invoice's gross at another VAT rate and posts the difference (ROADMAP P1-3).</summary>
        [HttpPost("{id:guid}/adjust-vat")]
        public async Task<IActionResult> AdjustVat(Guid id, [FromBody] AdjustVatRequest request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();

            return Ok(await _mediator.Send(new AdjustInvoiceVatCommand { InvoiceId = id, TaxRatePercent = request.TaxRatePercent, Lines = request.Lines ?? new() }, ct));
        }

        public class CancelEInvoiceRequest
        {
            public string Reason { get; set; } = string.Empty;
        }

        public class IssueEInvoiceRequest
        {
            public string? BuyerName { get; set; }
            public string? BuyerTaxCode { get; set; }
            public string? BuyerAddress { get; set; }
        }

        /// <summary>The e-invoice providers this installation offers (for the tax settings).</summary>
        [HttpGet("e-invoice-providers")]
        public async Task<IActionResult> EInvoiceProviders(CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:view"))
                return Forbid();
            return Ok(await _mediator.Send(new ListEInvoiceProvidersQuery(), ct));
        }

        /// <summary>Issues the e-invoice of the invoice through the tenant's provider (ROADMAP P1-3). Locks the invoice's VAT.</summary>
        [HttpPost("{id:guid}/e-invoice")]
        public async Task<IActionResult> IssueEInvoice(Guid id, [FromBody] IssueEInvoiceRequest? request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();
            return Ok(await _mediator.Send(new IssueEInvoiceCommand { InvoiceId = id, BuyerName = request?.BuyerName, BuyerTaxCode = request?.BuyerTaxCode, BuyerAddress = request?.BuyerAddress }, ct));
        }

        [HttpGet("{id:guid}/e-invoice")]
        public async Task<IActionResult> GetEInvoice(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:view"))
                return Forbid();
            var result = await _mediator.Send(new GetEInvoiceQuery { InvoiceId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        public class ReplaceEInvoiceRequest
        {
            public string Reason { get; set; } = string.Empty;
            public string? BuyerName { get; set; }
            public string? BuyerTaxCode { get; set; }
            public string? BuyerAddress { get; set; }
            public decimal? TaxRatePercent { get; set; }
            public List<ERP.ACC.Application.Posting.VatLineInput> Lines { get; set; } = new();
        }

        public class AdjustEInvoiceRequest
        {
            public decimal AdjustNet { get; set; }
            public decimal AdjustTax { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        /// <summary>Every e-invoice of the invoice: original, replacements, adjustments.</summary>
        [HttpGet("{id:guid}/e-invoices")]
        public async Task<IActionResult> EInvoices(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:view"))
                return Forbid();
            return Ok(await _mediator.Send(new ListEInvoicesQuery { InvoiceId = id }, ct));
        }

        /// <summary>Replaces the issued e-invoice (a corrected VAT split can be given) — ROADMAP P1-3b.</summary>
        [HttpPost("{id:guid}/e-invoice/replace")]
        public async Task<IActionResult> ReplaceEInvoice(Guid id, [FromBody] ReplaceEInvoiceRequest request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();
            return Ok(await _mediator.Send(new ReplaceEInvoiceCommand
            {
                InvoiceId = id, Reason = request.Reason, BuyerName = request.BuyerName, BuyerTaxCode = request.BuyerTaxCode, BuyerAddress = request.BuyerAddress,
                TaxRatePercent = request.TaxRatePercent, Lines = request.Lines ?? new(),
            }, ct));
        }

        /// <summary>Issues an adjustment e-invoice (signed change of net and VAT) referring to the issued one — ROADMAP P1-3b.</summary>
        [HttpPost("{id:guid}/e-invoice/adjust")]
        public async Task<IActionResult> AdjustEInvoice(Guid id, [FromBody] AdjustEInvoiceRequest request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();
            return Ok(await _mediator.Send(new AdjustEInvoiceCommand { InvoiceId = id, AdjustNet = request.AdjustNet, AdjustTax = request.AdjustTax, Reason = request.Reason }, ct));
        }

        [HttpPost("{id:guid}/e-invoice/cancel")]
        public async Task<IActionResult> CancelEInvoice(Guid id, [FromBody] CancelEInvoiceRequest request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();
            return Ok(await _mediator.Send(new CancelEInvoiceCommand { InvoiceId = id, Reason = request.Reason }, ct));
        }

        public class MarkPaidRequest
        {
            /// <summary>The rate the money was received at; required for an invoice in a foreign currency.</summary>
            public decimal? PaymentRate { get; set; }
        }

        [HttpPost("{id:guid}/mark-paid")]
        public async Task<IActionResult> MarkPaid(Guid id, [FromBody] MarkPaidRequest? request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:invoices:manage"))
                return Forbid();

            var result = await _mediator.Send(new MarkInvoicePaidCommand { InvoiceId = id, PaymentRate = request?.PaymentRate }, ct);
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
