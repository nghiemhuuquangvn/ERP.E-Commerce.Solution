using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Events;
using ERP.PO.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.PO.UnitTests.Procurement;

/// <summary>ROADMAP P1-8: RFQ states, the three-way match with tolerances, the invoice approval rules, vendor-return limits.</summary>
public class ProcurementDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid SupplierA = Guid.NewGuid();
    private static readonly Guid SupplierB = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 27);

    // ─── RFQ ──────────────────────────────────────────────────────────────

    private static (RequestForQuotation Rfq, Guid Shirt, Guid Pants) SentRfq()
    {
        var rfq = RequestForQuotation.Create("RFQ-1", "Summer stock", Tenant);
        var shirt = rfq.AddLine("SHIRT", "Shirt", 100, "pcs").Id;
        var pants = rfq.AddLine("PANTS", "Pants", 50, "pcs").Id;
        rfq.Invite(SupplierA);
        rfq.Invite(SupplierB);
        rfq.Send();
        return (rfq, shirt, pants);
    }

    [Fact]
    public void An_rfq_needs_lines_and_suppliers_before_it_is_sent()
    {
        var rfq = RequestForQuotation.Create("RFQ-2", "Empty", Tenant);
        FluentActions.Invoking(rfq.Send).Should().Throw<InvalidOperationException>().WithMessage("*line*");

        rfq.AddLine("A", "A", 1, "pcs");
        FluentActions.Invoking(rfq.Send).Should().Throw<InvalidOperationException>().WithMessage("*supplier*");

        rfq.Invite(SupplierA);
        rfq.Send();
        rfq.Status.Should().Be(RfqStatus.Sent);
        FluentActions.Invoking(() => rfq.AddLine("B", "B", 1, "pcs")).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_quotation_must_price_every_line_come_from_an_invited_supplier_and_still_be_valid()
    {
        var (rfq, shirt, pants) = SentRfq();

        FluentActions.Invoking(() => rfq.RecordQuotation(SupplierA, "Q1", Now.AddDays(10), "VND", 7, null, new Dictionary<Guid, decimal> { [shirt] = 10m }, Now))
            .Should().Throw<InvalidOperationException>().WithMessage("*PANTS*");
        FluentActions.Invoking(() => rfq.RecordQuotation(Guid.NewGuid(), "Q1", Now.AddDays(10), "VND", 7, null, new Dictionary<Guid, decimal> { [shirt] = 10m, [pants] = 20m }, Now))
            .Should().Throw<InvalidOperationException>().WithMessage("*not invited*");
        FluentActions.Invoking(() => rfq.RecordQuotation(SupplierA, "Q1", Now.AddDays(-1), "VND", 7, null, new Dictionary<Guid, decimal> { [shirt] = 10m, [pants] = 20m }, Now))
            .Should().Throw<InvalidOperationException>().WithMessage("*expired*");

        rfq.RecordQuotation(SupplierA, "Q1", Now.AddDays(10), "vnd", 7, null, new Dictionary<Guid, decimal> { [shirt] = 10m, [pants] = 20m }, Now);
        rfq.Quotations.Single().CurrencyCode.Should().Be("VND");
        rfq.Invitations.Single(i => i.SupplierId == SupplierA).State.Should().Be(RfqInvitationState.Quoted);
    }

    [Fact]
    public void A_second_quotation_from_the_same_supplier_replaces_the_first()
    {
        var (rfq, shirt, pants) = SentRfq();
        rfq.RecordQuotation(SupplierA, "Q1", Now.AddDays(10), "VND", 7, null, new Dictionary<Guid, decimal> { [shirt] = 10m, [pants] = 20m }, Now);
        rfq.RecordQuotation(SupplierA, "Q1b", Now.AddDays(12), "VND", 5, null, new Dictionary<Guid, decimal> { [shirt] = 9m, [pants] = 19m }, Now);

        var q = rfq.Quotations.Should().ContainSingle().Subject;
        q.QuoteNumber.Should().Be("Q1b");
        q.PriceFor(shirt).Should().Be(9m);
    }

    [Fact]
    public void The_comparison_marks_the_lowest_price_per_line_and_ignores_expired_quotes()
    {
        var (rfq, shirt, pants) = SentRfq();
        rfq.RecordQuotation(SupplierA, "A", Now.AddDays(10), "VND", 7, null, new Dictionary<Guid, decimal> { [shirt] = 10m, [pants] = 20m }, Now);
        rfq.RecordQuotation(SupplierB, "B", Now.AddDays(10), "VND", 3, null, new Dictionary<Guid, decimal> { [shirt] = 9m, [pants] = 25m }, Now);

        var c = rfq.Compare(Now);

        c.Lines.Single(l => l.ProductCode == "SHIRT").LowestPrice.Should().Be(9m);
        c.Lines.Single(l => l.ProductCode == "PANTS").LowestPrice.Should().Be(20m);
        c.Suppliers.Single(s => s.SupplierId == SupplierA).Total.Should().Be(100 * 10m + 50 * 20m);   // 2,000
        c.Suppliers.Single(s => s.SupplierId == SupplierB).Total.Should().Be(100 * 9m + 50 * 25m);    // 2,150
        c.Suppliers.First().SupplierId.Should().Be(SupplierA, "the cheapest total comes first");

        // a week after both expired: nothing is the lowest any more
        rfq.Compare(Now.AddDays(30)).Lines.Should().OnlyContain(l => l.LowestPrice == null);
    }

    [Fact]
    public void Awarding_closes_the_rfq_marks_the_winner_and_refuses_an_expired_or_missing_quote()
    {
        var (rfq, shirt, pants) = SentRfq();
        rfq.RecordQuotation(SupplierA, "A", Now.AddDays(10), "VND", 7, null, new Dictionary<Guid, decimal> { [shirt] = 10m, [pants] = 20m }, Now);

        FluentActions.Invoking(() => rfq.Award(SupplierB, Now)).Should().Throw<InvalidOperationException>().WithMessage("*not quoted*");
        FluentActions.Invoking(() => rfq.Award(SupplierA, Now.AddDays(20))).Should().Throw<InvalidOperationException>().WithMessage("*expired*");

        var winner = rfq.Award(SupplierA, Now);

        winner.IsWinner.Should().BeTrue();
        rfq.Status.Should().Be(RfqStatus.Awarded);
        rfq.AwardedSupplierId.Should().Be(SupplierA);
        FluentActions.Invoking(() => rfq.Cancel()).Should().Throw<InvalidOperationException>();
    }

    // ─── the match ────────────────────────────────────────────────────────

    private static readonly InvoiceMatchTolerance Default = new(0m, 2m);

    private static InvoiceMatchOrderLine Order(decimal received = 10, decimal returned = 0, decimal invoiced = 0, decimal price = 100) =>
        new("SHIRT", 20, received, returned, invoiced, price);

    [Fact]
    public void An_invoice_for_what_was_received_at_the_order_price_matches()
    {
        var outcome = InvoiceMatcher.Match(new[] { Order() }, new[] { new InvoiceMatchInputLine("SHIRT", 10, 100) }, Default);

        outcome.IsMatched.Should().BeTrue();
        outcome.Lines.Single().Verdict.Should().Be("Matched");
        outcome.ReceivedValue.Should().Be(1000m);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(102, true)]    // +2 % is inside the default price tolerance
    [InlineData(98, true)]
    [InlineData(102.5, false)]
    [InlineData(105, false)]
    [InlineData(90, false)]    // a big discount is a variance too: check it before paying
    public void The_price_tolerance_works_both_ways(double invoicedPrice, bool matched)
    {
        var outcome = InvoiceMatcher.Match(new[] { Order() }, new[] { new InvoiceMatchInputLine("SHIRT", 10, (decimal)invoicedPrice) }, Default);

        outcome.IsMatched.Should().Be(matched);
        if (!matched) outcome.Lines.Single().Verdict.Should().Be("PriceVariance");
    }

    [Fact]
    public void Invoicing_more_than_received_goes_on_hold_unless_the_quantity_tolerance_allows_it()
    {
        var order = new[] { Order(received: 10) };
        var lines = new[] { new InvoiceMatchInputLine("SHIRT", 11, 100) };

        var strict = InvoiceMatcher.Match(order, lines, new InvoiceMatchTolerance(0m, 2m));
        strict.IsMatched.Should().BeFalse();
        strict.Lines.Single().Verdict.Should().Be("QuantityVariance");
        strict.Lines.Single().QuantityVariance.Should().Be(1m);

        InvoiceMatcher.Match(order, lines, new InvoiceMatchTolerance(10m, 2m)).IsMatched.Should().BeTrue();
    }

    [Fact]
    public void An_invoice_may_bill_less_than_received_and_earlier_invoices_and_returns_use_up_the_billable_quantity()
    {
        // 10 received, 2 sent back, 3 already invoiced -> 5 left to invoice
        var order = new[] { Order(received: 10, returned: 2, invoiced: 3) };

        InvoiceMatcher.Match(order, new[] { new InvoiceMatchInputLine("SHIRT", 5, 100) }, Default).IsMatched.Should().BeTrue();
        InvoiceMatcher.Match(order, new[] { new InvoiceMatchInputLine("SHIRT", 2, 100) }, Default).IsMatched.Should().BeTrue();
        InvoiceMatcher.Match(order, new[] { new InvoiceMatchInputLine("SHIRT", 6, 100) }, Default).IsMatched.Should().BeFalse();
    }

    [Fact]
    public void A_product_not_on_the_order_and_a_double_variance_are_reported_with_their_reasons()
    {
        var order = new[] { Order(received: 10) };
        var outcome = InvoiceMatcher.Match(order, new[] { new InvoiceMatchInputLine("SHIRT", 12, 110), new InvoiceMatchInputLine("HAT", 1, 5) }, Default);

        outcome.IsMatched.Should().BeFalse();
        outcome.Lines.Select(l => l.Verdict).Should().Equal("QuantityAndPriceVariance", "NotOnOrder");
        outcome.Reasons.Should().HaveCount(3).And.Contain(r => r.Contains("HAT is not on the purchase order"));
    }

    // ─── the invoice ──────────────────────────────────────────────────────

    private static SupplierInvoice Invoice(decimal quantity = 10, decimal price = 100, decimal tax = 100) =>
        SupplierInvoice.Record("INV-1", SupplierA, Guid.NewGuid(), "PO-1", Tenant, Now, Now.AddDays(30), "VND", tax, new[] { ("SHIRT", quantity, price) });

    [Fact]
    public void An_invoice_totals_its_lines_and_refuses_bad_input()
    {
        var invoice = Invoice();

        invoice.NetAmount.Should().Be(1000m);
        invoice.TotalAmount.Should().Be(1100m);
        FluentActions.Invoking(() => SupplierInvoice.Record("", SupplierA, Guid.NewGuid(), "PO", Tenant, Now, Now, "VND", 0, new[] { ("A", 1m, 1m) })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SupplierInvoice.Record("X", SupplierA, Guid.NewGuid(), "PO", Tenant, Now, Now.AddDays(-1), "VND", 0, new[] { ("A", 1m, 1m) })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SupplierInvoice.Record("X", SupplierA, Guid.NewGuid(), "PO", Tenant, Now, Now, "VND", 0, Array.Empty<(string, decimal, decimal)>())).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_matched_invoice_is_approved_once_and_publishes_the_event_with_the_received_value()
    {
        var invoice = Invoice();
        invoice.ApplyMatch(InvoiceMatcher.Match(new[] { Order() }, new[] { new InvoiceMatchInputLine("SHIRT", 10, 100) }, Default));
        invoice.Status.Should().Be(SupplierInvoiceStatus.Matched);

        invoice.Approve("anna", overrideVariance: false, reason: null);

        invoice.Status.Should().Be(SupplierInvoiceStatus.Approved);
        var evt = invoice.DomainEvents.OfType<SupplierInvoiceApprovedDomainEvent>().Single();
        evt.InvoiceNumber.Should().Be("INV-1");
        evt.NetAmount.Should().Be(1000m);
        evt.TaxAmount.Should().Be(100m);
        evt.ReceivedValue.Should().Be(1000m);
        FluentActions.Invoking(() => invoice.Approve("anna", false, null)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => invoice.Reject("late")).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void An_invoice_on_hold_needs_the_variance_permission_and_a_reason_and_the_price_difference_shows_in_the_event()
    {
        var invoice = Invoice(price: 105);       // +5 % over the order price
        invoice.ApplyMatch(InvoiceMatcher.Match(new[] { Order() }, new[] { new InvoiceMatchInputLine("SHIRT", 10, 105) }, Default));
        invoice.Status.Should().Be(SupplierInvoiceStatus.OnHold);
        invoice.HoldReasons.Should().Contain("SHIRT").And.Contain("5");

        FluentActions.Invoking(() => invoice.Approve("anna", overrideVariance: false, reason: "ok")).Should().Throw<InvalidOperationException>().WithMessage("*on hold*");
        FluentActions.Invoking(() => invoice.Approve("anna", overrideVariance: true, reason: " ")).Should().Throw<InvalidOperationException>().WithMessage("*reason*");

        invoice.Approve("anna", true, "supplier raised the price, agreed by phone");

        invoice.ApprovalReason.Should().Contain("agreed");
        var evt = invoice.DomainEvents.OfType<SupplierInvoiceApprovedDomainEvent>().Single();
        evt.NetAmount.Should().Be(1050m);
        evt.ReceivedValue.Should().Be(1000m);        // the receipt was accrued at the order price: the 50 is the price variance
    }

    [Fact]
    public void An_invoice_can_be_matched_again_after_a_receipt_and_a_rejected_invoice_stays_rejected()
    {
        var invoice = Invoice(quantity: 10);
        invoice.ApplyMatch(InvoiceMatcher.Match(new[] { Order(received: 6) }, new[] { new InvoiceMatchInputLine("SHIRT", 10, 100) }, Default));
        invoice.Status.Should().Be(SupplierInvoiceStatus.OnHold);

        invoice.ApplyMatch(InvoiceMatcher.Match(new[] { Order(received: 10) }, new[] { new InvoiceMatchInputLine("SHIRT", 10, 100) }, Default));
        invoice.Status.Should().Be(SupplierInvoiceStatus.Matched);
        invoice.HoldReasons.Should().BeNull();

        invoice.Reject("duplicate");
        FluentActions.Invoking(() => invoice.ApplyMatch(InvoiceMatcher.Match(new[] { Order() }, new[] { new InvoiceMatchInputLine("SHIRT", 10, 100) }, Default)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Invoice().Reject("")).Should().Throw<ArgumentException>();
    }

    // ─── settings ─────────────────────────────────────────────────────────

    [Fact]
    public void The_default_tolerances_are_zero_quantity_and_two_percent_price_and_bad_values_are_refused()
    {
        var s = PurchasingSettings.CreateDefault(Tenant);

        s.QuantityTolerancePercent.Should().Be(0m);
        s.PriceTolerancePercent.Should().Be(2m);
        s.AutoApproveMatched.Should().BeFalse();
        FluentActions.Invoking(() => s.Update(-1, 2, false)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => s.Update(0, 101, false)).Should().Throw<ArgumentException>();
        s.Update(5, 1, true);
        s.PriceTolerancePercent.Should().Be(1m);
    }

    // ─── returns to vendor ────────────────────────────────────────────────

    private static VendorReturn Return(decimal quantity, decimal returnable) =>
        VendorReturn.Create("VR-1", Guid.NewGuid(), "PO-1", SupplierA, Tenant, "VND", "damaged",
            new[] { ("SHIRT", "Shirt", quantity, 100m) }, new Dictionary<string, decimal> { ["SHIRT"] = returnable });

    [Fact]
    public void A_return_cannot_exceed_what_is_returnable_and_needs_a_reason_and_lines()
    {
        Return(3, 3).Value.Should().Be(300m);
        FluentActions.Invoking(() => Return(4, 3)).Should().Throw<InvalidOperationException>().WithMessage("*only 3*");
        FluentActions.Invoking(() => Return(1, 0)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => VendorReturn.Create("VR", Guid.NewGuid(), "PO", SupplierA, Tenant, "VND", " ",
            new[] { ("SHIRT", "Shirt", 1m, 1m) }, new Dictionary<string, decimal> { ["SHIRT"] = 1 })).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shipping_publishes_the_lines_and_a_credit_note_publishes_the_amount_each_only_once()
    {
        var r = Return(2, 5);

        r.Ship();
        var shipped = r.DomainEvents.OfType<VendorReturnShippedDomainEvent>().Single();
        shipped.Lines.Single().Quantity.Should().Be(2m);
        shipped.Lines.Single().UnitPrice.Should().Be(100m);
        FluentActions.Invoking(r.Ship).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(r.Cancel).Should().Throw<InvalidOperationException>("goods already left");

        FluentActions.Invoking(() => r.RecordCredit("", 200)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => r.RecordCredit("CN-1", 0)).Should().Throw<ArgumentException>();
        r.RecordCredit("CN-1", 200m);

        r.Status.Should().Be(VendorReturnStatus.Credited);
        r.DomainEvents.OfType<VendorReturnCreditedDomainEvent>().Single().CreditAmount.Should().Be(200m);
        FluentActions.Invoking(() => r.RecordCredit("CN-2", 1)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_credit_note_needs_a_shipped_return_and_a_draft_return_can_be_cancelled()
    {
        var draft = Return(1, 1);
        FluentActions.Invoking(() => draft.RecordCredit("CN", 10)).Should().Throw<InvalidOperationException>();

        draft.Cancel();
        draft.Status.Should().Be(VendorReturnStatus.Cancelled);
        FluentActions.Invoking(draft.Ship).Should().Throw<InvalidOperationException>();
    }
}
