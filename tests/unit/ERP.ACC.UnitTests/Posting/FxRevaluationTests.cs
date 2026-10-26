using ERP.ACC.Application.Fx;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-4: the period-end revaluation of open foreign receivables and payables (unrealized difference, reversed the next day).</summary>
public class FxRevaluationTests
{
    private static readonly DateTime End = new(2026, 10, 31);

    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey!).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    private static Invoice UsdInvoice(decimal usd = 1_000m, decimal rate = 24_000m, DateTime? created = null)
    {
        var invoice = Invoice.Create("INV-USD", Guid.NewGuid(), Guid.NewGuid(), Math.Round(usd * rate, 2), End.AddDays(30));
        invoice.SetForeign("USD", usd, rate);
        invoice.CreatedAt = created ?? new DateTime(2026, 10, 5);
        return invoice;
    }

    private static Bill ApprovedUsdBill(decimal usd = 500m, decimal rate = 24_000m, DateTime? created = null)
    {
        var bill = Bill.Create("BILL-USD", Guid.NewGuid(), Guid.NewGuid(), Math.Round(usd * rate, 2), End.AddDays(30));
        bill.SetForeign("USD", usd, rate);
        bill.CreatedAt = created ?? new DateTime(2026, 10, 6);
        bill.Approve();
        bill.ApprovedOn = bill.CreatedAt;
        return bill;
    }

    private static Dictionary<string, decimal> Usd(decimal rate) => new() { ["USD"] = rate };

    // ---- the maths ----

    [Fact]
    public void An_open_receivable_worth_more_at_the_closing_rate_is_a_gain_and_a_payable_worth_more_is_a_loss()
    {
        var missing = new List<string>();

        var lines = FxRevaluationMath.Compute(new[] { UsdInvoice() }, new[] { ApprovedUsdBill() }, new List<PaymentApplication>(), Usd(24_500m), End, missing);

        missing.Should().BeEmpty();
        var invoice = lines.Single(l => l.DocumentType == "Invoice");
        (invoice.CarryingAmount, invoice.RevaluedAmount, invoice.Difference).Should().Be((24_000_000m, 24_500_000m, 500_000m));
        var bill = lines.Single(l => l.DocumentType == "Bill");
        (bill.CarryingAmount, bill.RevaluedAmount, bill.Difference).Should().Be((12_000_000m, 12_250_000m, -250_000m));
    }

    [Fact]
    public void Only_the_open_part_at_the_end_of_the_period_is_revalued()
    {
        var invoice = UsdInvoice();
        var half = PaymentApplication.Create(PaymentApplication.InvoiceDocument, invoice.Id, new DateTime(2026, 10, 20), 12_000_000m, "p1");
        var later = PaymentApplication.Create(PaymentApplication.InvoiceDocument, invoice.Id, new DateTime(2026, 11, 3), 12_000_000m, "p2");   // after the period end: still open on 31 Oct

        var lines = FxRevaluationMath.Compute(new[] { invoice }, Array.Empty<Bill>(), new[] { half, later }, Usd(24_500m), End, new List<string>());

        var line = lines.Single();
        (line.ForeignOutstanding, line.CarryingAmount, line.Difference).Should().Be((500m, 12_000_000m, 250_000m));
    }

    [Fact]
    public void Documents_that_do_not_count_are_left_out()
    {
        var cancelled = UsdInvoice(); cancelled.Cancel();
        var paid = UsdInvoice();
        var payment = PaymentApplication.Create(PaymentApplication.InvoiceDocument, paid.Id, new DateTime(2026, 10, 10), paid.Amount, "p");
        var later = UsdInvoice(created: new DateTime(2026, 11, 2));
        var pendingBill = Bill.Create("B", Guid.NewGuid(), Guid.NewGuid(), 1_000m, End); pendingBill.SetForeign("USD", 1m, 1_000m);
        var baseInvoice = Invoice.Create("INV-BASE", Guid.NewGuid(), Guid.NewGuid(), 1_000_000m, End);

        var lines = FxRevaluationMath.Compute(new[] { cancelled, paid, later, baseInvoice }, new[] { pendingBill }, new[] { payment }, Usd(25_000m), End, new List<string>());

        lines.Should().BeEmpty();
    }

    [Fact]
    public void A_currency_without_a_rate_is_reported_and_its_documents_skipped()
    {
        var missing = new List<string>();

        var lines = FxRevaluationMath.Compute(new[] { UsdInvoice() }, Array.Empty<Bill>(), new List<PaymentApplication>(), new Dictionary<string, decimal>(), End, missing);

        lines.Should().BeEmpty();
        missing.Should().Equal("USD");
    }

    [Theory]
    [InlineData(500_000, 0, 500_000, 0)]          // receivable gain only
    [InlineData(-300_000, 0, 0, 300_000)]         // receivable loss only
    [InlineData(500_000, -250_000, 250_000, 0)]   // gain on receivables, loss on payables: net gain
    [InlineData(100_000, 400_000, 500_000, 0)]    // payable gain too
    [InlineData(100_000, -400_000, 0, 300_000)]
    public void The_revaluation_entry_balances_with_the_net_in_the_FX_account(int receivables, int payables, int gain, int loss)
    {
        var request = PostingRules.ForFxRevaluation(Guid.NewGuid(), receivables, payables, "10/2026", End);

        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
        var lines = ByKey(request);
        if (gain > 0) lines[PostingKeys.FxGain].Should().Be((0m, gain));
        else lines.ContainsKey(PostingKeys.FxGain).Should().BeFalse();
        if (loss > 0) lines[PostingKeys.FxLoss].Should().Be((loss, 0m));
        else lines.ContainsKey(PostingKeys.FxLoss).Should().BeFalse();
    }

    [Fact]
    public void The_revaluation_source_is_stable_per_period()
    {
        var period = Guid.NewGuid();

        PostingRules.ForFxRevaluation(period, 1m, 0m, "a", End).SourceId.Should().Be(PostingRules.ForFxRevaluation(period, 9m, 9m, "b", End).SourceId);
        PostingRules.ForFxRevaluation(Guid.NewGuid(), 1m, 0m, "a", End).SourceId.Should().NotBe(PostingRules.ForFxRevaluation(period, 1m, 0m, "a", End).SourceId);
    }

    // ---- handler ----

    private class Harness
    {
        public readonly Mock<IAccountingUnitOfWork> Uow = new();
        public readonly Mock<IJournalPostingService> Posting = new();
        public readonly Mock<IExchangeRateProvider> Provider = new();
        public readonly List<PostingRequest> Posted = new();
        public readonly FiscalPeriod Period = FiscalPeriod.Create("10/2026", new DateTime(2026, 10, 1), End);
        public JournalEntry? Existing;

        public Harness(IEnumerable<Invoice> invoices, IEnumerable<Bill> bills, IEnumerable<PaymentApplication>? applications = null, decimal? masterRate = 24_500m)
        {
            var periods = new Mock<IFiscalPeriodRepository>();
            periods.Setup(p => p.GetByIdAsync(Period.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Period);
            var invoiceRepo = new Mock<IInvoiceRepository>();
            invoiceRepo.Setup(r => r.GetForeignCreatedByAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(invoices.ToList());
            var billRepo = new Mock<IBillRepository>();
            billRepo.Setup(r => r.GetForeignCreatedByAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(bills.ToList());
            var apps = new Mock<IPaymentApplicationRepository>();
            apps.Setup(r => r.ListForDocumentsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync((applications ?? Array.Empty<PaymentApplication>()).ToList());
            var journals = new Mock<IJournalEntryRepository>();
            journals.Setup(j => j.GetBySourceKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Existing);
            Uow.SetupGet(u => u.FiscalPeriods).Returns(periods.Object);
            Uow.SetupGet(u => u.Invoices).Returns(invoiceRepo.Object);
            Uow.SetupGet(u => u.Bills).Returns(billRepo.Object);
            Uow.SetupGet(u => u.PaymentApplications).Returns(apps.Object);
            Uow.SetupGet(u => u.JournalEntries).Returns(journals.Object);
            Posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PostingRequest, CancellationToken>((r, _) => Posted.Add(r)).ReturnsAsync(() => JournalEntry.Create("JE-REVAL", Period.Id, "x"));
            Posting.Setup(p => p.ReverseEntryAsync(It.IsAny<JournalEntry>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => JournalEntry.Create("JE-REV", Period.Id, "y"));
            Provider.Setup(p => p.GetRateAsync("USD", "VND", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(masterRate is null ? null : new ExchangeRateQuote("USD", "VND", masterRate.Value, End, false));
        }

        private IVatPolicy Policy()
        {
            var vat = new Mock<IVatPolicy>();
            vat.Setup(v => v.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VatSettings(0m, true, "VND"));
            return vat.Object;
        }

        public RevalueForeignBalancesCommandHandler Revalue() =>
            new(Uow.Object, Provider.Object, Policy(), Posting.Object, NullLogger<RevalueForeignBalancesCommandHandler>.Instance);

        public GetRevaluationQueryHandler Preview() => new(Uow.Object, Provider.Object, Policy());
    }

    [Fact]
    public async Task Revaluing_posts_the_difference_on_the_last_day_and_reverses_it_the_next_day()
    {
        var h = new Harness(new[] { UsdInvoice() }, new[] { ApprovedUsdBill() });

        var dto = await h.Revalue().Handle(new RevalueForeignBalancesCommand { FiscalPeriodId = h.Period.Id }, default);

        var request = h.Posted.Single();
        request.Date.Should().Be(End);
        var lines = ByKey(request);
        lines[PostingKeys.AccountsReceivable].Should().Be((500_000m, 0m));
        lines[PostingKeys.AccountsPayable].Should().Be((0m, 250_000m));
        lines[PostingKeys.FxGain].Should().Be((0m, 250_000m));
        h.Posting.Verify(p => p.ReverseEntryAsync(It.IsAny<JournalEntry>(), It.IsAny<string>(), End.AddDays(1), It.IsAny<CancellationToken>()), Times.Once);
        h.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        h.Uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        (dto.Posted, dto.EntryNumber, dto.ReversalEntryNumber, dto.NetDifference).Should().Be((true, "JE-REVAL", "JE-REV", 250_000m));
    }

    [Fact]
    public async Task Typed_rates_win_over_the_master_data()
    {
        var h = new Harness(new[] { UsdInvoice() }, Array.Empty<Bill>());

        var dto = await h.Revalue().Handle(new RevalueForeignBalancesCommand { FiscalPeriodId = h.Period.Id, Rates = new() { ["usd"] = 23_000m } }, default);

        ByKey(h.Posted.Single())[PostingKeys.FxLoss].Should().Be((1_000_000m, 0m));
        dto.Rates["USD"].Should().Be(23_000m);
        h.Provider.Verify(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Without_any_closing_rate_nothing_is_posted()
    {
        var h = new Harness(new[] { UsdInvoice() }, Array.Empty<Bill>(), masterRate: null);

        var act = () => h.Revalue().Handle(new RevalueForeignBalancesCommand { FiscalPeriodId = h.Period.Id }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*closing rate for USD*");
        h.Posted.Should().BeEmpty();
    }

    [Fact]
    public async Task A_period_is_revalued_once_and_never_when_closed()
    {
        var h = new Harness(new[] { UsdInvoice() }, Array.Empty<Bill>());
        h.Existing = JournalEntry.Create("JE-OLD", h.Period.Id, "old");

        var again = () => h.Revalue().Handle(new RevalueForeignBalancesCommand { FiscalPeriodId = h.Period.Id }, default);
        await again.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already revalued*JE-OLD*");

        h.Existing = null;
        h.Period.Status = (int)FiscalPeriodStatus.Closed;
        var closed = () => h.Revalue().Handle(new RevalueForeignBalancesCommand { FiscalPeriodId = h.Period.Id }, default);
        await closed.Should().ThrowAsync<BusinessRuleException>().WithMessage("*closed*");
    }

    [Fact]
    public async Task When_the_rate_has_not_moved_nothing_is_posted()
    {
        var h = new Harness(new[] { UsdInvoice() }, Array.Empty<Bill>(), masterRate: 24_000m);

        var dto = await h.Revalue().Handle(new RevalueForeignBalancesCommand { FiscalPeriodId = h.Period.Id }, default);

        h.Posted.Should().BeEmpty();
        dto.Posted.Should().BeFalse();
        dto.Notes.Should().Contain(n => n.Contains("Nothing to post"));
    }

    [Fact]
    public async Task The_preview_posts_nothing_and_reports_an_existing_revaluation()
    {
        var h = new Harness(new[] { UsdInvoice() }, Array.Empty<Bill>());

        var before = await h.Preview().Handle(new GetRevaluationQuery { FiscalPeriodId = h.Period.Id }, default);
        h.Existing = JournalEntry.Create("JE-OLD", h.Period.Id, "old");
        var after = await h.Preview().Handle(new GetRevaluationQuery { FiscalPeriodId = h.Period.Id }, default);

        h.Posted.Should().BeEmpty();
        (before.Posted, before.NetDifference).Should().Be((false, 500_000m));
        (after.Posted, after.EntryNumber).Should().Be((true, "JE-OLD"));
    }
}
