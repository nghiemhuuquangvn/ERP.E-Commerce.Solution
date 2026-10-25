using Asp.Versioning;
using ERP.ACC.Application.Vat;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    /// <summary>The settlement of a period's VAT (ROADMAP P1-3b).</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/vat")]
    public class VatController : ControllerBase
    {
        private readonly IMediator _mediator;

        public VatController(IMediator mediator) => _mediator = mediator;

        /// <summary>The figures of the settlement of a fiscal period (output VAT, input VAT, the part settled) and whether it was settled.</summary>
        [HttpGet("settlement")]
        [AuthorizePermission(PermissionConstants.Accounting_ReportsView)]
        [ProducesResponseType(typeof(VatSettlementDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Settlement([FromQuery] Guid fiscalPeriodId, CancellationToken ct)
        {
            return Ok(await _mediator.Send(new GetVatSettlementQuery { FiscalPeriodId = fiscalPeriodId }, ct));
        }

        /// <summary>Settles the VAT of an open fiscal period: Dr VAT output / Cr VAT input for the smaller of the two balances.</summary>
        [HttpPost("settle")]
        [AuthorizePermission(PermissionConstants.Accounting_JournalsManage)]
        [ProducesResponseType(typeof(VatSettlementDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Settle([FromBody] SettleVatCommand command, CancellationToken ct)
        {
            return Ok(await _mediator.Send(command, ct));
        }
    }
}
