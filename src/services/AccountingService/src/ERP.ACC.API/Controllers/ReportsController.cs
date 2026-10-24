using Asp.Versioning;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/reports")]
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
            [FromQuery] bool rollUp = true,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetTrialBalanceQuery
            {
                FiscalPeriodId = fiscalPeriodId,
                FromDate = fromDate,
                ToDate = toDate,
                HideZeroBalance = hideZeroBalance,
                RollUp = rollUp
            }, ct);
            return Ok(result);
        }

        /// <summary>
        /// Trial balance with opening, period and closing columns (bảng cân đối số phát sinh) and the grand totals with a balanced check.
        /// Opening = everything before fromDate. rollUp adds each sub-account into its parents.
        /// </summary>
        [HttpGet("trial-balance/full")]
        [ProducesResponseType(typeof(TrialBalanceDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> TrialBalanceFull(
            [FromQuery] Guid? fiscalPeriodId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] bool hideZeroBalance = true,
            [FromQuery] bool rollUp = true,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            return Ok(await _mediator.Send(new GetTrialBalanceFullQuery
            {
                FiscalPeriodId = fiscalPeriodId,
                FromDate = fromDate,
                ToDate = toDate,
                HideZeroBalance = hideZeroBalance,
                RollUp = rollUp
            }, ct));
        }

        /// <summary>
        /// Cash-flow statement, direct method: cash received and paid in the range, by the counter-account of each movement and grouped into
        /// operating, investing and financing activities (the account's cash-flow category), with opening and closing cash and a reconciliation check.
        /// Cash accounts are those flagged IsCash; transfers between them are not shown.
        /// </summary>
        [HttpGet("cash-flow")]
        [ProducesResponseType(typeof(CashFlowStatementDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> CashFlow(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            return Ok(await _mediator.Send(new GetCashFlowQuery { FromDate = fromDate, ToDate = toDate }, ct));
        }

        /// <summary>
        /// Account ledger (sổ cái / sổ chi tiết): opening balance, the journal lines of the range with counter-accounts and a running balance,
        /// closing balance. A parent account includes its sub-accounts (includeSubAccounts, default true). Paged.
        /// </summary>
        [HttpGet("account-ledger")]
        [ProducesResponseType(typeof(AccountLedgerDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> AccountLedger(
            [FromQuery] Guid accountId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] bool includeSubAccounts = true,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            return Ok(await _mediator.Send(new GetAccountLedgerQuery
            {
                AccountId = accountId,
                FromDate = fromDate,
                ToDate = toDate,
                IncludeSubAccounts = includeSubAccounts,
                PageNumber = pageNumber,
                PageSize = pageSize
            }, ct));
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
