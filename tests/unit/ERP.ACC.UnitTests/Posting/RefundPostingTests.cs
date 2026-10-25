using ERP.ACC.Application.EventHandlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.ACC.Infrastructure.BackgroundJobs;
using ERP.ACC.Infrastructure.EventBus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-1e: confirmed refunds and completed returns reach the ledger.</summary>
public class RefundPostingTests
{
    private static readonly DateTime Day = new(2026, 10, 24);

    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.ToDictionary(l => l.PostingKey!, l => (l.Debit, l.Credit));

    [Fact]
    public void A_refund_is_the_reverse_of_the_payment_entry_cash_or_bank()
    {
        var bank = PostingRules.ForPaymentRefund(Guid.NewGuid(), Guid.NewGuid(), 400m, 2, Day);
        var cash = PostingRules.ForPaymentRefund(Guid.NewGuid(), Guid.NewGuid(), 400m, 4, Day);

        bank.SourceType.Should().Be("PaymentRefund");
        ByKey(bank)[PostingKeys.AccountsReceivable].Should().Be((400m, 0m));
        ByKey(bank)[PostingKeys.Bank].Should().Be((0m, 400m));
        ByKey(cash)[PostingKeys.Cash].Should().Be((0m, 400m));
    }

    [Fact]
    public void A_return_debits_sales_returns_and_credits_receivables()
    {
        var request = PostingRules.ForSalesReturn(Guid.NewGuid(), "INV-1", 1000m, Day);

        request.SourceType.Should().Be("SalesReturn");
        ByKey(request)[PostingKeys.SalesReturns].Should().Be((1000m, 0m));
        ByKey(request)[PostingKeys.AccountsReceivable].Should().Be((0m, 1000m));
    }

    [Fact]
    public void A_refund_lowers_the_paid_amount_and_reopens_a_paid_invoice_but_not_a_cancelled_one()
    {
        var invoice = Invoice.Create("INV-1", Guid.NewGuid(), Guid.NewGuid(), 1000m, Day.AddDays(30));
        invoice.ApplyPayment(1000m);
        invoice.Status.Should().Be((int)InvoiceStatus.Paid);

        invoice.ApplyRefund(400m);

        invoice.AmountPaid.Should().Be(600m);
        invoice.Status.Should().Be((int)InvoiceStatus.Unpaid);

        invoice.ApplyRefund(5000m);
        invoice.AmountPaid.Should().Be(0m);

        var cancelled = Invoice.Create("INV-2", Guid.NewGuid(), Guid.NewGuid(), 100m, Day.AddDays(30));
        cancelled.ApplyPayment(100m);
        cancelled.Cancel();
        cancelled.ApplyRefund(100m);
        cancelled.Status.Should().Be((int)InvoiceStatus.Cancelled);
        cancelled.AmountPaid.Should().Be(0m);
    }

    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IInvoiceRepository> _invoices = new();
    private readonly Mock<IPaymentApplicationRepository> _applications = new();
    private readonly Mock<IJournalEntryRepository> _entries = new();
    private readonly Mock<IJournalPostingService> _posting = new();
    private readonly List<PostingRequest> _posted = new();

    public RefundPostingTests()
    {
        _uow.SetupGet(u => u.Invoices).Returns(_invoices.Object);
        _uow.SetupGet(u => u.PaymentApplications).Returns(_applications.Object);
        _uow.SetupGet(u => u.JournalEntries).Returns(_entries.Object);
        _posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r)).ReturnsAsync((JournalEntry?)null);
    }

    private PaymentRefundedIntegrationEventHandler RefundHandler() =>
        new(_uow.Object, _posting.Object, NullLogger<PaymentRefundedIntegrationEventHandler>.Instance);

    private SalesOrderReturnedIntegrationEventHandler ReturnHandler() =>
        new(_uow.Object, _posting.Object, NullLogger<SalesOrderReturnedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task A_confirmed_refund_is_posted_and_applied_to_the_invoice()
    {
        var invoice = Invoice.Create("INV-1", Guid.NewGuid(), Guid.NewGuid(), 1000m, Day.AddDays(30));
        invoice.ApplyPayment(1000m);
        _invoices.Setup(i => i.GetByOrderIdAsync(invoice.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        var refundId = Guid.NewGuid();

        await RefundHandler().Handle(new PaymentRefundedIntegrationEvent
        {
            RefundId = refundId, PaymentId = Guid.NewGuid(), OrderId = invoice.OrderId, Amount = 400m, Method = 2, ProcessedAt = Day
        }, default);

        _posted.Should().ContainSingle().Which.SourceId.Should().Be(refundId);
        invoice.AmountPaid.Should().Be(600m);
        _applications.Verify(a => a.AddAsync(It.Is<PaymentApplication>(p => p.DocumentId == invoice.Id && p.Amount == -400m && p.PaidOn == Day.Date), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_refund_event_without_a_refund_id_and_a_redelivered_refund_post_nothing()
    {
        var handler = RefundHandler();
        var seen = Guid.NewGuid();
        _entries.Setup(e => e.GetBySourceKeyAsync(JournalEntry.BuildSourceKey("PaymentRefund", seen), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JournalEntry.CreatePosted("JE-1", Guid.NewGuid(), Day, "x", "PaymentRefund", seen,
                new[] { new JournalLineDraft(Guid.NewGuid(), 1m, 0m), new JournalLineDraft(Guid.NewGuid(), 0m, 1m) }));

        await handler.Handle(new PaymentRefundedIntegrationEvent { PaymentId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 400m }, default);
        await handler.Handle(new PaymentRefundedIntegrationEvent { RefundId = seen, PaymentId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 400m }, default);

        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task A_return_is_posted_against_the_invoice_but_not_above_its_amount_or_without_an_invoice()
    {
        var invoice = Invoice.Create("INV-1", Guid.NewGuid(), Guid.NewGuid(), 1000m, Day.AddDays(30));
        _invoices.Setup(i => i.GetByOrderIdAsync(invoice.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        var handler = ReturnHandler();

        await handler.Handle(new SalesOrderReturnedIntegrationEvent { OrderId = invoice.OrderId, RefundAmount = 1000m, ReturnedAt = Day }, default);
        await handler.Handle(new SalesOrderReturnedIntegrationEvent { OrderId = invoice.OrderId, RefundAmount = 1500m }, default);
        await handler.Handle(new SalesOrderReturnedIntegrationEvent { OrderId = Guid.NewGuid(), RefundAmount = 100m }, default);

        _posted.Should().ContainSingle().Which.SourceType.Should().Be("SalesReturn");
    }

    [Fact]
    public void The_consumers_read_the_refund_payloads()
    {
        var id = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var refunded = AccountingKafkaConsumer.ParsePaymentRefunded(JObject.Parse(
            "{\"$type\":\"ERP.PAY.Domain.Events.PaymentRefundedEvent, ERP.PAY.Domain\",\"PaymentId\":\"" + id + "\",\"OrderId\":\"" + id +
            "\",\"RefundAmount\":400.0,\"Reason\":\"x\",\"RefundId\":\"" + refundId + "\",\"Method\":4,\"ProcessedAt\":\"2026-10-24T08:30:00Z\"}"));
        refunded.RefundId.Should().Be(refundId);
        refunded.Amount.Should().Be(400m);
        refunded.Method.Should().Be(4);

        var legacy = AccountingKafkaConsumer.ParsePaymentRefunded(JObject.Parse(
            "{\"PaymentId\":\"" + id + "\",\"OrderId\":\"" + id + "\",\"RefundAmount\":400.0,\"Reason\":\"x\"}"));
        legacy.RefundId.Should().Be(Guid.Empty);

        var returned = SalesOrderRabbitMqConsumer.ParseReturned(
            "{\"OrderId\":\"" + id + "\",\"RefundAmount\":250.5,\"Reason\":\"wrong size\",\"ReturnedAt\":\"2026-10-24T08:30:00Z\"}")!;
        returned.OrderId.Should().Be(id);
        returned.RefundAmount.Should().Be(250.5m);
        SalesOrderRabbitMqConsumer.ParseReturned("{\"Reason\":\"x\"}").Should().BeNull();
    }
}
