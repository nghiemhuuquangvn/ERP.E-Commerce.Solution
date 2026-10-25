using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Reports
{
    /// <summary>One line of the VAT listing: a document (invoice or bill) or a ledger movement that is not one (a return, an adjustment, a POS sale, a manual entry).</summary>
    public class VatListingRowDto
    {
        /// <summary>"Invoice", "Bill" or "Entry".</summary>
        public string Kind { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public Guid? PartyId { get; set; }
        public string Description { get; set; } = string.Empty;

        /// <summary>Amount before VAT; 0 for an "Entry" row (the journal does not tell).</summary>
        public decimal Net { get; set; }

        /// <summary>Null for an "Entry" row.</summary>
        public decimal? RatePercent { get; set; }

        /// <summary>Negative for a return or any movement that reduces the VAT.</summary>
        public decimal Tax { get; set; }

        /// <summary>"Taxable" or "NotTaxable" (goods not subject to VAT, KCT); entries count as taxable.</summary>
        public string Category { get; set; } = "Taxable";
    }

    public class VatRateTotalDto
    {
        /// <summary>Null = the entries whose rate the journal does not tell.</summary>
        public decimal? RatePercent { get; set; }
        public decimal Net { get; set; }
        public decimal Tax { get; set; }
        public string Category { get; set; } = "Taxable";
    }

    public class VatListingDto
    {
        /// <summary>"output" (sales, account VAT output) or "input" (purchases, VAT input).</summary>
        public string Direction { get; set; } = "output";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<VatListingRowDto> Rows { get; set; } = new();
        public List<VatRateTotalDto> ByRate { get; set; } = new();
        public decimal TotalNet { get; set; }
        public decimal TotalTax { get; set; }

        /// <summary>The movement of the VAT account(s) in the period (credit − debit for output, debit − credit for input) and its difference to <see cref="TotalTax"/>.</summary>
        public string? LedgerAccountNumber { get; set; }
        public decimal LedgerTax { get; set; }
        public decimal Difference { get; set; }
        public bool IsReconciled { get; set; }
        public ReportHeaderDto? Header { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public class GetVatListingQuery : IRequest<VatListingDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Direction { get; set; } = "output";
    }

    public class GetVatReturnQuery : IRequest<FinancialStatementDto>
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Unit { get; set; }
    }

    /// <summary>Which accounts hold the VAT and what the journal says about them; shared by the listing and the return.</summary>
    internal static class VatLedger
    {
        public static readonly HashSet<string> DocumentTypes = new(StringComparer.Ordinal) { "Invoice", "InvoiceReversal", "Bill", "BillReversal" };

        public static Account? Resolve(IReadOnlyCollection<Account> accounts, IReadOnlyCollection<PostingAccountMapping> mappings, string key)
        {
            var mapping = mappings.FirstOrDefault(m => m.PostingKey == key);
            var found = mapping != null ? accounts.FirstOrDefault(a => a.Id == mapping.AccountId) : null;
            if (found != null)
                return found;
            var number = ChartTemplates.Find(ChartTemplates.Detect(accounts))?.PostingMap.GetValueOrDefault(key) ?? PostingKeys.DefaultAccountNumbers.GetValueOrDefault(key);
            return accounts.FirstOrDefault(a => a.AccountNumber == number);
        }

        /// <summary>The account and its sub-accounts.</summary>
        public static List<Guid> WithChildren(IReadOnlyCollection<Account> accounts, Account? account) =>
            account == null ? new List<Guid>() : accounts.Where(a => a.AccountNumber.StartsWith(account.AccountNumber, StringComparison.Ordinal)).Select(a => a.Id).ToList();
    }

    public partial class GetVatListingQueryHandler : IRequestHandler<GetVatListingQuery, VatListingDto>
    {
        private readonly IAccountRepository _accounts;
        private readonly IPostingAccountMappingRepository _mappings;
        private readonly IInvoiceRepository _invoices;
        private readonly IBillRepository _bills;
        private readonly IJournalLineRepository _lines;
        private readonly IReportProfileRepository _profiles;
        private readonly IVatLineRepository? _vatLines;

        public GetVatListingQueryHandler(
            IAccountRepository accounts, IPostingAccountMappingRepository mappings, IInvoiceRepository invoices, IBillRepository bills,
            IJournalLineRepository lines, IReportProfileRepository profiles, IVatLineRepository? vatLines = null)
        {
            _vatLines = vatLines;
            _accounts = accounts;
            _mappings = mappings;
            _invoices = invoices;
            _bills = bills;
            _lines = lines;
            _profiles = profiles;
        }

        public async Task<VatListingDto> Handle(GetVatListingQuery request, CancellationToken ct)
        {
            var output = string.Equals(request.Direction, "output", StringComparison.OrdinalIgnoreCase);
            if (!output && !string.Equals(request.Direction, "input", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Accounting", "The direction must be output or input.");
            var from = request.FromDate.Date;
            var to = request.ToDate.Date;
            if (to < from)
                throw new BusinessRuleException("Accounting", "The end date is before the start date.");

            var result = new VatListingDto { Direction = output ? "output" : "input", FromDate = from, ToDate = to };
            var accounts = await _accounts.GetAllAsync(ct);
            var mappings = await _mappings.GetAllAsync(ct);
            var vatAccount = VatLedger.Resolve(accounts, mappings, output ? PostingKeys.VatOutput : PostingKeys.VatInput);
            result.LedgerAccountNumber = vatAccount?.AccountNumber;

            if (output)
            {
                var invoices = (await _invoices.GetAllAsync(ct)).Where(i => i.Status != (int)InvoiceStatus.Cancelled && i.CreatedAt.Date >= from && i.CreatedAt.Date <= to).OrderBy(i => i.CreatedAt).ToList();
                var lineMap = await LinesOf(VatLine.InvoiceDocument, invoices.Select(i => i.Id).ToList(), ct);
                foreach (var i in invoices)
                    AddRows(result.Rows, "Invoice", i.InvoiceNumber, i.CreatedAt.Date, i.CustomerId, $"Order {i.OrderId:N}".Substring(0, 14), i.EffectiveNet, i.TaxRatePercent, i.TaxAmount, lineMap.GetValueOrDefault(i.Id));
            }
            else
            {
                var bills = (await _bills.GetAllAsync(ct)).Where(b => b.Status is (int)BillStatus.Approved or (int)BillStatus.Paid)
                    .Where(b => (b.ApprovedOn ?? b.CreatedAt).Date >= from && (b.ApprovedOn ?? b.CreatedAt).Date <= to).OrderBy(b => b.ApprovedOn ?? b.CreatedAt).ToList();
                var lineMap = await LinesOf(VatLine.BillDocument, bills.Select(b => b.Id).ToList(), ct);
                foreach (var b in bills)
                    AddRows(result.Rows, "Bill", b.BillNumber, (b.ApprovedOn ?? b.CreatedAt).Date, b.SupplierId, $"Purchase order {b.POId:N}".Substring(0, 20), b.EffectiveNet, b.TaxRatePercent, b.TaxAmount, lineMap.GetValueOrDefault(b.Id));
            }

            // what the journal holds on the VAT account that is not an invoice or a bill: returns, adjustments, POS sales, manual entries
            var ids = VatLedger.WithChildren(accounts, vatAccount);
            decimal ledgerTax = 0m;
            if (ids.Count > 0)
            {
                var rows = await _lines.GetLedgerLinesAsync(ids, from, to, ct);
                ledgerTax = rows.Sum(r => output ? r.Credit - r.Debit : r.Debit - r.Credit);
                foreach (var r in rows.Where(r => r.ReferenceType == null || !VatLedger.DocumentTypes.Contains(r.ReferenceType)).OrderBy(r => r.EntryDate).ThenBy(r => r.EntryNumber))
                {
                    var tax = output ? r.Credit - r.Debit : r.Debit - r.Credit;
                    if (tax == 0m)
                        continue;
                    result.Rows.Add(new VatListingRowDto
                    {
                        Kind = "Entry", Number = r.EntryNumber, Date = r.EntryDate.Date, Description = string.IsNullOrEmpty(r.ReferenceType) ? r.EntryDescription : $"{r.ReferenceType}: {r.EntryDescription}", Tax = tax,
                    });
                }
            }

            result.ByRate = result.Rows.GroupBy(r => (r.Category, r.RatePercent)).OrderBy(g => g.Key.Category).ThenBy(g => g.Key.RatePercent ?? decimal.MaxValue)
                .Select(g => new VatRateTotalDto { Category = g.Key.Category, RatePercent = g.Key.RatePercent, Net = g.Sum(r => r.Net), Tax = g.Sum(r => r.Tax) }).ToList();
            result.TotalNet = result.Rows.Sum(r => r.Net);
            result.TotalTax = result.Rows.Sum(r => r.Tax);
            result.LedgerTax = ledgerTax;
            result.Difference = result.TotalTax - ledgerTax;
            result.IsReconciled = result.Difference == 0m;
            result.Header = ReportProfileMapper.Header(await _profiles.GetAsync(ct));

            result.Notes.Add("Invoices and bills are listed by their VAT split; returns, VAT adjustments, POS sales and manual entries come from the journal (kind Entry, no net or rate).");
            if (!result.IsReconciled)
                result.Notes.Add("The listing differs from the VAT account of the ledger (a document cancelled after the period, a rate or entry made by hand, or an entry dated differently from its document).");
            if (accounts.Count > 0 && vatAccount == null)
                result.Notes.Add("No account is mapped to the VAT key: map it in the posting profile.");
            return result;
        }
    }

    public partial class GetVatListingQueryHandler
    {
        private async Task<Dictionary<Guid, List<VatLine>>> LinesOf(int documentType, List<Guid> ids, CancellationToken ct) =>
            _vatLines == null || ids.Count == 0
                ? new Dictionary<Guid, List<VatLine>>()
                : (await _vatLines.ListAsync(documentType, ids, ct)).GroupBy(l => l.DocumentId).ToDictionary(g => g.Key, g => g.ToList());

        /// <summary>One row per VAT line of the document; a document without stored lines is one row from its own split.</summary>
        private static void AddRows(List<VatListingRowDto> rows, string kind, string number, DateTime date, Guid party, string description, decimal net, decimal rate, decimal tax, List<VatLine>? lines)
        {
            if (lines is { Count: > 0 })
            {
                foreach (var l in lines)
                    rows.Add(new VatListingRowDto
                    {
                        Kind = kind, Number = number, Date = date, PartyId = party, Description = l.Text ?? description, Net = l.Net,
                        RatePercent = l.Category == VatLine.NotTaxable ? null : l.RatePercent, Tax = l.Tax, Category = l.Category == VatLine.NotTaxable ? "NotTaxable" : "Taxable",
                    });
                return;
            }
            rows.Add(new VatListingRowDto { Kind = kind, Number = number, Date = date, PartyId = party, Description = description, Net = net, RatePercent = rate, Tax = tax });
        }
    }

    public class GetVatReturnQueryHandler : IRequestHandler<GetVatReturnQuery, FinancialStatementDto>
    {
        private readonly IMediator _mediator;
        private readonly IAccountRepository _accounts;
        private readonly IPostingAccountMappingRepository _mappings;
        private readonly IJournalLineRepository _lines;
        private readonly IReportProfileRepository _profiles;

        public GetVatReturnQueryHandler(IMediator mediator, IAccountRepository accounts, IPostingAccountMappingRepository mappings, IJournalLineRepository lines, IReportProfileRepository profiles)
        {
            _mediator = mediator;
            _accounts = accounts;
            _mappings = mappings;
            _lines = lines;
            _profiles = profiles;
        }

        public async Task<FinancialStatementDto> Handle(GetVatReturnQuery request, CancellationToken ct)
        {
            var from = request.FromDate.Date;
            var to = request.ToDate.Date;
            var sales = await _mediator.Send(new GetVatListingQuery { FromDate = from, ToDate = to, Direction = "output" }, ct);
            var purchases = await _mediator.Send(new GetVatListingQuery { FromDate = from, ToDate = to, Direction = "input" }, ct);

            var accounts = await _accounts.GetAllAsync(ct);
            var mappings = await _mappings.GetAllAsync(ct);
            var input = VatLedger.Resolve(accounts, mappings, PostingKeys.VatInput);
            var outputAccount = VatLedger.Resolve(accounts, mappings, PostingKeys.VatOutput);
            var shared = input != null && outputAccount != null && input.Id == outputAccount.Id;

            // [22] the deductible VAT carried over from earlier periods: the debit balance of the input VAT account before the period
            // (one shared account on the 14-account chart nets input against output, so nothing is carried from it)
            var carried = 0m;
            if (input != null && !shared)
            {
                var ids = VatLedger.WithChildren(accounts, input).ToHashSet();
                var before = await _lines.GetAccountBalancesAsync(null, from.AddDays(-1), null, ct);
                carried = Math.Max(0m, before.Where(b => ids.Contains(b.AccountId)).Sum(b => b.TotalDebit - b.TotalCredit));
            }

            var profile = await _profiles.GetAsync(ct);
            var unit = ReportProfileMapper.ResolveUnit(request.Unit, profile);
            var divisor = ReportProfileMapper.Divisor(unit);
            decimal U(decimal v) => Math.Round(v / divisor, divisor == 1m ? 2 : 0, MidpointRounding.AwayFromZero);

            var inputTax = purchases.TotalTax;
            var deductible = carried + inputTax;
            var outputTax = sales.TotalTax;
            var payable = Math.Max(0m, outputTax - deductible);
            var carryForward = Math.Max(0m, deductible - outputTax);

            var rows = new List<StatementRowDto>();
            void Row(string code, string name, decimal amount, int indent = 0, bool total = false) =>
                rows.Add(new StatementRowDto { Code = code, Name = name, Amount = U(amount), Indent = indent, IsTotal = total });

            Row("22", "VAT carried over from the previous period", carried);
            Row("23", "Value of goods and services purchased in the period", purchases.ByRate.Where(g => g.Category == "Taxable").Sum(g => g.Net));
            Row("24", "Input VAT of the period", inputTax);
            Row("25", "Total deductible input VAT [22 + 24]", deductible, total: true);
            // rows of one rate add up across invoices (the listing groups by category and rate)
            var taxable = sales.ByRate.Where(g => g.Category == "Taxable" && g.RatePercent.HasValue).GroupBy(g => g.RatePercent!.Value).OrderBy(g => g.Key).ToList();
            var notTaxable = sales.ByRate.Where(g => g.Category == "NotTaxable").Sum(g => g.Net);
            if (notTaxable != 0m)
                Row("26", "Goods and services sold not subject to VAT", notTaxable);
            foreach (var group in taxable)
                Row($"S{group.Key:0.##}", $"Sales at {group.Key:0.##}%: value", group.Sum(g => g.Net), 1);
            foreach (var group in taxable)
                Row($"T{group.Key:0.##}", $"Sales at {group.Key:0.##}%: output VAT", group.Sum(g => g.Tax), 1);
            var others = sales.ByRate.FirstOrDefault(g => g.Category == "Taxable" && !g.RatePercent.HasValue);
            if (others != null)
                Row("TX", "Output VAT from returns, adjustments, POS and manual entries (no rate)", others.Tax, 1);
            Row("35", "Total output VAT of the period", outputTax, total: true);
            Row("40", "VAT payable [35 − 25, if positive]", payable, total: true);
            Row("43", "VAT to carry forward [25 − 35, if positive]", carryForward, total: true);

            var statement = new FinancialStatementDto
            {
                LayoutId = "VAT-01-GTGT",
                Title = "VAT return 01/GTGT (simplified) — draft",
                IsDraft = true,
                FromDate = from,
                ToDate = to,
                Rows = rows,
                Unit = ReportProfileMapper.Label(unit),
                UnitDivisor = divisor,
                Header = ReportProfileMapper.Header(profile),
            };
            statement.Notes.Add("Simplified form for the accountant to review: it computes the figures of the period from the VAT listings; it is not the tax authority's filing file. Codes 22–25, 35, 40 and 43 follow the form; sales are grouped by rate (S = value, T = VAT).");
            statement.Notes.Add($"Output VAT: listing {U(sales.TotalTax)} vs ledger {U(sales.LedgerTax)} ({(sales.IsReconciled ? "agrees" : "differs by " + U(sales.Difference))}); input VAT: listing {U(purchases.TotalTax)} vs ledger {U(purchases.LedgerTax)} ({(purchases.IsReconciled ? "agrees" : "differs by " + U(purchases.Difference))}).");
            if (shared)
                statement.Notes.Add("Input and output VAT share one account on this chart: the VAT carried over [22] is not taken from the ledger.");
            return statement;
        }
    }
}
