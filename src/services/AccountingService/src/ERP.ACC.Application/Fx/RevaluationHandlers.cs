using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.Fx
{
    /// <summary>One open foreign document revalued at the closing rate of a period (ROADMAP P1-4).</summary>
    public class RevaluationLineDto
    {
        /// <summary>"Invoice" or "Bill".</summary>
        public string DocumentType { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;

        /// <summary>The part still open at the end of the period, in the foreign currency and as it is carried in the books.</summary>
        public decimal ForeignOutstanding { get; set; }
        public decimal CarryingAmount { get; set; }
        public decimal ClosingRate { get; set; }
        public decimal RevaluedAmount { get; set; }

        /// <summary>Positive = unrealized gain, negative = unrealized loss (a receivable worth more, a payable worth less is a gain).</summary>
        public decimal Difference { get; set; }
    }

    public class RevaluationDto
    {
        public Guid FiscalPeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime AsOfDate { get; set; }
        public string BaseCurrency { get; set; } = "VND";

        /// <summary>The rate used for each currency (typed, else the master data's of the last day of the period).</summary>
        public Dictionary<string, decimal> Rates { get; set; } = new();

        /// <summary>Currencies with open documents and no rate: nothing can be posted until they are given.</summary>
        public List<string> MissingRates { get; set; } = new();
        public List<RevaluationLineDto> Lines { get; set; } = new();
        public decimal ReceivablesDifference { get; set; }
        public decimal PayablesDifference { get; set; }
        public decimal NetDifference { get; set; }
        public bool Posted { get; set; }
        public string? EntryNumber { get; set; }
        public string? ReversalEntryNumber { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    /// <summary>The effect of the closing rates on the open foreign receivables and payables of a fiscal period; nothing is posted.</summary>
    public class GetRevaluationQuery : IRequest<RevaluationDto>
    {
        public Guid FiscalPeriodId { get; set; }

        /// <summary>Closing rates by currency that replace the master data's (base currency per 1 foreign).</summary>
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }

    /// <summary>
    /// Posts the unrealized difference of a period: Dr / Cr Accounts receivable and Accounts payable against FX gain / loss on the last day of the period, and reverses it on the next
    /// day so the realized difference of the settlement stays right. One revaluation per period.
    /// </summary>
    public class RevalueForeignBalancesCommand : IRequest<RevaluationDto>
    {
        public Guid FiscalPeriodId { get; set; }
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }

    public static class FxRevaluationMath
    {
        /// <summary>The documents' open foreign amounts at <paramref name="asOf"/> revalued at <paramref name="rates"/>; a currency without a rate is listed in <paramref name="missing"/> and its documents skipped.</summary>
        public static List<RevaluationLineDto> Compute(
            IEnumerable<Invoice> invoices, IEnumerable<Bill> bills, IReadOnlyCollection<PaymentApplication> applications,
            IReadOnlyDictionary<string, decimal> rates, DateTime asOf, ICollection<string> missing)
        {
            var end = asOf.Date;
            var lines = new List<RevaluationLineDto>();

            foreach (var invoice in invoices.Where(i => i.IsForeign && i.Status != (int)InvoiceStatus.Cancelled && i.CreatedAt.Date <= end))
            {
                var paid = applications.Where(a => a.DocumentType == PaymentApplication.InvoiceDocument && a.DocumentId == invoice.Id && a.PaidOn <= end).Sum(a => a.Amount);
                var outstanding = invoice.Amount - paid;
                Add(lines, "Invoice", invoice.Id, invoice.InvoiceNumber, invoice.ForeignCurrencyCode!, invoice.ForeignAmount!.Value, invoice.Amount, outstanding, rates, missing, receivable: true);
            }
            foreach (var bill in bills.Where(b => b.IsForeign && (b.Status == (int)BillStatus.Approved || b.Status == (int)BillStatus.Paid) && (b.ApprovedOn ?? b.CreatedAt).Date <= end))
            {
                var paid = applications.Where(a => a.DocumentType == PaymentApplication.BillDocument && a.DocumentId == bill.Id && a.PaidOn <= end).Sum(a => a.Amount);
                var outstanding = bill.Amount - paid;
                Add(lines, "Bill", bill.Id, bill.BillNumber, bill.ForeignCurrencyCode!, bill.ForeignAmount!.Value, bill.Amount, outstanding, rates, missing, receivable: false);
            }
            return lines;
        }

        private static void Add(List<RevaluationLineDto> lines, string type, Guid id, string number, string currency, decimal foreignTotal, decimal baseTotal,
            decimal outstanding, IReadOnlyDictionary<string, decimal> rates, ICollection<string> missing, bool receivable)
        {
            if (outstanding <= 0m || baseTotal <= 0m)
                return;
            if (!rates.TryGetValue(currency, out var rate) || rate <= 0m)
            {
                if (!missing.Contains(currency))
                    missing.Add(currency);
                return;
            }

            var foreignOutstanding = Math.Round(foreignTotal * outstanding / baseTotal, 2);
            var revalued = FxSupport.ToBase(foreignOutstanding, rate);
            var difference = receivable ? revalued - outstanding : outstanding - revalued;
            lines.Add(new RevaluationLineDto
            {
                DocumentType = type,
                DocumentId = id,
                Number = number,
                Currency = currency,
                ForeignOutstanding = foreignOutstanding,
                CarryingAmount = outstanding,
                ClosingRate = rate,
                RevaluedAmount = revalued,
                Difference = difference,
            });
        }
    }

    internal static class FxRevaluationSupport
    {
        public static async Task<RevaluationDto> BuildAsync(
            IAccountingUnitOfWork uow, IExchangeRateProvider provider, IVatPolicy policy, FiscalPeriod period, Dictionary<string, decimal> typedRates, CancellationToken ct)
        {
            var end = period.EndDate.Date;
            var settings = await policy.GetAsync(ct);
            var dto = new RevaluationDto { FiscalPeriodId = period.Id, PeriodName = period.PeriodName, AsOfDate = end, BaseCurrency = settings.BaseCurrency };

            var invoices = await uow.Invoices.GetForeignCreatedByAsync(end, ct) ?? new List<Invoice>();
            var bills = await uow.Bills.GetForeignCreatedByAsync(end, ct) ?? new List<Bill>();
            var applications = await uow.PaymentApplications.ListForDocumentsAsync(
                invoices.Select(i => i.Id).Concat(bills.Select(b => b.Id)).ToList(), ct) ?? new List<PaymentApplication>();

            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var (currency, rate) in typedRates)
                if (rate > 0m)
                    rates[FxSupport.Normalise(currency)] = rate;
            var currencies = invoices.Select(i => i.ForeignCurrencyCode!).Concat(bills.Select(b => b.ForeignCurrencyCode!)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var currency in currencies.Where(c => !rates.ContainsKey(c)))
            {
                var quote = await provider.GetRateAsync(currency, settings.BaseCurrency, end, ct);
                if (quote is { Rate: > 0m })
                    rates[currency] = quote.Rate;
            }

            dto.Lines = FxRevaluationMath.Compute(invoices, bills, applications, rates, end, dto.MissingRates);
            dto.Rates = dto.Lines.Select(l => l.Currency).Distinct().ToDictionary(c => c, c => rates[c]);
            dto.ReceivablesDifference = dto.Lines.Where(l => l.DocumentType == "Invoice").Sum(l => l.Difference);
            dto.PayablesDifference = dto.Lines.Where(l => l.DocumentType == "Bill").Sum(l => l.Difference);
            dto.NetDifference = dto.ReceivablesDifference + dto.PayablesDifference;
            if (dto.MissingRates.Count > 0)
                dto.Notes.Add($"No closing rate for {string.Join(", ", dto.MissingRates)}: enter it, or add it to the master data.");
            if (dto.Lines.Count == 0 && dto.MissingRates.Count == 0)
                dto.Notes.Add("There is no open foreign receivable or payable at the end of this period.");
            return dto;
        }

        public static Guid SourceId(Guid periodId) => PostingRules.ForFxRevaluation(periodId, 1m, 0m, string.Empty, DateTime.UtcNow).SourceId;
    }

    public class GetRevaluationQueryHandler : IRequestHandler<GetRevaluationQuery, RevaluationDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IExchangeRateProvider _provider;
        private readonly IVatPolicy _policy;

        public GetRevaluationQueryHandler(IAccountingUnitOfWork uow, IExchangeRateProvider provider, IVatPolicy policy)
        {
            _uow = uow;
            _provider = provider;
            _policy = policy;
        }

        public async Task<RevaluationDto> Handle(GetRevaluationQuery request, CancellationToken cancellationToken)
        {
            var period = await _uow.FiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new KeyNotFoundException($"Fiscal period {request.FiscalPeriodId} not found.");
            var dto = await FxRevaluationSupport.BuildAsync(_uow, _provider, _policy, period, request.Rates, cancellationToken);
            var entry = await _uow.JournalEntries.GetBySourceKeyAsync(JournalEntry.BuildSourceKey(PostingRules.FxRevaluation, FxRevaluationSupport.SourceId(period.Id)), cancellationToken);
            if (entry != null)
            {
                dto.Posted = true;
                dto.EntryNumber = entry.EntryNumber;
                var reversal = await _uow.JournalEntries.GetBySourceKeyAsync(JournalEntry.BuildSourceKey(PostingRules.FxRevaluation + "Reversal", FxRevaluationSupport.SourceId(period.Id)), cancellationToken);
                dto.ReversalEntryNumber = reversal?.EntryNumber;
            }
            return dto;
        }
    }

    public class RevalueForeignBalancesCommandHandler : IRequestHandler<RevalueForeignBalancesCommand, RevaluationDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IExchangeRateProvider _provider;
        private readonly IVatPolicy _policy;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<RevalueForeignBalancesCommandHandler> _logger;

        public RevalueForeignBalancesCommandHandler(IAccountingUnitOfWork uow, IExchangeRateProvider provider, IVatPolicy policy,
            IJournalPostingService posting, ILogger<RevalueForeignBalancesCommandHandler> logger)
        {
            _uow = uow;
            _provider = provider;
            _policy = policy;
            _posting = posting;
            _logger = logger;
        }

        public async Task<RevaluationDto> Handle(RevalueForeignBalancesCommand request, CancellationToken cancellationToken)
        {
            var period = await _uow.FiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new KeyNotFoundException($"Fiscal period {request.FiscalPeriodId} not found.");
            if (period.Status != (int)FiscalPeriodStatus.Open)
                throw new BusinessRuleException("Accounting", $"Fiscal period {period.PeriodName} is closed: reopen it before revaluing its foreign balances.");

            var sourceId = FxRevaluationSupport.SourceId(period.Id);
            var existing = await _uow.JournalEntries.GetBySourceKeyAsync(JournalEntry.BuildSourceKey(PostingRules.FxRevaluation, sourceId), cancellationToken);
            if (existing != null)
                throw new BusinessRuleException("Accounting", $"The foreign balances of {period.PeriodName} were already revalued by entry {existing.EntryNumber}.");

            var dto = await FxRevaluationSupport.BuildAsync(_uow, _provider, _policy, period, request.Rates, cancellationToken);
            if (dto.MissingRates.Count > 0)
                throw new BusinessRuleException("Accounting", $"No closing rate for {string.Join(", ", dto.MissingRates)}: enter it before revaluing.");
            if (dto.NetDifference == 0m && dto.ReceivablesDifference == 0m && dto.PayablesDifference == 0m)
            {
                dto.Notes.Add("Nothing to post: the closing rates leave the open foreign balances as they are booked.");
                return dto;
            }

            var entry = await _posting.TryPostAsync(_logger,
                PostingRules.ForFxRevaluation(period.Id, dto.ReceivablesDifference, dto.PayablesDifference, period.PeriodName, period.EndDate), cancellationToken);
            if (entry == null)
                throw new BusinessRuleException("Accounting", "The revaluation could not be posted: complete the posting profile (Accounts receivable, Accounts payable, FX gain and FX loss).");
            // the unrealized difference only holds until the end of the period: the next day it is taken back
            var reversal = await _posting.ReverseEntryAsync(entry, $"Reversal of the FX revaluation of {period.PeriodName}", period.EndDate.Date.AddDays(1), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            dto.Posted = true;
            dto.EntryNumber = entry.EntryNumber;
            dto.ReversalEntryNumber = reversal?.EntryNumber;
            return dto;
        }
    }
}
