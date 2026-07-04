using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    [ApiController]
    [Route("api/v1/accounting/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public ReportsController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        /// <summary>
        /// Trial balance — debit/credit totals and net balance per account.
        /// Optionally filter by fiscal period or explicit date range.
        /// </summary>
        [HttpGet("trial-balance")]
        [ProducesResponseType(typeof(List<TrialBalanceLineDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> TrialBalance(
            [FromQuery] Guid? fiscalPeriodId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] bool hideZeroBalance = true,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetTrialBalanceQuery
            {
                FiscalPeriodId = fiscalPeriodId,
                FromDate = fromDate,
                ToDate = toDate,
                HideZeroBalance = hideZeroBalance
            }, ct);
            return Ok(result);
        }

        /// <summary>
        /// Profit and Loss statement — revenue and expense account balances for a date range.
        /// </summary>
        [HttpGet("profit-and-loss")]
        [ProducesResponseType(typeof(ProfitAndLossDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> ProfitAndLoss(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetProfitAndLossQuery
            {
                FromDate = fromDate,
                ToDate = toDate
            }, ct);
            return Ok(result);
        }

        /// <summary>
        /// Balance sheet — snapshot of asset, liability, and equity account balances as of a date.
        /// </summary>
        [HttpGet("balance-sheet")]
        [ProducesResponseType(typeof(BalanceSheetDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> BalanceSheet(
            [FromQuery] DateTime? asOfDate,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetBalanceSheetQuery
            {
                AsOfDate = asOfDate ?? DateTime.UtcNow
            }, ct);
            return Ok(result);
        }
    }
}
