using ERP.ACC.Application.Commands;
using ERP.ACC.Application.EventHandlers;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ERP.ACC.UnitTests.Handlers;

/// <summary>ROADMAP P1-1: which business events write which journal entries.</summary>
public class AutoPostingFlowTests
{
    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IInvoiceRepository> _invoices = new();
    private readonly Mock<IBillRepository> _bills = new();
    private readonly Mock<IJournalEntryRepository> _entries = new();
    private readonly Mock<IJournalPostingService> _posting = new();
    private readonly List<PostingRequest> _posted = new();
    private readonly List<(string Type, Guid Id, string Reason)> _reversed = new();

    public AutoPostingFlowTests()
    {
        _uow.SetupGet(u => u.Invoices).Returns(_invoices.Object);
        _uow.SetupGet(u => u.Bills).Returns(_bills.Object);
        _uow.SetupGet(u => u.JournalEntries).Returns(_entries.Object);
        _posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r)).ReturnsAsync((JournalEntry?)null);
        _posting.Setup(p => p.ReverseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, string, DateTime, CancellationToken>((t, id, r, _, _) => _reversed.Add((t, id, r))).ReturnsAsync((JournalEntry?)null);
    }

    private static Invoice NewInvoice(decimal amount = 1000m, Guid? orderId = null) =>
        Invoice.Create("INV-1", Guid.NewGuid(), orderId ?? Guid.NewGuid(), amount, DateTime.UtcNow.AddDays(30));

    private static void Lines(PostingRequest r, string debitKey, string creditKey, decimal amount)
    {
        r.Lines.Should().HaveCount(2);
        r.Lines.Single(l => l.Debit > 0).PostingKey.Should().Be(debitKey);
        r.Lines.Single(l => l.Debit > 0).Debit.Should().Be(amount);
        r.Lines.Single(l => l.Credit > 0).PostingKey.Should().Be(creditKey);
        r.Lines.Single(l => l.Credit > 0).Credit.Should().Be(amount);
    }

    // ---- invoice ----

    [Fact]
    public async Task CreatingAnInvoice_PostsReceivableAgainstRevenue_InTheSameSave()
    {
        var handler = new CreateInvoiceCommandHandler(_uow.Object, _posting.Object, NullLogger<CreateInvoiceCommandHandler>.Instance);
        var order = Guid.NewGuid();

        var dto = await handler.Handle(new CreateInvoiceCommand
        {
            InvoiceNumber = "INV-9", CustomerId = Guid.NewGuid(), OrderId = order, Amount = 750m, DueDate = DateTime.UtcNow.AddDays(30)
        }, default);

        var request = _posted.Should().ContainSingle().Subject;
        request.SourceType.Should().Be("Invoice");
        request.SourceId.Should().Be(dto.Id);
        Lines(request, PostingKeys.AccountsReceivable, PostingKeys.SalesRevenue, 750m);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnIncompletePostingProfile_DoesNotStopTheInvoiceFromBeingSaved()
    {
        _posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PostingConfigurationException(PostingKeys.SalesRevenue, "no account"));
        var handler = new CreateInvoiceCommandHandler(_uow.Object, _posting.Object, NullLogger<CreateInvoiceCommandHandler>.Instance);

        var dto = await handler.Handle(new CreateInvoiceCommand
        {
            InvoiceNumber = "INV-9", CustomerId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 10m, DueDate = DateTime.UtcNow.AddDays(30)
        }, default);

        dto.Should().NotBeNull();
        _invoices.Verify(i => i.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARedeliveredSalesOrderCreated_DoesNotCreateASecondInvoice()
    {
        var mediator = new Mock<IMediator>();
        var order = Guid.NewGuid();
        _invoices.Setup(i => i.GetByOrderIdAsync(order, It.IsAny<CancellationToken>())).ReturnsAsync(NewInvoice(orderId: order));
        var handler = new SalesOrderCreatedIntegrationEventHandler(mediator.Object, _invoices.Object,
            NullLogger<SalesOrderCreatedIntegrationEventHandler>.Instance);

        await handler.Handle(new SalesOrderCreatedIntegrationEvent { OrderId = order, CustomerId = Guid.NewGuid(), TotalAmount = 5 }, default);
        mediator.Verify(m => m.Send(It.IsAny<CreateInvoiceCommand>(), It.IsAny<CancellationToken>()), Times.Never);

        var fresh = Guid.NewGuid();
        await handler.Handle(new SalesOrderCreatedIntegrationEvent { OrderId = fresh, CustomerId = Guid.NewGuid(), TotalAmount = 5 }, default);
        mediator.Verify(m => m.Send(It.Is<CreateInvoiceCommand>(c => c.OrderId == fresh), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- order cancelled ----

    private SalesOrderCancelledIntegrationEventHandler CancelHandler() =>
        new(_uow.Object, _posting.Object, NullLogger<SalesOrderCancelledIntegrationEventHandler>.Instance);

    [Fact]
    public async Task ACancelledOrder_CancelsItsUnpaidInvoice_AndReversesTheRevenue()
    {
        var invoice = NewInvoice();
        _invoices.Setup(i => i.GetByOrderIdAsync(invoice.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);

        await CancelHandler().Handle(new SalesOrderCancelledIntegrationEvent { OrderId = invoice.OrderId, Reason = "customer asked" }, default);

        invoice.Status.Should().Be((int)InvoiceStatus.Cancelled);
        _reversed.Should().ContainSingle().Which.Should().Match<(string Type, Guid Id, string Reason)>(
            r => r.Type == "Invoice" && r.Id == invoice.Id && r.Reason.Contains("customer asked"));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACancelledOrder_WithAPaidOrPartlyPaidInvoice_ChangesNothing()
    {
        var paid = NewInvoice(100m);
        paid.ApplyPayment(100m);
        var partly = NewInvoice(100m);
        partly.ApplyPayment(30m);
        _invoices.Setup(i => i.GetByOrderIdAsync(paid.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(paid);
        _invoices.Setup(i => i.GetByOrderIdAsync(partly.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(partly);

        await CancelHandler().Handle(new SalesOrderCancelledIntegrationEvent { OrderId = paid.OrderId }, default);
        await CancelHandler().Handle(new SalesOrderCancelledIntegrationEvent { OrderId = partly.OrderId }, default);

        paid.Status.Should().Be((int)InvoiceStatus.Paid);
        partly.Status.Should().Be((int)InvoiceStatus.Unpaid);
        _reversed.Should().BeEmpty();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ACancelledOrder_AlreadyHandledOrWithoutInvoice_IsIgnored()
    {
        var cancelled = NewInvoice();
        cancelled.Cancel();
        _invoices.Setup(i => i.GetByOrderIdAsync(cancelled.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(cancelled);

        await CancelHandler().Handle(new SalesOrderCancelledIntegrationEvent { OrderId = cancelled.OrderId }, default);
        await CancelHandler().Handle(new SalesOrderCancelledIntegrationEvent { OrderId = Guid.NewGuid() }, default);

        _reversed.Should().BeEmpty();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- payment ----

    private PaymentProcessedIntegrationEventHandler PaymentHandler() =>
        new(_uow.Object, _posting.Object, NullLogger<PaymentProcessedIntegrationEventHandler>.Instance);

    [Theory]
    [InlineData(4, PostingKeys.Cash)]          // Cash
    [InlineData(0, PostingKeys.Bank)]          // CreditCard
    [InlineData(2, PostingKeys.Bank)]          // BankTransfer
    [InlineData(3, PostingKeys.Bank)]          // EWallet
    public async Task APayment_IsBookedToCashOrBank_AgainstReceivables(int method, string debitKey)
    {
        var invoice = NewInvoice(400m);
        _invoices.Setup(i => i.GetByOrderIdAsync(invoice.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        var payment = Guid.NewGuid();

        await PaymentHandler().Handle(new PaymentProcessedIntegrationEvent
        {
            PaymentId = payment, OrderId = invoice.OrderId, Amount = 400m, Method = method, ProcessedAt = new DateTime(2026, 10, 20)
        }, default);

        var request = _posted.Should().ContainSingle().Subject;
        request.SourceType.Should().Be("Payment");
        request.SourceId.Should().Be(payment);
        request.Date.Should().Be(new DateTime(2026, 10, 20));
        Lines(request, debitKey, PostingKeys.AccountsReceivable, 400m);
        invoice.Status.Should().Be((int)InvoiceStatus.Paid);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APartialPayment_LeavesTheInvoiceOpen_ButIsStillBooked()
    {
        var invoice = NewInvoice(400m);
        _invoices.Setup(i => i.GetByOrderIdAsync(invoice.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);

        await PaymentHandler().Handle(new PaymentProcessedIntegrationEvent { PaymentId = Guid.NewGuid(), OrderId = invoice.OrderId, Amount = 150m }, default);

        invoice.Status.Should().Be((int)InvoiceStatus.Unpaid);
        invoice.AmountPaid.Should().Be(150m);
        _posted.Should().ContainSingle();
    }

    [Fact]
    public async Task ARedeliveredPayment_IsNeitherBookedNorAppliedTwice()
    {
        var invoice = NewInvoice(400m);
        _invoices.Setup(i => i.GetByOrderIdAsync(invoice.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        var payment = Guid.NewGuid();
        _entries.Setup(e => e.GetBySourceKeyAsync(JournalEntry.BuildSourceKey("Payment", payment), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JournalEntry.Create("JE-1", Guid.NewGuid(), "x"));

        await PaymentHandler().Handle(new PaymentProcessedIntegrationEvent { PaymentId = payment, OrderId = invoice.OrderId, Amount = 150m }, default);

        invoice.AmountPaid.Should().Be(0);
        _posted.Should().BeEmpty();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task APaymentWithoutAnInvoice_IsStillBooked_AndZeroAmountsAreIgnored()
    {
        await PaymentHandler().Handle(new PaymentProcessedIntegrationEvent { PaymentId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 90m }, default);
        await PaymentHandler().Handle(new PaymentProcessedIntegrationEvent { PaymentId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 0m }, default);

        _posted.Should().ContainSingle().Which.Lines.Max(l => l.Debit).Should().Be(90m);
    }

    [Fact]
    public void ThePaymentMethodOnTheWire_IsReadAsANumberOrAName()
    {
        ERP.ACC.Infrastructure.BackgroundJobs.AccountingKafkaConsumer.ParsePaymentMethod(JToken.FromObject(4)).Should().Be(4);
        ERP.ACC.Infrastructure.BackgroundJobs.AccountingKafkaConsumer.ParsePaymentMethod(JToken.FromObject("Cash")).Should().Be(4);
        ERP.ACC.Infrastructure.BackgroundJobs.AccountingKafkaConsumer.ParsePaymentMethod(JToken.FromObject("EWallet")).Should().Be(3);
        ERP.ACC.Infrastructure.BackgroundJobs.AccountingKafkaConsumer.ParsePaymentMethod(null).Should().Be(0);
    }

    // ---- manual invoice payment, bills ----

    [Fact]
    public async Task MarkingAnInvoicePaidByHand_BooksTheOutstandingAmountToTheBank()
    {
        var invoice = NewInvoice(500m);
        invoice.ApplyPayment(200m);
        _invoices.Setup(i => i.GetByIdAsync(invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        var handler = new MarkInvoicePaidCommandHandler(_uow.Object, _posting.Object, NullLogger<MarkInvoicePaidCommandHandler>.Instance);

        await handler.Handle(new MarkInvoicePaidCommand { InvoiceId = invoice.Id }, default);

        invoice.Status.Should().Be((int)InvoiceStatus.Paid);
        invoice.AmountPaid.Should().Be(500m);
        var request = _posted.Should().ContainSingle().Subject;
        request.SourceType.Should().Be("InvoicePayment");
        Lines(request, PostingKeys.Bank, PostingKeys.AccountsReceivable, 300m);
    }

    [Fact]
    public async Task ApprovingAndPayingABill_PostsInventoryAgainstPayables_ThenPayablesAgainstBank()
    {
        var bill = Bill.Create("BILL-1", Guid.NewGuid(), Guid.NewGuid(), 2000m, DateTime.UtcNow.AddDays(30));
        _bills.Setup(b => b.GetByIdAsync(bill.Id, It.IsAny<CancellationToken>())).ReturnsAsync(bill);

        await new ApproveBillCommandHandler(_uow.Object, _posting.Object, NullLogger<ApproveBillCommandHandler>.Instance)
            .Handle(new ApproveBillCommand { BillId = bill.Id }, default);
        await new MarkBillPaidCommandHandler(_uow.Object, _posting.Object, NullLogger<MarkBillPaidCommandHandler>.Instance)
            .Handle(new MarkBillPaidCommand { BillId = bill.Id }, default);

        _posted.Should().HaveCount(2);
        _posted[0].SourceType.Should().Be("Bill");
        Lines(_posted[0], PostingKeys.Inventory, PostingKeys.AccountsPayable, 2000m);
        _posted[1].SourceType.Should().Be("BillPayment");
        Lines(_posted[1], PostingKeys.AccountsPayable, PostingKeys.Bank, 2000m);
        _posted.Select(p => p.SourceId).Should().AllBeEquivalentTo(bill.Id);
    }
}
