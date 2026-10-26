using ERP.ACC.Application.EventHandlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.BackgroundJobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-8: the bill comes from an approved supplier invoice; price variance, supplier credits and goods sent back reach the books.</summary>
public class SupplierInvoiceBillTests
{
    private static readonly DateTime Today = new(2026, 10, 27);

    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey!).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    private static Bill FromInvoice(decimal net, decimal tax, decimal received)
    {
        var bill = Bill.Create("PO-1/INV-1", Guid.NewGuid(), Guid.NewGuid(), net + tax, Today.AddDays(30));
        bill.ApplyVat(net, net == 0 ? 0 : Math.Round(tax / net * 100, 2), tax);
        bill.ReceivedValue = received;
        return bill;
    }

    // ─── bill posting ─────────────────────────────────────────────────────

    [Fact]
    public void A_bill_at_the_order_price_clears_the_receipt_accrual_with_no_variance()
    {
        var request = PostingRules.ForBillApproval(FromInvoice(1000m, 100m, 1000m), Today);

        var lines = ByKey(request);
        lines[PostingKeys.GoodsReceivedNotInvoiced].Should().Be((1000m, 0m));
        lines[PostingKeys.VatInput].Should().Be((100m, 0m));
        lines[PostingKeys.AccountsPayable].Should().Be((0m, 1100m));
        lines.Should().NotContainKey(PostingKeys.PurchasePriceVariance);
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void A_supplier_who_charged_more_than_the_order_price_books_the_difference_as_a_variance_debit()
    {
        var request = PostingRules.ForBillApproval(FromInvoice(1050m, 105m, 1000m), Today);

        var lines = ByKey(request);
        lines[PostingKeys.GoodsReceivedNotInvoiced].Should().Be((1000m, 0m));
        lines[PostingKeys.PurchasePriceVariance].Should().Be((50m, 0m));
        lines[PostingKeys.AccountsPayable].Should().Be((0m, 1155m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void A_supplier_who_charged_less_books_a_variance_credit()
    {
        var request = PostingRules.ForBillApproval(FromInvoice(980m, 0m, 1000m), Today);

        var lines = ByKey(request);
        lines[PostingKeys.PurchasePriceVariance].Should().Be((0m, 20m));
        lines[PostingKeys.AccountsPayable].Should().Be((0m, 980m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void A_bill_entered_by_hand_still_clears_the_accrual_by_its_whole_net()
    {
        var bill = Bill.Create("B-1", Guid.NewGuid(), Guid.NewGuid(), 1100m, Today);
        bill.ApplyVat(1000m, 10m, 100m);

        var lines = ByKey(PostingRules.ForBillApproval(bill, Today));

        lines[PostingKeys.GoodsReceivedNotInvoiced].Should().Be((1000m, 0m));
        lines.Should().NotContainKey(PostingKeys.PurchasePriceVariance);
    }

    // ─── credits ──────────────────────────────────────────────────────────

    [Fact]
    public void A_credit_comes_off_the_open_balance_never_below_zero_and_payment_clears_only_what_is_left()
    {
        var bill = FromInvoice(1000m, 100m, 1000m);

        bill.ApplyCredit(300m).Should().Be(300m);
        bill.Outstanding.Should().Be(800m);
        bill.ApplyCredit(5000m).Should().Be(800m);   // only what is still owed
        bill.Outstanding.Should().Be(0m);
        bill.ApplyCredit(10m).Should().Be(0m);

        var open = FromInvoice(1000m, 100m, 1000m);
        open.ApplyCredit(200m);
        var payment = PostingRules.ForBillPayment(open, Today);
        ByKey(payment)[PostingKeys.AccountsPayable].Should().Be((900m, 0m));
        ByKey(payment)[PostingKeys.Bank].Should().Be((0m, 900m));
    }

    [Fact]
    public void A_debit_note_debits_payables_and_credits_the_receipt_accrual()
    {
        var request = PostingRules.ForDebitNote(Guid.NewGuid(), "VR-1", "CN-1", 200m, Today)!;

        request.SourceType.Should().Be("DebitNote");
        var lines = ByKey(request);
        lines[PostingKeys.AccountsPayable].Should().Be((200m, 0m));
        lines[PostingKeys.GoodsReceivedNotInvoiced].Should().Be((0m, 200m));
        PostingRules.ForDebitNote(Guid.NewGuid(), "VR-1", "CN-1", 0m, Today).Should().BeNull();
    }

    [Fact]
    public void Goods_sent_back_to_the_supplier_credit_inventory_and_debit_the_receipt_accrual()
    {
        var request = PostingRules.ForStockMovement(Guid.NewGuid(), "ReturnedToVendor", -500m, Today, "VR-1")!;

        var lines = ByKey(request);
        lines[PostingKeys.GoodsReceivedNotInvoiced].Should().Be((500m, 0m));
        lines[PostingKeys.Inventory].Should().Be((0m, 500m));
    }

    // ─── the handlers ─────────────────────────────────────────────────────

    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IBillRepository> _bills = new();
    private readonly Mock<IJournalEntryRepository> _entries = new();
    private readonly Mock<IJournalPostingService> _posting = new();
    private readonly List<PostingRequest> _posted = new();
    private readonly List<Bill> _added = new();

    private void Wire()
    {
        _uow.SetupGet(u => u.Bills).Returns(_bills.Object);
        _uow.SetupGet(u => u.JournalEntries).Returns(_entries.Object);
        _bills.Setup(b => b.AddAsync(It.IsAny<Bill>(), default)).Callback<Bill, CancellationToken>((b, _) => _added.Add(b)).Returns(Task.CompletedTask);
        _posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r)).ReturnsAsync((JournalEntry?)null);
    }

    private static SupplierInvoiceApprovedIntegrationEvent InvoiceEvent(string currency = "VND") => new()
    {
        SupplierInvoiceId = Guid.NewGuid(), InvoiceNumber = "INV-77", SupplierId = Guid.NewGuid(), PurchaseOrderId = Guid.NewGuid(), PONumber = "PO-9",
        InvoiceDate = Today, DueDate = Today.AddDays(30), CurrencyCode = currency, NetAmount = 1050m, TaxAmount = 105m, ReceivedValue = 1000m
    };

    [Fact]
    public async Task An_approved_supplier_invoice_becomes_an_approved_bill_with_the_invoiced_vat_and_the_variance_posting()
    {
        Wire();
        var handler = new SupplierInvoiceApprovedIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<SupplierInvoiceApprovedIntegrationEventHandler>.Instance);
        var evt = InvoiceEvent();

        await handler.Handle(evt, default);

        var bill = _added.Single();
        bill.BillNumber.Should().Be("PO-9/INV-77");
        bill.Amount.Should().Be(1155m);
        bill.NetAmount.Should().Be(1050m);
        bill.TaxAmount.Should().Be(105m);
        bill.TaxRatePercent.Should().Be(10m);
        bill.SupplierInvoiceId.Should().Be(evt.SupplierInvoiceId);
        bill.ReceivedValue.Should().Be(1000m);
        bill.Status.Should().Be(1, "a person already approved the invoice in Purchasing");
        ByKey(_posted.Single())[PostingKeys.PurchasePriceVariance].Should().Be((50m, 0m));
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task A_redelivered_invoice_event_books_no_second_bill()
    {
        Wire();
        var evt = InvoiceEvent();
        _bills.Setup(b => b.GetBySupplierInvoiceIdAsync(evt.SupplierInvoiceId, default)).ReturnsAsync(Bill.Create("x", evt.SupplierId, evt.PurchaseOrderId, 1m, Today));
        var handler = new SupplierInvoiceApprovedIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<SupplierInvoiceApprovedIntegrationEventHandler>.Instance);

        await handler.Handle(evt, default);

        _added.Should().BeEmpty();
        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task An_invoice_in_a_foreign_currency_is_left_pending_for_a_person()
    {
        Wire();
        var vat = new Mock<IVatPolicy>();
        vat.Setup(v => v.GetAsync(default)).ReturnsAsync(new VatSettings(10m, true));
        var handler = new SupplierInvoiceApprovedIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<SupplierInvoiceApprovedIntegrationEventHandler>.Instance, vat.Object);

        await handler.Handle(InvoiceEvent("USD"), default);

        _added.Single().Status.Should().Be(0);
        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task A_supplier_credit_posts_the_debit_note_once_and_comes_off_the_oldest_open_bills_first()
    {
        Wire();
        var po = Guid.NewGuid();
        var older = Bill.Create("B-1", Guid.NewGuid(), po, 300m, Today.AddDays(5)); older.Status = 1;
        var newer = Bill.Create("B-2", Guid.NewGuid(), po, 500m, Today.AddDays(20)); newer.Status = 1;
        _bills.Setup(b => b.GetOpenApprovedByPurchaseOrderIdAsync(po, default)).ReturnsAsync(new List<Bill> { older, newer });
        var handler = new VendorReturnCreditedIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<VendorReturnCreditedIntegrationEventHandler>.Instance);
        var evt = new VendorReturnCreditedIntegrationEvent { VendorReturnId = Guid.NewGuid(), ReturnNumber = "VR-1", PurchaseOrderId = po, CreditNoteNumber = "CN-1", CreditAmount = 400m };

        await handler.Handle(evt, default);

        older.CreditedAmount.Should().Be(300m);
        newer.CreditedAmount.Should().Be(100m);
        ByKey(_posted.Single())[PostingKeys.AccountsPayable].Should().Be((400m, 0m));

        // the same event again: the debit note entry exists, nothing more is applied
        _entries.Setup(e => e.GetBySourceKeyAsync(JournalEntry.BuildSourceKey("DebitNote", evt.VendorReturnId), default))
            .ReturnsAsync(JournalEntry.CreatePosted("JE-1", Guid.NewGuid(), Today, "d", "DebitNote", evt.VendorReturnId,
                new[] { new JournalLineDraft(Guid.NewGuid(), 1m, 0m, ""), new JournalLineDraft(Guid.NewGuid(), 0m, 1m, "") }));
        await handler.Handle(evt, default);
        older.CreditedAmount.Should().Be(300m);
        _posted.Should().HaveCount(1);
    }

    // ─── the consumer ─────────────────────────────────────────────────────

    [Fact]
    public void The_purchase_order_approval_no_longer_creates_a_bill()
    {
        typeof(AccountingKafkaConsumer).Assembly.GetTypes().Select(t => t.Name).Should().NotContain("PurchaseOrderApprovedIntegrationEventHandler");
        typeof(SupplierInvoiceApprovedIntegrationEvent).Assembly.GetTypes().Select(t => t.Name).Should().NotContain("PurchaseOrderApprovedIntegrationEventHandler");
    }
}
