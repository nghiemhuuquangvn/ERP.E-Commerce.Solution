using ERP.PO.Application.Procurement;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PO.UnitTests.Procurement;

/// <summary>ROADMAP P1-8 handlers against a mocked store: awarding builds a purchase order, an invoice is matched when recorded, returns are limited.</summary>
public class ProcurementHandlerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private readonly Mock<IProcurementStore> _store = new();
    private readonly Mock<IUserContextService> _user = new();
    private PurchasingSettings _settings = PurchasingSettings.CreateDefault(Tenant);

    public ProcurementHandlerTests()
    {
        _user.Setup(u => u.GetCurrentUser()).Returns(new UserContext { UserId = "anna", TenantId = Tenant.ToString() });
        _store.Setup(s => s.GetSettingsAsync(Tenant, default)).ReturnsAsync(() => _settings);
        _store.Setup(s => s.GetSupplierAsync(SupplierId, default)).ReturnsAsync((Supplier?)null);
        _store.Setup(s => s.InvoicedQuantitiesAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), default)).ReturnsAsync(new Dictionary<string, decimal>());
        _store.Setup(s => s.ReturnedQuantitiesAsync(It.IsAny<Guid>(), default)).ReturnsAsync(new Dictionary<string, decimal>());
        _store.Setup(s => s.InvoiceNumberExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), default)).ReturnsAsync(false);
        _store.Setup(s => s.GetSupplierNamesAsync(It.IsAny<IEnumerable<Guid>>(), default)).ReturnsAsync(new Dictionary<Guid, string>());
    }

    /// <summary>An approved order for 20 shirts at 100 with 10 accepted so far.</summary>
    private PurchaseOrder ReceivedOrder(decimal accepted = 10)
    {
        var po = PurchaseOrder.Create("PO-1", SupplierId, Tenant);
        var item = PurchaseOrderItem.Create(po.Id, "SHIRT", "Shirt", 20, "pcs", new Money(100m, "VND"));
        po.AddItem(item);
        po.Submit();
        po.Approve("boss");
        if (accepted > 0)
            po.ReceiveGoods(new Dictionary<Guid, GoodsReceiptLineInput> { [item.Id] = new() { QuantityAccepted = accepted } });
        _store.Setup(s => s.GetOrderAsync(po.Id, default)).ReturnsAsync(po);
        return po;
    }

    // ─── award ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Awarding_an_rfq_creates_a_draft_order_at_the_quoted_prices()
    {
        var rfq = RequestForQuotation.Create("RFQ-9", "Stock", Tenant);
        var line = rfq.AddLine("SHIRT", "Shirt", 40, "pcs").Id;
        rfq.Invite(SupplierId);
        rfq.Send();
        rfq.RecordQuotation(SupplierId, "Q-7", DateTime.UtcNow.AddDays(5), "VND", 6, null, new Dictionary<Guid, decimal> { [line] = 95m }, DateTime.UtcNow);
        _store.Setup(s => s.GetRfqAsync(rfq.Id, default)).ReturnsAsync(rfq);
        PurchaseOrder? created = null;
        _store.Setup(s => s.Add(It.IsAny<PurchaseOrder>())).Callback<PurchaseOrder>(p => created = p);

        var dto = await new AwardRfqCommandHandler(_store.Object, _user.Object).Handle(new AwardRfqCommand(rfq.Id, SupplierId), default);

        dto.Status.Should().Be("Awarded");
        dto.AwardedPurchaseOrderId.Should().Be(created!.Id);
        created.Status.Should().Be(ERP.PO.Domain.Enums.PurchaseOrderStatus.Draft);
        created.SupplierId.Should().Be(SupplierId);
        var item = created.Items.Single();
        (item.ProductCode, item.Quantity, item.UnitPrice.Amount, item.UnitPrice.Currency).Should().Be(("SHIRT", 40m, 95m, "VND"));
        created.Notes.Should().Contain("RFQ-9").And.Contain("Q-7");
        _store.Verify(s => s.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Awarding_a_supplier_that_did_not_quote_is_a_business_rule_error_and_creates_nothing()
    {
        var rfq = RequestForQuotation.Create("RFQ-10", "Stock", Tenant);
        rfq.AddLine("SHIRT", "Shirt", 40, "pcs");
        rfq.Invite(SupplierId);
        rfq.Send();
        _store.Setup(s => s.GetRfqAsync(rfq.Id, default)).ReturnsAsync(rfq);

        var act = () => new AwardRfqCommandHandler(_store.Object, _user.Object).Handle(new AwardRfqCommand(rfq.Id, SupplierId), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _store.Verify(s => s.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    // ─── record an invoice ────────────────────────────────────────────────

    private RecordSupplierInvoiceCommand InvoiceFor(PurchaseOrder po, decimal quantity, decimal price, string number = "INV-1") =>
        new(po.Id, number, DateTime.UtcNow, null, null, 0m, new() { new SupplierInvoiceLineInput("SHIRT", quantity, price) });

    [Fact]
    public async Task An_invoice_for_the_received_goods_at_the_order_price_is_matched_when_recorded()
    {
        var po = ReceivedOrder(10);
        SupplierInvoice? saved = null;
        _store.Setup(s => s.Add(It.IsAny<SupplierInvoice>())).Callback<SupplierInvoice>(i => saved = i);

        var dto = await new RecordSupplierInvoiceCommandHandler(_store.Object, _user.Object).Handle(InvoiceFor(po, 10, 100), default);

        dto.Status.Should().Be("Matched");
        dto.MatchLines.Single().Received.Should().Be(10m);
        saved!.DomainEvents.Should().BeEmpty("a matched invoice still waits for a person to approve it");
    }

    [Fact]
    public async Task An_invoice_for_goods_not_yet_received_goes_on_hold_with_the_reason()
    {
        var po = ReceivedOrder(10);

        var dto = await new RecordSupplierInvoiceCommandHandler(_store.Object, _user.Object).Handle(InvoiceFor(po, 15, 100), default);

        dto.Status.Should().Be("OnHold");
        dto.HoldReasons.Should().Contain("15").And.Contain("10");
    }

    [Fact]
    public async Task An_invoice_in_another_currency_than_the_order_is_held()
    {
        var po = ReceivedOrder(10);
        var command = InvoiceFor(po, 10, 100) with { CurrencyCode = "USD" };

        var dto = await new RecordSupplierInvoiceCommandHandler(_store.Object, _user.Object).Handle(command, default);

        dto.Status.Should().Be("OnHold");
        dto.HoldReasons.Should().Contain("USD");
    }

    [Fact]
    public async Task The_same_supplier_invoice_number_cannot_be_recorded_twice_and_a_draft_order_cannot_be_invoiced()
    {
        var po = ReceivedOrder(10);
        _store.Setup(s => s.InvoiceNumberExistsAsync(SupplierId, "INV-1", default)).ReturnsAsync(true);
        var handler = new RecordSupplierInvoiceCommandHandler(_store.Object, _user.Object);

        await ((Func<Task>)(() => handler.Handle(InvoiceFor(po, 10, 100), default))).Should().ThrowAsync<BusinessRuleException>().WithMessage("*already recorded*");

        var draft = PurchaseOrder.Create("PO-D", SupplierId, Tenant);
        _store.Setup(s => s.GetOrderAsync(draft.Id, default)).ReturnsAsync(draft);
        await ((Func<Task>)(() => handler.Handle(InvoiceFor(draft, 1, 1, "INV-2"), default))).Should().ThrowAsync<BusinessRuleException>().WithMessage("*Draft*");
    }

    [Fact]
    public async Task With_auto_approve_on_a_matched_invoice_is_approved_and_publishes_but_a_held_one_is_not()
    {
        _settings.Update(0, 2, autoApproveMatched: true);
        var po = ReceivedOrder(10);
        var saved = new List<SupplierInvoice>();
        _store.Setup(s => s.Add(It.IsAny<SupplierInvoice>())).Callback<SupplierInvoice>(saved.Add);
        var handler = new RecordSupplierInvoiceCommandHandler(_store.Object, _user.Object);

        (await handler.Handle(InvoiceFor(po, 10, 100, "A"), default)).Status.Should().Be("Approved");
        (await handler.Handle(InvoiceFor(po, 10, 120, "B"), default)).Status.Should().Be("OnHold");

        saved[0].DomainEvents.Should().ContainSingle();
        saved[1].DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task A_tighter_tolerance_holds_an_invoice_the_default_would_pass()
    {
        var po = ReceivedOrder(10);
        _settings.Update(0, 0, false);

        var dto = await new RecordSupplierInvoiceCommandHandler(_store.Object, _user.Object).Handle(InvoiceFor(po, 10, 101), default);

        dto.Status.Should().Be("OnHold");
    }

    [Fact]
    public async Task Approving_an_invoice_on_hold_without_the_variance_permission_is_refused_with_it_accepted()
    {
        var po = ReceivedOrder(10);
        var invoice = SupplierInvoice.Record("INV-9", SupplierId, po.Id, po.PONumber, Tenant, DateTime.UtcNow, DateTime.UtcNow.AddDays(30), "VND", 0, new[] { ("SHIRT", 10m, 110m) });
        invoice.ApplyMatch(InvoiceMatcher.Match(new[] { new InvoiceMatchOrderLine("SHIRT", 20, 10, 0, 0, 100) },
            new[] { new InvoiceMatchInputLine("SHIRT", 10, 110) }, new InvoiceMatchTolerance(0, 2)));
        _store.Setup(s => s.GetInvoiceAsync(invoice.Id, default)).ReturnsAsync(invoice);
        var handler = new ApproveSupplierInvoiceCommandHandler(_store.Object, _user.Object);

        await ((Func<Task>)(() => handler.Handle(new ApproveSupplierInvoiceCommand(invoice.Id, "agreed", CanApproveVariance: false), default))).Should().ThrowAsync<BusinessRuleException>();
        (await handler.Handle(new ApproveSupplierInvoiceCommand(invoice.Id, "agreed", CanApproveVariance: true), default)).Status.Should().Be("Approved");
    }

    // ─── returns ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_return_is_priced_from_the_order_and_limited_to_what_was_received_less_earlier_returns()
    {
        var po = ReceivedOrder(10);
        _store.Setup(s => s.ReturnedQuantitiesAsync(po.Id, default)).ReturnsAsync(new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["SHIRT"] = 4m });
        var handler = new CreateVendorReturnCommandHandler(_store.Object, _user.Object);

        var ok = await handler.Handle(new CreateVendorReturnCommand(po.Id, "damaged", new() { new VendorReturnLineInput("SHIRT", 6) }), default);
        ok.Value.Should().Be(600m);
        ok.Lines.Single().UnitPrice.Should().Be(100m);

        await ((Func<Task>)(() => handler.Handle(new CreateVendorReturnCommand(po.Id, "damaged", new() { new VendorReturnLineInput("SHIRT", 7) }), default)))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*only 6*");
        await ((Func<Task>)(() => handler.Handle(new CreateVendorReturnCommand(po.Id, "x", new() { new VendorReturnLineInput("HAT", 1) }), default)))
            .Should().ThrowAsync<BusinessRuleException>().WithMessage("*HAT*");
    }

    [Fact]
    public async Task The_order_procurement_view_shows_what_can_be_returned_and_billed()
    {
        var po = ReceivedOrder(10);
        _store.Setup(s => s.ReturnedQuantitiesAsync(po.Id, default)).ReturnsAsync(new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["SHIRT"] = 2m });
        _store.Setup(s => s.InvoicedQuantitiesAsync(po.Id, null, default)).ReturnsAsync(new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["SHIRT"] = 3m });

        var view = await new GetOrderProcurementQueryHandler(_store.Object).Handle(new GetOrderProcurementQuery(po.Id), default);

        var line = view!.Lines.Single();
        (line.Ordered, line.Received, line.Returned, line.Invoiced, line.Returnable, line.Billable).Should().Be((20m, 10m, 2m, 3m, 8m, 5m));
        view.CurrencyCode.Should().Be("VND");
    }
}
