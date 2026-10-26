using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Fx;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Entities.Settings;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-4: documents in a foreign currency, converted at the rate typed on them, and the realized exchange difference when they are settled.</summary>
public class ForeignCurrencyTests
{
    private readonly List<Invoice> _invoices = new();
    private readonly List<Bill> _bills = new();
    private readonly List<PaymentApplication> _applications = new();
    private readonly List<PostingRequest> _posted = new();

    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey!).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    private static void Balanced(PostingRequest r) => r.Lines.Sum(l => l.Debit).Should().Be(r.Lines.Sum(l => l.Credit));

    private (CreateInvoiceCommandHandler CreateInvoice, MarkInvoicePaidCommandHandler PayInvoice, CreateBillCommandHandler CreateBill, MarkBillPaidCommandHandler PayBill, ApproveBillCommandHandler ApproveBill) Handlers(bool inclusive = true, decimal rate = 10m)
    {
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Callback<Invoice, CancellationToken>((i, _) => _invoices.Add(i)).Returns(Task.CompletedTask);
        invoices.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _invoices.FirstOrDefault(i => i.Id == id));
        var bills = new Mock<IBillRepository>();
        bills.Setup(r => r.AddAsync(It.IsAny<Bill>(), It.IsAny<CancellationToken>())).Callback<Bill, CancellationToken>((b, _) => _bills.Add(b)).Returns(Task.CompletedTask);
        bills.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _bills.FirstOrDefault(b => b.Id == id));
        var applications = new Mock<IPaymentApplicationRepository>();
        applications.Setup(r => r.AddAsync(It.IsAny<PaymentApplication>(), It.IsAny<CancellationToken>())).Callback<PaymentApplication, CancellationToken>((a, _) => _applications.Add(a)).Returns(Task.CompletedTask);
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        uow.SetupGet(u => u.Bills).Returns(bills.Object);
        uow.SetupGet(u => u.PaymentApplications).Returns(applications.Object);
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r)).ReturnsAsync(() => JournalEntry.Create("JE-1", Guid.NewGuid(), "x"));
        var vat = new Mock<IVatPolicy>();
        vat.Setup(v => v.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VatSettings(rate, inclusive, "VND"));
        return (new CreateInvoiceCommandHandler(uow.Object, posting.Object, NullLogger<CreateInvoiceCommandHandler>.Instance, vat.Object, null),
                new MarkInvoicePaidCommandHandler(uow.Object, posting.Object, NullLogger<MarkInvoicePaidCommandHandler>.Instance),
                new CreateBillCommandHandler(uow.Object, vat.Object, null),
                new MarkBillPaidCommandHandler(uow.Object, posting.Object, NullLogger<MarkBillPaidCommandHandler>.Instance),
                new ApproveBillCommandHandler(uow.Object, posting.Object, NullLogger<ApproveBillCommandHandler>.Instance, null));
    }

    private static CreateInvoiceCommand UsdInvoice(decimal usd = 1_000m, decimal? rate = 24_000m, string code = "USD") =>
        new() { InvoiceNumber = "INV-USD", CustomerId = Guid.NewGuid(), OrderId = Guid.NewGuid(), DueDate = DateTime.UtcNow.AddDays(30), CurrencyCode = code, ForeignAmount = usd, ExchangeRate = rate };

    private static CreateBillCommand UsdBill(decimal usd = 1_000m, decimal? rate = 24_000m) =>
        new() { BillNumber = "BILL-USD", SupplierId = Guid.NewGuid(), POId = Guid.NewGuid(), DueDate = DateTime.UtcNow.AddDays(30), CurrencyCode = "USD", ForeignAmount = usd, ExchangeRate = rate };

    // ---- creation ----

    [Fact]
    public async Task A_foreign_invoice_is_booked_in_the_base_currency_at_its_rate_and_keeps_the_foreign_gross()
    {
        var (create, _, _, _, _) = Handlers(rate: 10m);

        var dto = await create.Handle(UsdInvoice(), default);

        (dto.Amount, dto.ForeignAmount, dto.ExchangeRate, dto.ForeignCurrencyCode).Should().Be((24_000_000m, 1_000m, 24_000m, "USD"));
        dto.TaxAmount.Should().Be(2_181_818.18m);                 // VAT is split on the base amount
        var lines = ByKey(_posted.Single());
        lines[PostingKeys.AccountsReceivable].Should().Be((24_000_000m, 0m));
        lines[PostingKeys.VatOutput].Should().Be((0m, 2_181_818.18m));
    }

    [Fact]
    public async Task With_prices_excluding_tax_the_foreign_gross_includes_the_VAT()
    {
        var (create, _, _, _, _) = Handlers(inclusive: false, rate: 10m);

        var dto = await create.Handle(UsdInvoice(), default);

        (dto.Amount, dto.ForeignAmount).Should().Be((26_400_000m, 1_100m));
    }

    [Fact]
    public async Task The_base_currency_code_is_not_a_foreign_document()
    {
        var (create, _, _, _, _) = Handlers();

        var dto = await create.Handle(new CreateInvoiceCommand { InvoiceNumber = "INV-1", CustomerId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 1_000_000m, DueDate = DateTime.UtcNow, CurrencyCode = "vnd" }, default);

        (dto.ForeignCurrencyCode, dto.ForeignAmount, dto.ExchangeRate).Should().Be((null, null, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_foreign_document_needs_a_positive_rate(int? rate)
    {
        var (create, _, createBill, _, _) = Handlers();

        var invoice = () => create.Handle(UsdInvoice(rate: rate), default);
        var bill = () => createBill.Handle(UsdBill(rate: rate), default);

        await invoice.Should().ThrowAsync<BusinessRuleException>().WithMessage("*rate*");
        await bill.Should().ThrowAsync<BusinessRuleException>().WithMessage("*rate*");
    }

    [Fact]
    public async Task A_foreign_document_needs_its_foreign_amount_and_takes_no_VAT_lines_yet()
    {
        var (create, _, _, _, _) = Handlers();

        var noAmount = () => create.Handle(UsdInvoice(usd: 0m), default);
        var withLines = UsdInvoice();
        withLines.Lines.Add(new VatLineInput { Category = 0, RatePercent = 10m, Amount = 100m });
        var lines = () => create.Handle(withLines, default);

        await noAmount.Should().ThrowAsync<BusinessRuleException>().WithMessage("*foreign currency is required*");
        await lines.Should().ThrowAsync<BusinessRuleException>().WithMessage("*VAT lines*");
    }

    // ---- realized difference: invoice ----

    [Theory]
    [InlineData(24_500, 24_500_000, 500_000, 0)]      // the dollar rose: gain
    [InlineData(23_500, 23_500_000, 0, 500_000)]      // the dollar fell: loss
    [InlineData(24_000, 24_000_000, 0, 0)]            // same rate: none
    public async Task Receiving_a_foreign_invoice_at_another_rate_posts_the_realized_difference(int paymentRate, int cash, int gain, int loss)
    {
        var (create, pay, _, _, _) = Handlers(rate: 0m);
        var dto = await create.Handle(UsdInvoice(), default);
        _posted.Clear();

        var paid = await pay.Handle(new MarkInvoicePaidCommand { InvoiceId = dto.Id, PaymentRate = paymentRate }, default);

        paid.Status.Should().Be("Paid");
        var entry = _posted.Single();
        Balanced(entry);
        var lines = ByKey(entry);
        lines[PostingKeys.Bank].Should().Be((cash, 0m));
        lines[PostingKeys.AccountsReceivable].Should().Be((0m, 24_000_000m));
        lines.ContainsKey(PostingKeys.FxGain).Should().Be(gain > 0);
        lines.ContainsKey(PostingKeys.FxLoss).Should().Be(loss > 0);
        if (gain > 0) lines[PostingKeys.FxGain].Should().Be((0m, gain));
        if (loss > 0) lines[PostingKeys.FxLoss].Should().Be((loss, 0m));

        var application = _applications.Single();
        (application.Amount, application.ForeignAmount, application.ExchangeRate, application.FxDifference).Should().Be((24_000_000m, 1_000m, (decimal)paymentRate, (decimal)(gain - loss)));
    }

    [Fact]
    public async Task A_foreign_invoice_cannot_be_marked_paid_without_the_payment_rate()
    {
        var (create, pay, _, _, _) = Handlers(rate: 0m);
        var dto = await create.Handle(UsdInvoice(), default);

        var act = () => pay.Handle(new MarkInvoicePaidCommand { InvoiceId = dto.Id }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*rate*");
    }

    [Fact]
    public async Task A_base_currency_invoice_takes_no_payment_rate_and_posts_no_difference()
    {
        var (create, pay, _, _, _) = Handlers(rate: 0m);
        var dto = await create.Handle(new CreateInvoiceCommand { InvoiceNumber = "INV-1", CustomerId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 1_000_000m, DueDate = DateTime.UtcNow }, default);
        var withRate = () => pay.Handle(new MarkInvoicePaidCommand { InvoiceId = dto.Id, PaymentRate = 24_000m }, default);
        await withRate.Should().ThrowAsync<BusinessRuleException>().WithMessage("*base currency*");
        _posted.Clear();

        await pay.Handle(new MarkInvoicePaidCommand { InvoiceId = dto.Id }, default);

        var lines = ByKey(_posted.Single());
        lines.Keys.Should().BeEquivalentTo(PostingKeys.Bank, PostingKeys.AccountsReceivable);
        _applications.Single().FxDifference.Should().BeNull();
    }

    // ---- realized difference: bill ----

    [Theory]
    [InlineData(24_500, 500_000, 0)]      // paying more than booked: loss
    [InlineData(23_500, 0, 500_000)]      // paying less: gain
    public async Task Paying_a_foreign_bill_at_another_rate_posts_the_realized_difference(int paymentRate, int loss, int gain)
    {
        var (_, _, createBill, payBill, approve) = Handlers(rate: 0m);
        var dto = await createBill.Handle(UsdBill(), default);
        await approve.Handle(new ApproveBillCommand { BillId = dto.Id }, default);
        _posted.Clear();

        await payBill.Handle(new MarkBillPaidCommand { BillId = dto.Id, PaymentRate = paymentRate }, default);

        var entry = _posted.Single();
        Balanced(entry);
        var lines = ByKey(entry);
        lines[PostingKeys.AccountsPayable].Should().Be((24_000_000m, 0m));
        lines[PostingKeys.Bank].Should().Be((0m, 1_000m * paymentRate));
        if (gain > 0) lines[PostingKeys.FxGain].Should().Be((0m, gain));
        if (loss > 0) lines[PostingKeys.FxLoss].Should().Be((loss, 0m));
        _applications.Single().FxDifference.Should().Be(gain - loss);
    }

    [Fact]
    public async Task A_foreign_bill_cannot_be_marked_paid_without_the_payment_rate()
    {
        var (_, _, createBill, payBill, _) = Handlers(rate: 0m);
        var dto = await createBill.Handle(UsdBill(), default);

        var act = () => payBill.Handle(new MarkBillPaidCommand { BillId = dto.Id }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*rate*");
    }

    // ---- posting keys and settings ----

    [Fact]
    public void The_exchange_difference_has_default_accounts_on_both_charts()
    {
        PostingKeys.All.Should().Contain(new[] { PostingKeys.FxGain, PostingKeys.FxLoss });
        PostingKeys.DefaultAccountNumbers.Should().ContainKeys(PostingKeys.FxGain, PostingKeys.FxLoss);
        var vas = ChartTemplates.All.Single(t => t.Id == ChartTemplates.Vas99);
        (vas.PostingMap[PostingKeys.FxGain], vas.PostingMap[PostingKeys.FxLoss]).Should().Be(("515", "635"));
    }

    [Fact]
    public void The_base_currency_is_VND_until_the_tenant_changes_it()
    {
        var settings = TenantTaxSettings.CreateDefault(Guid.NewGuid());
        settings.BaseCurrencyCode.Should().Be("VND");

        settings.Update(null, 10m, true, baseCurrencyCode: " usd ");

        settings.BaseCurrencyCode.Should().Be("USD");
    }

    [Fact]
    public async Task The_rate_suggestion_asks_the_master_data_for_the_currency_in_the_base_currency()
    {
        var provider = new Mock<IExchangeRateProvider>();
        provider.Setup(p => p.GetRateAsync("USD", "VND", new DateTime(2026, 10, 26), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRateQuote("USD", "VND", 24_500m, new DateTime(2026, 10, 25), false));
        var vat = new Mock<IVatPolicy>();
        vat.Setup(v => v.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VatSettings(0m, true, "VND"));
        var handler = new GetExchangeRateQueryHandler(provider.Object, vat.Object);

        var quote = await handler.Handle(new GetExchangeRateQuery { Currency = "usd", Date = new DateTime(2026, 10, 26, 15, 0, 0) }, default);

        quote!.Rate.Should().Be(24_500m);
        var bad = () => handler.Handle(new GetExchangeRateQuery { Currency = "US" }, default);
        await bad.Should().ThrowAsync<BusinessRuleException>();
    }
}
