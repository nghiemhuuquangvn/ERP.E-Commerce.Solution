using Asp.Versioning;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Queries;
using ERP.ACC.Application.Reports;
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
            [FromQuery] string? format = null,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            if (IsPdf(format))
                return PdfNotAvailable();

            var result = await _mediator.Send(new GetTrialBalanceFullQuery
            {
                FiscalPeriodId = fiscalPeriodId,
                FromDate = fromDate,
                ToDate = toDate,
                HideZeroBalance = hideZeroBalance,
                RollUp = rollUp
            }, ct);
            return IsExport(format) ? Export(format, ReportTables.TrialBalance(result), "trial-balance") : Ok(result);
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
            [FromQuery] string? format = null,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            if (IsPdf(format))
                return PdfNotAvailable();

            var result = await _mediator.Send(new GetCashFlowQuery { FromDate = fromDate, ToDate = toDate }, ct);
            return IsExport(format) ? Export(format, ReportTables.CashFlow(result), "cash-flow") : Ok(result);
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
            [FromQuery] string? format = null,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            if (IsPdf(format))
                return PdfNotAvailable();

            var export = IsExport(format);
            var result = await _mediator.Send(new GetAccountLedgerQuery
            {
                AccountId = accountId,
                FromDate = fromDate,
                ToDate = toDate,
                IncludeSubAccounts = includeSubAccounts,
                PageNumber = pageNumber,
                PageSize = pageSize,
                All = export // an export has every row, not one page
            }, ct);
            return export ? Export(format, ReportTables.Ledger(result), "account-ledger") : Ok(result);
        }

        /// <summary>
        /// Balance sheet B01-DN by indicator code (Vietnamese chart Vas99; a DRAFT layout to be reviewed by the accountant). compareAsOfDate adds a prior column;
        /// format=csv downloads it. Accounts with a balance that the layout does not take are listed under "unmapped".
        /// </summary>
        [HttpGet("statements/balance-sheet")]
        [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> BalanceSheetStatement(
            [FromQuery] DateTime? asOfDate,
            [FromQuery] DateTime? compareAsOfDate,
            [FromQuery] string? unit = null,
            [FromQuery] string? format = null,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetBalanceSheetStatementQuery
            {
                AsOfDate = asOfDate ?? DateTime.UtcNow,
                CompareAsOfDate = compareAsOfDate,
                Unit = unit
            }, ct);
            return IsExport(format) ? StatementExport(format, result, "balance-sheet-b01") : Ok(result);
        }

        /// <summary>Income statement B02-DN by indicator code (see balance-sheet). Period-closing entries are left out; compareFromDate / compareToDate add a prior column.</summary>
        [HttpGet("statements/income-statement")]
        [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> IncomeStatement(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] DateTime? compareFromDate,
            [FromQuery] DateTime? compareToDate,
            [FromQuery] string? unit = null,
            [FromQuery] string? format = null,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetIncomeStatementQuery
            {
                FromDate = fromDate,
                ToDate = toDate,
                CompareFromDate = compareFromDate,
                CompareToDate = compareToDate,
                Unit = unit
            }, ct);
            return IsExport(format) ? StatementExport(format, result, "income-statement-b02") : Ok(result);
        }

        /// <summary>
        /// Cash-flow statement B03-DN by indicator code, direct method (Vas99; a DRAFT layout to be reviewed by the accountant). Each cash movement is
        /// classified by the counter-account of its entry; compareFromDate / compareToDate add a prior column; format=csv downloads it.
        /// </summary>
        [HttpGet("statements/cash-flow")]
        [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> CashFlowStatement(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] DateTime? compareFromDate,
            [FromQuery] DateTime? compareToDate,
            [FromQuery] string? unit = null,
            [FromQuery] string? format = null,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetCashFlowStatementQuery
            {
                FromDate = fromDate,
                ToDate = toDate,
                CompareFromDate = compareFromDate,
                CompareToDate = compareToDate,
                Unit = unit
            }, ct);
            return IsExport(format) ? StatementExport(format, result, "cash-flow-b03") : Ok(result);
        }

        /// <summary>The header, signatories and amount unit of the statements.</summary>
        [HttpGet("profile")]
        [ProducesResponseType(typeof(ReportProfileDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProfile(CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            return Ok(await _mediator.Send(new GetReportProfileQuery(), ct));
        }

        /// <summary>Sets the company header, the signatories and the amount unit (VND, thousand, million) used by B01 / B02 / B03.</summary>
        [HttpPut("profile")]
        [ProducesResponseType(typeof(ReportProfileDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateReportProfileCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:manage"))
                return Forbid();

            return Ok(await _mediator.Send(command, ct));
        }

        private static bool IsPdf(string? format) => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

        private IActionResult PdfNotAvailable() => BadRequest(new ProblemDetails
        {
            Title = "Business rule violation.",
            Status = StatusCodes.Status400BadRequest,
            Detail = "PDF is available for the statements B01, B02 and B03 only; use csv or xlsx for this report.",
        });

        /// <summary>CSV, Excel or PDF of a statement (B01 / B02 / B03).</summary>
        private IActionResult StatementExport(string? format, FinancialStatementDto statement, string name) => IsPdf(format)
            ? File(PdfWriter.ToBytes(statement), "application/pdf", $"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf")
            : Export(format, ReportTables.Statement(statement), name);

        private static bool IsXlsx(string? format) => string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase);

        /// <summary>format=csv or format=xlsx asks for a file instead of JSON.</summary>
        private static bool IsExport(string? format) => IsXlsx(format) || IsPdf(format) || string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

        private FileContentResult Export(string? format, ReportTable table, string name) => IsXlsx(format)
            ? File(XlsxWriter.ToBytes(table), XlsxWriter.ContentType, $"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx")
            : File(CsvWriter.ToBytes(table.Rows), "text/csv; charset=utf-8", $"{name}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");

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
