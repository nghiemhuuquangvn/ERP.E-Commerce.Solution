using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Handlers;
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

/// <summary>ROADMAP P1-3: VAT on sales invoices, the adjustment of the rate and the VAT inside a return.</summary>
public class VatSalesTests
{
    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey!).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    // ---- calculator ----

    [Theory]
    [InlineData(1_100_000, 10, true, 1_000_000, 100_000, 1_100_000)]
    [InlineData(1_000_000, 10, false, 1_000_000, 100_000, 1_100_000)]
    [InlineData(1_080_000, 8, true, 1_000_000, 80_000, 1_080_000)]
    [InlineData(105, 5, true, 100, 5, 105)]
    [InlineData(1000, 0, true, 1000, 0, 1000)]
    [InlineData(1000, 0, false, 1000, 0, 1000)]
    public void The_split_follows_the_price_convention(double amount, double rate, bool inclusive, double net, double tax, double gross)
    {
        var split = VatCalculator.Split((decimal)amount, (decimal)rate, inclusive);

        split.Net.Should().Be((decimal)net);
        split.Tax.Should().Be((decimal)tax);
        split.Gross.Should().Be((decimal)gross);
    }

    [Fact]
    public void Rounding_is_half_away_from_zero_and_net_plus_tax_always_equals_the_gross()
    {
        var inclusive = VatCalculator.Split(100.05m, 10m, true);       // 100.05 × 10 ÷ 110 = 9.0954…
        var exclusive = VatCalculator.Split(33.33m, 10m, false);       // 3.333 -> 3.33

        inclusive.Tax.Should().Be(9.10m);
        (inclusive.Net + inclusive.Tax).Should().Be(inclusive.Gross);
        exclusive.Tax.Should().Be(3.33m);
        exclusive.Gross.Should().Be(36.66m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void A_rate_outside_0_to_100_is_refused(int rate)
    {
        var act = () => VatCalculator.Split(100m, rate, true);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---- invoice posting ----

    private static Invoice Invoiced(decimal gross, decimal rate, decimal tax)
    {
        var invoice = Invoice.Create("INV-V", Guid.NewGuid(), Guid.NewGuid(), gross, DateTime.UtcNow.AddDays(30));
        invoice.ApplyVat(gross - tax, rate, tax);
        return invoice;
    }

    [Fact]
    public void An_invoice_with_VAT_posts_receivable_gross_revenue_net_and_VAT_output()
    {
        var lines = ByKey(PostingRules.ForInvoice(Invoiced(1_100_000m, 10m, 100_000m)));

        lines[PostingKeys.AccountsReceivable].Should().Be((1_100_000m, 0m));
        lines[PostingKeys.SalesRevenue].Should().Be((0m, 1_000_000m));
        lines[PostingKeys.VatOutput].Should().Be((0m, 100_000m));
    }

    [Fact]
    public void An_invoice_without_VAT_posts_the_two_lines_as_before()
    {
        var request = PostingRules.ForInvoice(Invoice.Create("INV-0", Guid.NewGuid(), Guid.NewGuid(), 500m, DateTime.UtcNow.AddDays(30)));

        request.Lines.Should().HaveCount(2);
        ByKey(request)[PostingKeys.SalesRevenue].Should().Be((0m, 500m));
    }

    [Fact]
    public void The_split_must_add_up_to_the_invoice_amount()
    {
        var invoice = Invoice.Create("INV-X", Guid.NewGuid(), Guid.NewGuid(), 1100m, DateTime.UtcNow.AddDays(30));

        var act = () => invoice.ApplyVat(1000m, 10m, 50m);

        act.Should().Throw<ArgumentException>();
    }

    // ---- handler ----

    private readonly List<PostingRequest> _posted = new();
    private readonly List<Invoice> _stored = new();

    private (CreateInvoiceCommandHandler Create, AdjustInvoiceVatCommandHandler Adjust) Handlers(decimal rate, bool inclusive)
    {
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Callback<Invoice, CancellationToken>((i, _) => _stored.Add(i)).Returns(Task.CompletedTask);
        invoices.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _stored.FirstOrDefault(i => i.Id == id));
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r))
            .ReturnsAsync(() => JournalEntry.Create("JE-1", Guid.NewGuid(), "x"));
        var vat = new Mock<IVatPolicy>();
        vat.Setup(v => v.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VatSettings(rate, inclusive));
        return (new CreateInvoiceCommandHandler(uow.Object, posting.Object, NullLogger<CreateInvoiceCommandHandler>.Instance, vat.Object),
                new AdjustInvoiceVatCommandHandler(uow.Object, posting.Object, NullLogger<AdjustInvoiceVatCommandHandler>.Instance));
    }

    private static CreateInvoiceCommand Command(decimal amount, decimal? rate = null) =>
        new() { InvoiceNumber = "INV-H", CustomerId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = amount, DueDate = DateTime.UtcNow.AddDays(30), TaxRatePercent = rate };

    [Fact]
    public async Task The_invoice_takes_the_tenant_rate_when_prices_include_tax()
    {
        var (create, _) = Handlers(10m, true);

        var dto = await create.Handle(Command(1_100_000m), default);

        (dto.Amount, dto.NetAmount, dto.TaxAmount, dto.TaxRatePercent).Should().Be((1_100_000m, 1_000_000m, 100_000m, 10m));
        ByKey(_posted.Single())[PostingKeys.VatOutput].Should().Be((0m, 100_000m));
    }

    [Fact]
    public async Task When_prices_exclude_tax_the_VAT_is_added_on_top_of_the_order_total()
    {
        var (create, _) = Handlers(10m, false);

        var dto = await create.Handle(Command(1_000_000m), default);

        (dto.Amount, dto.NetAmount, dto.TaxAmount).Should().Be((1_100_000m, 1_000_000m, 100_000m));
    }

    [Fact]
    public async Task A_rate_on_the_command_overrides_the_tenant_default_and_a_bad_rate_is_refused()
    {
        var (create, _) = Handlers(10m, true);

        var dto = await create.Handle(Command(1_080_000m, 8m), default);
        var act = () => create.Handle(Command(100m, 150m), default);

        dto.TaxAmount.Should().Be(80_000m);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Without_a_policy_the_invoice_is_created_without_VAT()
    {
        var uow = new Mock<IAccountingUnitOfWork>();
        var invoices = new Mock<IInvoiceRepository>();
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        var posting = new Mock<IJournalPostingService>();
        var handler = new CreateInvoiceCommandHandler(uow.Object, posting.Object, NullLogger<CreateInvoiceCommandHandler>.Instance);

        var dto = await handler.Handle(Command(500m), default);

        (dto.Amount, dto.TaxAmount).Should().Be((500m, 0m));
    }

    [Fact]
    public async Task Adjusting_the_rate_keeps_the_gross_and_posts_only_the_difference()
    {
        var (create, adjust) = Handlers(10m, true);
        var created = await create.Handle(Command(1_100_000m), default);          // net 1,000,000 / tax 100,000
        _posted.Clear();

        var dto = await adjust.Handle(new AdjustInvoiceVatCommand { InvoiceId = created.Id, TaxRatePercent = 8m }, default);

        (dto.Amount, dto.NetAmount, dto.TaxAmount).Should().Be((1_100_000m, 1_018_518.52m, 81_481.48m));
        var lines = ByKey(_posted.Single());
        lines[PostingKeys.VatOutput].Should().Be((18_518.52m, 0m));               // VAT output goes down
        lines[PostingKeys.SalesRevenue].Should().Be((0m, 18_518.52m));
    }

    [Fact]
    public async Task Adjusting_to_the_same_rate_posts_nothing_and_a_cancelled_invoice_is_refused()
    {
        var (create, adjust) = Handlers(10m, true);
        var created = await create.Handle(Command(1_100_000m), default);
        _posted.Clear();

        await adjust.Handle(new AdjustInvoiceVatCommand { InvoiceId = created.Id, TaxRatePercent = 10m }, default);
        _posted.Should().BeEmpty();

        _stored.Single().Cancel();
        var act = () => adjust.Handle(new AdjustInvoiceVatCommand { InvoiceId = created.Id, TaxRatePercent = 5m }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // ---- returns ----

    [Fact]
    public void A_return_takes_the_VAT_inside_the_refund_off_the_VAT_output()
    {
        var lines = ByKey(PostingRules.ForSalesReturn(Guid.NewGuid(), "INV-V", 550_000m, DateTime.UtcNow, 10m));

        lines[PostingKeys.SalesReturns].Should().Be((500_000m, 0m));
        lines[PostingKeys.VatOutput].Should().Be((50_000m, 0m));
        lines[PostingKeys.AccountsReceivable].Should().Be((0m, 550_000m));
    }

    [Fact]
    public void A_return_on_an_invoice_without_VAT_is_the_two_line_entry_as_before()
    {
        PostingRules.ForSalesReturn(Guid.NewGuid(), "INV-0", 300m, DateTime.UtcNow).Lines.Should().HaveCount(2);
    }
}
