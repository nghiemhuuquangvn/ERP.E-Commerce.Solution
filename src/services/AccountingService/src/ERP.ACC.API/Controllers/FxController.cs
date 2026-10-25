using Asp.Versioning;
using ERP.ACC.Application.Fx;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    /// <summary>Foreign currency (ROADMAP P1-4): the exchange-rate suggestion for a document.</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/fx")]
    public class FxController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FxController(IMediator mediator) => _mediator = mediator;

        /// <summary>The rate (base currency per 1 unit of <c>currency</c>) the master data holds for a day; 404 when none is entered (the user types it).</summary>
        [HttpGet("rate")]
        [AuthorizePermission(PermissionConstants.Accounting_InvoicesView)]
        [ProducesResponseType(typeof(ExchangeRateQuote), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Rate([FromQuery] string currency, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var quote = await _mediator.Send(new GetExchangeRateQuery { Currency = currency, Date = date }, ct);
            return quote is null ? NotFound(new { message = $"No exchange rate for {currency} is entered in the master data." }) : Ok(quote);
        }
    }
}
