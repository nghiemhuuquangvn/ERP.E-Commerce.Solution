using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.EventHandlers;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-11c: what the customer paid for delivery is revenue on its own key, the entry still balances and an old chart still posts.</summary>
public class ShippingRevenueTests
{
    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey!).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    private static Invoice Invoiced(decimal gross, decimal rate, decimal tax, decimal shippingNet)
    {
        var invoice = Invoice.Create("INV-S", Guid.NewGuid(), Guid.NewGuid(), gross, DateTime.UtcNow.AddDays(30));
        invoice.ApplyVat(gross - tax, rate, tax);
        if (shippingNet > 0m) invoice.SetShippingNet(shippingNet);
        return invoice;
    }

    [Fact]
    public void Delivery_is_credited_on_its_own_key_and_the_entry_balances()
    {
        // goods 900 000 + delivery 100 000 + VAT 100 000 = 1 100 000
        var request = PostingRules.ForInvoice(Invoiced(1_100_000m, 10m, 100_000m, shippingNet: 100_000m));
        var lines = ByKey(request);

        lines[PostingKeys.AccountsReceivable].Should().Be((1_100_000m, 0m));
        lines[PostingKeys.SalesRevenue].Should().Be((0m, 900_000m));
        lines[PostingKeys.ShippingRevenue].Should().Be((0m, 100_000m));
        lines[PostingKeys.VatOutput].Should().Be((0m, 100_000m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void An_invoice_without_delivery_posts_exactly_as_before()
    {
        var request = PostingRules.ForInvoice(Invoiced(1_100_000m, 10m, 100_000m, shippingNet: 0m));

        request.Lines.Should().HaveCount(3);
        request.Lines.Should().NotContain(l => l.PostingKey == PostingKeys.ShippingRevenue);
    }

    [Fact]
    public void The_delivery_part_can_neither_be_negative_nor_exceed_the_net()
    {
        var invoice = Invoiced(1_100_000m, 10m, 100_000m, 0m);

        ((Action)(() => invoice.SetShippingNet(-1m))).Should().Throw<ArgumentException>();
        ((Action)(() => invoice.SetShippingNet(1_000_000.01m))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void An_invoice_that_is_all_delivery_leaves_no_negative_goods_line()
    {
        var request = PostingRules.ForInvoice(Invoiced(110m, 10m, 10m, shippingNet: 100m));

        request.Lines.Where(l => l.PostingKey == PostingKeys.SalesRevenue).Sum(l => l.Credit).Should().Be(0m);
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    // ---- the handler ----

    private (CreateInvoiceCommandHandler Handler, List<Invoice> Stored, List<PostingRequest> Posted) Handler(decimal rate, bool inclusive)
    {
        var stored = new List<Invoice>();
        var posted = new List<PostingRequest>();
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Callback<Invoice, CancellationToken>((i, _) => stored.Add(i)).Returns(Task.CompletedTask);
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => posted.Add(r))
            .ReturnsAsync(() => JournalEntry.Create("JE-1", Guid.NewGuid(), "x"));
        var vat = new Mock<IVatPolicy>();
        vat.Setup(v => v.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VatSettings(rate, inclusive));
        return (new CreateInvoiceCommandHandler(uow.Object, posting.Object, NullLogger<CreateInvoiceCommandHandler>.Instance, vat.Object), stored, posted);
    }

    private static CreateInvoiceCommand Command(decimal amount, decimal shipping) =>
        new() { InvoiceNumber = "INV-H", CustomerId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = amount, ShippingAmount = shipping, DueDate = DateTime.UtcNow.AddDays(30) };

    [Fact]
    public async Task With_prices_including_VAT_the_delivery_share_of_the_net_follows_its_share_of_the_amount()
    {
        var (handler, _, posted) = Handler(10m, inclusive: true);

        var dto = await handler.Handle(Command(1_100_000m, 110_000m), default);       // delivery is a tenth of the order

        (dto.NetAmount, dto.ShippingNetAmount, dto.TaxAmount).Should().Be((1_000_000m, 100_000m, 100_000m));
        ByKey(posted.Single())[PostingKeys.ShippingRevenue].Should().Be((0m, 100_000m));
    }

    [Fact]
    public async Task With_prices_excluding_VAT_the_delivery_net_is_its_own_amount()
    {
        var (handler, _, posted) = Handler(10m, inclusive: false);

        var dto = await handler.Handle(Command(1_000_000m, 100_000m), default);

        (dto.Amount, dto.NetAmount, dto.ShippingNetAmount).Should().Be((1_100_000m, 1_000_000m, 100_000m));
        var request = posted.Single();
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Theory]
    [InlineData(333_333.33, 29_999.99, 8)]
    [InlineData(1_000.01, 30_000, 10)]      // delivery larger than the order cannot happen; it is capped, not negative
    [InlineData(99.99, 0.01, 5)]
    public async Task The_entry_balances_for_awkward_amounts(double amount, double shipping, int rate)
    {
        var (handler, _, posted) = Handler(rate, inclusive: true);

        await handler.Handle(Command((decimal)amount, (decimal)shipping), default);

        var request = posted.Single();
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
        request.Lines.Should().OnlyContain(l => l.Debit >= 0m && l.Credit >= 0m);
    }

    [Fact]
    public async Task An_invoice_without_a_delivery_fee_is_unchanged()
    {
        var (handler, _, posted) = Handler(10m, inclusive: true);

        var dto = await handler.Handle(Command(1_100_000m, 0m), default);

        dto.ShippingNetAmount.Should().Be(0m);
        posted.Single().Lines.Should().NotContain(l => l.PostingKey == PostingKeys.ShippingRevenue);
    }

    // ---- the event ----

    [Fact]
    public async Task The_fee_of_the_order_event_reaches_the_invoice_command()
    {
        CreateInvoiceCommand? sent = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<InvoiceDto>, CancellationToken>((c, _) => sent = (CreateInvoiceCommand)c)
            .ReturnsAsync(new InvoiceDto());
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.GetByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Invoice?)null);

        await new SalesOrderCreatedIntegrationEventHandler(mediator.Object, invoices.Object, NullLogger<SalesOrderCreatedIntegrationEventHandler>.Instance)
            .Handle(new SalesOrderCreatedIntegrationEvent { OrderId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TotalAmount = 130_000m, ShippingFee = 30_000m }, default);

        (sent!.Amount, sent.ShippingAmount).Should().Be((130_000m, 30_000m));
    }

    [Fact]
    public void An_order_event_from_before_the_fee_existed_reads_as_no_delivery()
    {
        var created = System.Text.Json.JsonSerializer.Deserialize<SalesOrderCreatedIntegrationEvent>("{\"OrderId\":\"" + Guid.NewGuid() + "\",\"CustomerId\":\"" + Guid.NewGuid() + "\",\"TotalAmount\":500}")!;

        created.ShippingFee.Should().Be(0m);
    }
}
