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
    [Route("api/v{version:apiVersion}/bills")]
    public class BillsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public BillsController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] Guid? supplierId,
            [FromQuery] int? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:bills:view"))
                return Forbid();

            var result = await _mediator.Send(new ListBillsQuery
            {
                SupplierId = supplierId,
                Status = status,
                PageNumber = pageNumber,
                PageSize = pageSize
            }, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:bills:view"))
                return Forbid();

            var result = await _mediator.Send(new GetBillQuery { BillId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBillCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:bills:manage"))
                return Forbid();

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        public class ApproveBillRequest
        {
            public decimal? TaxRatePercent { get; set; }
            public List<ERP.ACC.Application.Posting.VatLineInput> Lines { get; set; } = new();
        }

        /// <summary>Approves the bill; the supplier's real VAT rate (or the VAT lines) can be given and re-split the bill before it is posted (ROADMAP P1-3 / P1-3b).</summary>
        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveBillRequest? request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:bills:manage"))
                return Forbid();

            var result = await _mediator.Send(new ApproveBillCommand { BillId = id, TaxRatePercent = request?.TaxRatePercent, Lines = request?.Lines ?? new() }, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}/vat-lines")]
        public async Task<IActionResult> VatLines(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:bills:view"))
                return Forbid();
            return Ok(await _mediator.Send(new GetVatLinesQuery { DocumentType = 2, DocumentId = id }, ct));
        }

        public class MarkPaidRequest
        {
            /// <summary>The rate the supplier was paid at; required for a bill in a foreign currency.</summary>
            public decimal? PaymentRate { get; set; }
        }

        [HttpPost("{id:guid}/mark-paid")]
        public async Task<IActionResult> MarkPaid(Guid id, [FromBody] MarkPaidRequest? request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:bills:manage"))
                return Forbid();

            var result = await _mediator.Send(new MarkBillPaidCommand { BillId = id, PaymentRate = request?.PaymentRate }, ct);
            return Ok(result);
        }

        [HttpGet("ap-aging")]
        public async Task<IActionResult> GetAPAgeing([FromQuery] DateTime? asOfDate, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:bills:view"))
                return Forbid();

            var result = await _mediator.Send(new GetAPAgingQuery { AsOfDate = asOfDate }, ct);
            return Ok(result);
        }
    }
}
