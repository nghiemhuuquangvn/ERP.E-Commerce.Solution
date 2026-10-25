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
        /// Cash-flow statement B03-DN by indicator code, direct method (method=indirect: the indirect method, starting from profit before tax) (Vas99; a DRAFT layout to be reviewed by the accountant). Each cash movement is
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
            [FromQuery] string? method = null,
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
                Unit = unit,
                Method = method
            }, ct);
            return IsExport(format) ? StatementExport(format, result, string.Equals(method, "indirect", StringComparison.OrdinalIgnoreCase) ? "cash-flow-b03-indirect" : "cash-flow-b03") : Ok(result);
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

        /// <summary>
        /// The off-balance register (class 0 accounts) per account for a range: opening, in, out, closing. Not part of the balance sheet; voided entries never count.
        /// </summary>
        [HttpGet("off-balance")]
        [ProducesResponseType(typeof(ERP.ACC.Application.Handlers.OffBalanceReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> OffBalance(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string? format = null,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();
            if (IsPdf(format))
                return PdfNotAvailable();

            var result = await _mediator.Send(new ERP.ACC.Application.Handlers.GetOffBalanceReportQuery { FromDate = fromDate, ToDate = toDate }, ct);
            return IsExport(format) ? Export(format, ReportTables.OffBalance(result), "off-balance") : Ok(result);
        }

        /// <summary>
        /// The notes B09-DN: the accountant's texts (policies, standards, non-cash transactions …) and tables computed from the books — cash, receivables, inventory,
        /// fixed assets, payables, taxes, borrowings from the loan ledger, equity, off-balance items, revenue and expenses. unit and format (csv, xlsx, pdf) as for B01; lang=vi|en picks the wording and the texts (default vi); compareFromDate / compareToDate add the prior period as a column.
        /// </summary>
        [HttpGet("statements/notes")]
        [ProducesResponseType(typeof(NotesDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Notes(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string? unit = null,
            [FromQuery] string? format = null,
            [FromQuery] string? lang = null,
            [FromQuery] DateTime? compareFromDate = null,
            [FromQuery] DateTime? compareToDate = null,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetNotesQuery
            {
                FromDate = fromDate, ToDate = toDate, Unit = unit, Language = lang, CompareFromDate = compareFromDate, CompareToDate = compareToDate,
            }, ct);
            if (IsPdf(format))
                return File(PdfWriter.ToBytes(result), "application/pdf", $"notes-b09-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
            return IsExport(format) ? Export(format, ReportTables.Notes(result), "notes-b09") : Ok(result);
        }

        /// <summary>The note sections the accountant writes (key, number, title, hint) with their current texts.</summary>
        [HttpGet("notes/text")]
        [ProducesResponseType(typeof(List<NoteTextEntryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> NoteTexts([FromQuery] string? lang = null, [FromQuery] int fiscalYear = 0, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            return Ok(await _mediator.Send(new GetNoteTextsQuery { Language = lang, FiscalYear = fiscalYear }, ct));
        }

        /// <summary>The history of one note text (section key, language, fiscal year): who saved what and when, newest first (ROADMAP P1-2l).</summary>
        [HttpGet("notes/text/history")]
        [ProducesResponseType(typeof(List<NoteTextRevisionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> NoteTextHistory([FromQuery] string key, [FromQuery] string? lang = null, [FromQuery] int fiscalYear = 0, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            return Ok(await _mediator.Send(new GetNoteTextHistoryQuery { Key = key, Language = lang, FiscalYear = fiscalYear }, ct));
        }

        /// <summary>Saves the texts of the given section keys (an empty text clears one). Same permission as the report profile.</summary>
        [HttpPut("notes/text")]
        [ProducesResponseType(typeof(List<NoteTextEntryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateNoteTexts([FromBody] UpdateNoteTextsCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:manage"))
                return Forbid();

            return Ok(await _mediator.Send(command, ct));
        }

        /// <summary>The loan ledger against the books (ROADMAP P1-2j): outstanding by the schedules, the balance of the Loan payable account and the difference. format=csv|xlsx.</summary>
        [HttpGet("loans-reconciliation")]
        [ProducesResponseType(typeof(LoanReconciliationDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> LoansReconciliation([FromQuery] DateTime asOfDate, [FromQuery] string? format = null, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetLoanReconciliationQuery { AsOfDate = asOfDate }, ct);
            return IsExport(format) ? Export(format, ReportTables.LoanReconciliation(result), "loans-reconciliation") : Ok(result);
        }

        /// <summary>
        /// The VAT listing (ROADMAP P1-3): direction=output lists the sales invoices (and returns, adjustments, POS and manual entries from the journal), direction=input the
        /// approved bills; subtotals per rate and a check against the VAT account. format=csv|xlsx.
        /// </summary>
        [HttpGet("vat/listing")]
        [ProducesResponseType(typeof(VatListingDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> VatListing(
            [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] string direction = "output", [FromQuery] string? format = null, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();
            if (IsPdf(format))
                return PdfNotAvailable();

            var result = await _mediator.Send(new GetVatListingQuery { FromDate = fromDate, ToDate = toDate, Direction = direction }, ct);
            return IsExport(format) ? Export(format, ReportTables.VatListing(result), "vat-listing-" + result.Direction) : Ok(result);
        }

        /// <summary>The simplified VAT return 01/GTGT of a period (draft for the accountant): carried-over, input and output VAT, payable or to carry forward. unit and format=csv|xlsx|pdf as for the statements.</summary>
        [HttpGet("vat/return")]
        [ProducesResponseType(typeof(FinancialStatementDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> VatReturn(
            [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] string? unit = null, [FromQuery] string? format = null, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:reports:view"))
                return Forbid();

            var result = await _mediator.Send(new GetVatReturnQuery { FromDate = fromDate, ToDate = toDate, Unit = unit }, ct);
            return IsExport(format) ? StatementExport(format, result, "vat-return-01-gtgt") : Ok(result);
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
