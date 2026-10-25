using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.DTOs.Settings;
using ERP.ACC.Application.EInvoicing;
using ERP.ACC.Application.EventHandlers;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-3b: replacement and adjustment e-invoices, and the automatic issue of an e-invoice with every invoice.</summary>
public class EInvoiceKindTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly List<EInvoice> _stored = new();
    private readonly List<EInvoicePayload> _sent = new();
    private readonly Invoice _invoice;
    private readonly Mock<IEInvoiceProvider> _provider = new();
    private readonly Mock<IMediator> _mediator = new();
    private TenantTaxSettingsDto _settings;
    private int _next = 1;
    private bool _providerFails;

    public EInvoiceKindTests()
    {
        _invoice = Invoice.Create("INV-K1", Guid.NewGuid(), Guid.NewGuid(), 1_100_000m, DateTime.UtcNow.AddDays(30));
        _invoice.ApplyVat(1_000_000m, 10m, 100_000m);
        _settings = new TenantTaxSettingsDto(_tenant, "0312345678", 10m, true, "sandbox", "C26TAA");
        _provider.SetupGet(p => p.Code).Returns("sandbox");
        _provider.Setup(p => p.IssueAsync(It.IsAny<EInvoicePayload>(), It.IsAny<CancellationToken>())).ReturnsAsync((EInvoicePayload p, CancellationToken _) =>
        {
            _sent.Add(p);
            return _providerFails ? new EInvoiceIssueResult(false, null, null, null, "provider down") : new EInvoiceIssueResult(true, (_next++).ToString("D7"), "LK" + _next, "sandbox:ref" + _next, null);
        });
    }

    private (IssueEInvoiceCommandHandler Issue, ReplaceEInvoiceCommandHandler Replace, AdjustEInvoiceCommandHandler Adjust) Handlers()
    {
        var repo = new Mock<IEInvoiceRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<EInvoice>(), It.IsAny<CancellationToken>())).Callback<EInvoice, CancellationToken>((e, _) => _stored.Add(e)).Returns(Task.CompletedTask);
        repo.Setup(r => r.GetActiveByInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) =>
            _stored.FirstOrDefault(e => e.InvoiceId == id && e.Kind != (int)EInvoiceKind.Adjustment && e.Status != (int)EInvoiceStatus.Cancelled && e.Status != (int)EInvoiceStatus.Replaced));
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(i => i.GetByIdAsync(_invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_invoice);
        var profiles = new Mock<IReportProfileRepository>();
        profiles.Setup(p => p.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ReportProfile?)null);
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        uow.SetupGet(u => u.ReportProfiles).Returns(profiles.Object);
        var factory = new Mock<IEInvoiceProviderFactory>();
        factory.Setup(f => f.Get(It.IsAny<string?>())).Returns((string? code) => string.Equals(code, "sandbox", StringComparison.OrdinalIgnoreCase) ? _provider.Object : null);
        var settings = new Mock<ITaxSettingsService>();
        settings.Setup(s => s.GetTenantSettingsAsync(_tenant, It.IsAny<CancellationToken>())).ReturnsAsync(() => _settings);
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(_tenant);
        return (new IssueEInvoiceCommandHandler(uow.Object, repo.Object, factory.Object, settings.Object, tenant.Object, NullLogger<IssueEInvoiceCommandHandler>.Instance),
                new ReplaceEInvoiceCommandHandler(uow.Object, repo.Object, factory.Object, settings.Object, tenant.Object, _mediator.Object, NullLogger<ReplaceEInvoiceCommandHandler>.Instance),
                new AdjustEInvoiceCommandHandler(uow.Object, repo.Object, factory.Object, settings.Object, tenant.Object, NullLogger<AdjustEInvoiceCommandHandler>.Instance));
    }

    private async Task<(IssueEInvoiceCommandHandler Issue, ReplaceEInvoiceCommandHandler Replace, AdjustEInvoiceCommandHandler Adjust)> WithOriginalAsync()
    {
        var h = Handlers();
        await h.Issue.Handle(new IssueEInvoiceCommand { InvoiceId = _invoice.Id, BuyerName = "Mau Co Ltd", BuyerTaxCode = "0101234567", BuyerAddress = "1 Le Loi" }, default);
        _sent.Clear();
        return h;
    }

    [Fact]
    public async Task A_replacement_takes_the_place_of_the_original_and_refers_to_it()
    {
        var h = await WithOriginalAsync();
        var original = _stored.Single();

        var dto = await h.Replace.Handle(new ReplaceEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "wrong buyer address", BuyerAddress = "2 Hai Ba Trung", BuyerName = "Mau Co Ltd", BuyerTaxCode = "0101234567" }, default);

        (dto.Kind, dto.Status, dto.RelatedEInvoiceId, dto.Reason).Should().Be(("Replacement", "Issued", original.Id, "wrong buyer address"));
        original.Status.Should().Be((int)EInvoiceStatus.Replaced);
        var payload = _sent.Single();
        payload.Kind.Should().Be("Replacement");
        payload.Reference.Should().NotBeNull();
        (payload.Reference!.Series, payload.Reference.Number, payload.Reference.Reason).Should().Be(("C26TAA", "0000001", "wrong buyer address"));
        payload.Buyer.Address.Should().Be("2 Hai Ba Trung");
        _stored.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_replacement_without_buyer_details_keeps_the_original_buyer()
    {
        var h = await WithOriginalAsync();

        await h.Replace.Handle(new ReplaceEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "x" }, default);

        var buyer = _sent.Single().Buyer;
        (buyer.Name, buyer.TaxCode, buyer.Address).Should().Be(("Mau Co Ltd", "0101234567", "1 Le Loi"));
    }

    [Fact]
    public async Task A_new_VAT_split_is_applied_to_the_invoice_before_the_replacement_is_issued()
    {
        var h = await WithOriginalAsync();
        var order = new List<string>();
        _mediator.Setup(m => m.Send(It.IsAny<AdjustInvoiceVatCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<InvoiceDto>, CancellationToken>((c, _) =>
            {
                order.Add("adjust");
                var cmd = (AdjustInvoiceVatCommand)c;
                cmd.ForReplacement.Should().BeTrue();
                var split = VatCalculator.Split(_invoice.Amount, cmd.TaxRatePercent, true);
                _invoice.ApplyVat(split.Net, split.RatePercent, split.Tax);
            }).ReturnsAsync(new InvoiceDto());
        _provider.Setup(p => p.IssueAsync(It.IsAny<EInvoicePayload>(), It.IsAny<CancellationToken>())).Callback(() => order.Add("issue"))
            .ReturnsAsync(new EInvoiceIssueResult(true, "0000002", "LK", "ref", null));

        var dto = await h.Replace.Handle(new ReplaceEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "rate was 8 %", TaxRatePercent = 8m }, default);

        order.Should().Equal("adjust", "issue");
        dto.Status.Should().Be("Issued");
        _invoice.TaxRatePercent.Should().Be(8m);
    }

    [Fact]
    public async Task A_provider_failure_leaves_the_original_issued_and_stores_no_replacement()
    {
        var h = await WithOriginalAsync();
        _providerFails = true;

        var act = () => h.Replace.Handle(new ReplaceEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "x" }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*provider down*");
        _stored.Should().ContainSingle().Which.Status.Should().Be((int)EInvoiceStatus.Issued);
    }

    [Fact]
    public async Task Replacing_needs_a_reason_and_an_issued_e_invoice()
    {
        var h = Handlers();

        var none = () => h.Replace.Handle(new ReplaceEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "x" }, default);
        var noReason = () => h.Replace.Handle(new ReplaceEInvoiceCommand { InvoiceId = _invoice.Id, Reason = " " }, default);

        await none.Should().ThrowAsync<BusinessRuleException>().WithMessage("*no issued e-invoice*");
        await noReason.Should().ThrowAsync<BusinessRuleException>().WithMessage("*reason*");
    }

    [Fact]
    public async Task Adjustments_carry_signed_amounts_refer_to_the_active_e_invoice_and_can_repeat()
    {
        var h = await WithOriginalAsync();
        var original = _stored.Single();

        var first = await h.Adjust.Handle(new AdjustEInvoiceCommand { InvoiceId = _invoice.Id, AdjustNet = -50_000m, AdjustTax = -5_000m, Reason = "return" }, default);
        var second = await h.Adjust.Handle(new AdjustEInvoiceCommand { InvoiceId = _invoice.Id, AdjustNet = 10_000m, AdjustTax = 1_000m, Reason = "price correction" }, default);

        (first.Kind, first.AdjustNet, first.AdjustTax, first.RelatedEInvoiceId).Should().Be(("Adjustment", -50_000m, -5_000m, original.Id));
        second.Status.Should().Be("Issued");
        _sent[0].Gross.Should().Be(-55_000m);
        _sent[0].Kind.Should().Be("Adjustment");
        original.Status.Should().Be((int)EInvoiceStatus.Issued);        // the original stays valid
        _stored.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(0, 0, "reason", "change")]
    [InlineData(10, 1, " ", "reason")]
    public async Task An_adjustment_needs_an_amount_and_a_reason(int net, int tax, string reason, string mentions)
    {
        var h = await WithOriginalAsync();

        var act = () => h.Adjust.Handle(new AdjustEInvoiceCommand { InvoiceId = _invoice.Id, AdjustNet = net, AdjustTax = tax, Reason = reason }, default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain(mentions);
    }

    [Fact]
    public async Task An_adjustment_needs_an_issued_e_invoice()
    {
        var h = Handlers();

        var act = () => h.Adjust.Handle(new AdjustEInvoiceCommand { InvoiceId = _invoice.Id, AdjustNet = -1m, Reason = "r" }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*no issued e-invoice*");
    }

    [Fact]
    public void Only_an_issued_e_invoice_can_be_replaced()
    {
        var e = EInvoice.Start(_tenant, Guid.NewGuid(), "sandbox", "C26TAA", "Buyer", null, null, "{}");

        e.Invoking(x => x.MarkReplaced()).Should().Throw<InvalidOperationException>();
        e.MarkIssued("0000001", "LK", "ref", DateTime.UtcNow);
        e.MarkReplaced();
        e.Status.Should().Be((int)EInvoiceStatus.Replaced);
    }

    // ---- auto-issue ----

    private (SalesOrderCreatedIntegrationEventHandler Handler, Mock<IMediator> Mediator) AutoHandler(bool autoIssue, bool issueFails = false)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateInvoiceCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InvoiceDto { Id = Guid.NewGuid() });
        if (issueFails)
            mediator.Setup(m => m.Send(It.IsAny<IssueEInvoiceCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new BusinessRuleException("Accounting", "no provider"));
        var invoices = new Mock<IInvoiceRepository>();
        var settings = new Mock<ITaxSettingsService>();
        settings.Setup(s => s.GetTenantSettingsAsync(_tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new TenantTaxSettingsDto(_tenant, "0312345678", 10m, true, "sandbox", "C26TAA", autoIssue));
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(_tenant);
        return (new SalesOrderCreatedIntegrationEventHandler(mediator.Object, invoices.Object, NullLogger<SalesOrderCreatedIntegrationEventHandler>.Instance, settings.Object, tenant.Object), mediator);
    }

    private static SalesOrderCreatedIntegrationEvent Order() => new() { OrderId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TotalAmount = 1_100_000m };

    [Fact]
    public async Task With_auto_issue_on_the_new_invoice_is_issued_at_once()
    {
        var (handler, mediator) = AutoHandler(true);

        await handler.Handle(Order(), default);

        mediator.Verify(m => m.Send(It.IsAny<IssueEInvoiceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task With_auto_issue_off_nothing_is_issued()
    {
        var (handler, mediator) = AutoHandler(false);

        await handler.Handle(Order(), default);

        mediator.Verify(m => m.Send(It.IsAny<IssueEInvoiceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_failing_auto_issue_keeps_the_invoice_and_does_not_fail_the_event()
    {
        var (handler, mediator) = AutoHandler(true, issueFails: true);

        var act = () => handler.Handle(Order(), default);

        await act.Should().NotThrowAsync();
        mediator.Verify(m => m.Send(It.IsAny<CreateInvoiceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
