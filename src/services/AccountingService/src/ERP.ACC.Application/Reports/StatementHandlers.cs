using ERP.ACC.Application.Posting;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Reports
{
    /// <summary>The balance sheet B01-DN as of a date, optionally next to a comparison date.</summary>
    public class GetBalanceSheetStatementQuery : IRequest<FinancialStatementDto>
    {
        public DateTime AsOfDate { get; set; }
        public DateTime? CompareAsOfDate { get; set; }

        /// <summary>"Vnd", "Thousand" or "Million"; empty = the report profile's unit.</summary>
        public string? Unit { get; set; }
    }

    /// <summary>The income statement B02-DN for a range, optionally next to a comparison range. Period-closing entries are left out.</summary>
    public class GetIncomeStatementQuery : IRequest<FinancialStatementDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime? CompareFromDate { get; set; }
        public DateTime? CompareToDate { get; set; }

        /// <summary>"Vnd", "Thousand" or "Million"; empty = the report profile's unit.</summary>
        public string? Unit { get; set; }
    }

    /// <summary>The cash-flow statement B03-DN (direct method) for a range, optionally next to a comparison range.</summary>
    public class GetCashFlowStatementQuery : IRequest<FinancialStatementDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime? CompareFromDate { get; set; }
        public DateTime? CompareToDate { get; set; }

        /// <summary>"Vnd", "Thousand" or "Million"; empty = the report profile's unit.</summary>
        public string? Unit { get; set; }

        /// <summary>"direct" (default) or "indirect" (ROADMAP P1-2g).</summary>
        public string? Method { get; set; }
    }

    public class GetCashFlowStatementQueryHandler : IRequestHandler<GetCashFlowStatementQuery, FinancialStatementDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;
        private readonly IReportProfileRepository _profiles;

        public GetCashFlowStatementQueryHandler(IAccountRepository accounts, IJournalLineRepository lines, IReportProfileRepository profiles)
        {
            _accounts = accounts;
            _lines = lines;
            _profiles = profiles;
        }

        public async Task<FinancialStatementDto> Handle(GetCashFlowStatementQuery request, CancellationToken cancellationToken)
        {
            if (request.FromDate.Date > request.ToDate.Date)
                throw new BusinessRuleException("Accounting", "The start date is after the end date.");
            if (request.CompareFromDate.HasValue != request.CompareToDate.HasValue)
                throw new BusinessRuleException("Accounting", "A comparison needs both its start and its end date.");
            if (request.CompareFromDate.HasValue && request.CompareFromDate.Value.Date > request.CompareToDate!.Value.Date)
                throw new BusinessRuleException("Accounting", "The comparison start date is after its end date.");

            var indirect = string.IsNullOrWhiteSpace(request.Method) || request.Method.Equals("direct", StringComparison.OrdinalIgnoreCase)
                ? false
                : request.Method.Equals("indirect", StringComparison.OrdinalIgnoreCase)
                    ? true
                    : throw new BusinessRuleException("Accounting", "The cash-flow method must be direct or indirect.");

            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var cashIds = accounts.Where(a => a.IsCash).Select(a => a.Id).ToHashSet();

            if (indirect)
                return await HandleIndirectAsync(request, accounts, cashIds, cancellationToken);

            var current = await ColumnAsync(accounts, cashIds, request.FromDate.Date, request.ToDate.Date, cancellationToken);
            CashFlowColumn? prior = request.CompareFromDate.HasValue
                ? await ColumnAsync(accounts, cashIds, request.CompareFromDate.Value.Date, request.CompareToDate!.Value.Date, cancellationToken)
                : null;

            var profile = await _profiles.GetAsync(cancellationToken);
            var unit = ReportProfileMapper.ResolveUnit(request.Unit, profile);
            var statement = CashFlowStatementEngine.Evaluate(CashFlowLayouts.For(ChartTemplates.Detect(accounts)), current, prior, ReportProfileMapper.Divisor(unit), ReportProfileMapper.Header(profile));
            if (cashIds.Count == 0)
                statement.Notes.Add("No account is flagged as cash (IsCash): flag the cash and bank accounts to get a cash-flow statement.");
            statement.FromDate = request.FromDate.Date;
            statement.ToDate = request.ToDate.Date;
            statement.CompareFromDate = request.CompareFromDate?.Date;
            statement.CompareToDate = request.CompareToDate?.Date;
            return statement;
        }

        /// <summary>The indirect method: the direct cash rows plus balance changes and the activity of each range (ROADMAP P1-2g).</summary>
        private async Task<FinancialStatementDto> HandleIndirectAsync(
            GetCashFlowStatementQuery request, List<Account> accounts, HashSet<Guid> cashIds, CancellationToken ct)
        {
            async Task<IndirectColumn> Column(DateTime from, DateTime to)
            {
                var direct = await ColumnAsync(accounts, cashIds, from, to, ct);
                var before = (await _lines.GetAccountBalancesAsync(null, from.AddDays(-1), null, ct)).ToDictionary(b => b.AccountId);
                var after = (await _lines.GetAccountBalancesAsync(null, to, null, ct)).ToDictionary(b => b.AccountId);
                var activity = (await _lines.GetActivityBalancesAsync(from, to, JournalEntry.PeriodClosingType, ct)).ToDictionary(b => b.AccountId);
                return new IndirectColumn(direct, before, after, activity);
            }

            var current = await Column(request.FromDate.Date, request.ToDate.Date);
            var prior = request.CompareFromDate.HasValue ? await Column(request.CompareFromDate.Value.Date, request.CompareToDate!.Value.Date) : null;

            var profile = await _profiles.GetAsync(ct);
            var unit = ReportProfileMapper.ResolveUnit(request.Unit, profile);
            var statement = IndirectCashFlowEngine.Evaluate(IndirectCashFlowLayouts.For(ChartTemplates.Detect(accounts)), accounts, current, prior, ReportProfileMapper.Divisor(unit), ReportProfileMapper.Header(profile));
            if (cashIds.Count == 0)
                statement.Notes.Add("No account is flagged as cash (IsCash): flag the cash and bank accounts to get a cash-flow statement.");
            statement.FromDate = request.FromDate.Date;
            statement.ToDate = request.ToDate.Date;
            statement.CompareFromDate = request.CompareFromDate?.Date;
            statement.CompareToDate = request.CompareToDate?.Date;
            return statement;
        }

        private async Task<CashFlowColumn> ColumnAsync(
            List<Account> accounts, HashSet<Guid> cashIds, DateTime from, DateTime to, CancellationToken ct)
        {
            if (cashIds.Count == 0)
                return new CashFlowColumn(Array.Empty<CashMovement>(), 0m, 0m);

            var byId = accounts.ToDictionary(a => a.Id);
            decimal Cash(IEnumerable<AccountBalanceSummary> b) => b.Where(x => cashIds.Contains(x.AccountId)).Sum(x => x.TotalDebit - x.TotalCredit);
            var opening = Cash(await _lines.GetAccountBalancesAsync(null, from.AddDays(-1), null, ct));
            var closing = Cash(await _lines.GetAccountBalancesAsync(null, to, null, ct));

            // every non-cash line of an entry that touches cash is what the money was for; entries between cash accounts move no money
            var movements = new List<CashMovement>();
            foreach (var entry in (await _lines.GetCashEntryLinesAsync(cashIds, from, to, ct)).GroupBy(r => r.EntryId))
                foreach (var line in entry.Where(r => !cashIds.Contains(r.AccountId)))
                    if (byId.TryGetValue(line.AccountId, out var account))
                        movements.Add(new CashMovement(account, line.Credit - line.Debit));

            return new CashFlowColumn(movements, opening, closing);
        }
    }

    public class GetBalanceSheetStatementQueryHandler :IRequestHandler<GetBalanceSheetStatementQuery, FinancialStatementDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;
        private readonly IReportProfileRepository _profiles;
        private readonly IInvoiceRepository _invoices;
        private readonly IBillRepository _bills;
        private readonly ILoanRepository _loans;

        public GetBalanceSheetStatementQueryHandler(
            IAccountRepository accounts, IJournalLineRepository lines, IReportProfileRepository profiles, IInvoiceRepository invoices, IBillRepository bills, ILoanRepository loans)
        {
            _loans = loans;
            _invoices = invoices;
            _bills = bills;
            _accounts = accounts;
            _lines = lines;
            _profiles = profiles;
        }

        public async Task<FinancialStatementDto> Handle(GetBalanceSheetStatementQuery request, CancellationToken cancellationToken)
        {
            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var current = (await _lines.GetAccountBalancesAsync(null, request.AsOfDate.Date, null, cancellationToken)).ToDictionary(b => b.AccountId);
            Dictionary<Guid, AccountBalanceSummary>? prior = null;
            if (request.CompareAsOfDate.HasValue)
                prior = (await _lines.GetAccountBalancesAsync(null, request.CompareAsOfDate.Value.Date, null, cancellationToken)).ToDictionary(b => b.AccountId);

            // the long-term part of trade receivables / payables comes from the due dates of the open documents (ROADMAP P1-2h)
            var invoices = await _invoices.GetUnpaidInvoicesAsync(cancellationToken);
            var bills = await _bills.GetUnpaidBillsAsync(cancellationToken);
            var loans = await _loans.ListAsync(cancellationToken);
            var maturity = MaturityFor(request.AsOfDate, accounts, current, invoices, bills, loans);
            var priorMaturity = prior != null ? MaturityFor(request.CompareAsOfDate!.Value, accounts, prior, invoices, bills, loans) : null;

            var profile = await _profiles.GetAsync(cancellationToken);
            var unit = ReportProfileMapper.ResolveUnit(request.Unit, profile);
            var statement = StatementEngine.Evaluate(StatementLayouts.For(StatementLayouts.BalanceSheet, accounts)!, accounts, current, prior,
                ReportProfileMapper.Divisor(unit), ReportProfileMapper.Header(profile), maturity, priorMaturity);
            var today = DateTime.UtcNow.Date;
            if ((maturity is { HasDocumentParts: true } && request.AsOfDate.Date < today) || (priorMaturity is { HasDocumentParts: true } && request.CompareAsOfDate!.Value.Date < today))
                statement.Notes.Add("The date is in the past: the open amount of each invoice and bill is its current open amount (payments are not kept by date), so the long-term split of an earlier date is approximate.");
            statement.ToDate = request.AsOfDate.Date;
            statement.CompareToDate = request.CompareAsOfDate?.Date;
            return statement;
        }

        private static StatementMaturity MaturityFor(
            DateTime asOf, IReadOnlyCollection<Account> accounts,
            IReadOnlyDictionary<Guid, AccountBalanceSummary> balances, List<Invoice> invoices, List<Bill> bills, List<Loan> loans)
        {
            // the ledger caps the split: debit balances of 131, credit balances of 331 (account by account, as the rows take them)
            decimal Side(string prefix, bool debit) => accounts.Where(a => a.AccountNumber.StartsWith(prefix, StringComparison.Ordinal))
                .Sum(a => balances.TryGetValue(a.Id, out var b) ? Math.Max(0m, debit ? b.TotalDebit - b.TotalCredit : b.TotalCredit - b.TotalDebit) : 0m);
            var layout = StatementLayouts.For(StatementLayouts.BalanceSheet, accounts)!;
            string[] Prefixes(string special) => layout.Rows.Where(r => r.Special == special).SelectMany(r => r.Terms).Select(x => x.Prefix).Distinct().ToArray();
            decimal Cap(string special, bool debit) => Prefixes(special).Sum(p => Side(p, debit));
            return MaturitySplit.Compute(invoices, bills, loans, asOf, Cap("ShortLessLongReceivable", debit: true), Cap("ShortLessLongPayable", debit: false), Cap("ShortLessLongLoan", debit: false));
        }
    }

    public class GetIncomeStatementQueryHandler : IRequestHandler<GetIncomeStatementQuery, FinancialStatementDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IJournalLineRepository _lines;
        private readonly IReportProfileRepository _profiles;

        public GetIncomeStatementQueryHandler(IAccountRepository accounts, IJournalLineRepository lines, IReportProfileRepository profiles)
        {
            _accounts = accounts;
            _lines = lines;
            _profiles = profiles;
        }

        public async Task<FinancialStatementDto> Handle(GetIncomeStatementQuery request, CancellationToken cancellationToken)
        {
            if (request.FromDate.Date > request.ToDate.Date)
                throw new BusinessRuleException("Accounting", "The start date is after the end date.");
            if (request.CompareFromDate.HasValue != request.CompareToDate.HasValue)
                throw new BusinessRuleException("Accounting", "A comparison needs both its start and its end date.");
            if (request.CompareFromDate.HasValue && request.CompareFromDate.Value.Date > request.CompareToDate!.Value.Date)
                throw new BusinessRuleException("Accounting", "The comparison start date is after its end date.");

            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var current = (await _lines.GetActivityBalancesAsync(request.FromDate, request.ToDate, JournalEntry.PeriodClosingType, cancellationToken))
                .ToDictionary(b => b.AccountId);
            Dictionary<Guid, AccountBalanceSummary>? prior = null;
            if (request.CompareFromDate.HasValue)
                prior = (await _lines.GetActivityBalancesAsync(request.CompareFromDate, request.CompareToDate, JournalEntry.PeriodClosingType, cancellationToken))
                    .ToDictionary(b => b.AccountId);

            var profile = await _profiles.GetAsync(cancellationToken);
            var unit = ReportProfileMapper.ResolveUnit(request.Unit, profile);
            var statement = StatementEngine.Evaluate(StatementLayouts.For(StatementLayouts.IncomeStatement, accounts)!, accounts, current, prior, ReportProfileMapper.Divisor(unit), ReportProfileMapper.Header(profile));
            statement.FromDate = request.FromDate.Date;
            statement.ToDate = request.ToDate.Date;
            statement.CompareFromDate = request.CompareFromDate?.Date;
            statement.CompareToDate = request.CompareToDate?.Date;
            return statement;
        }
    }
}
