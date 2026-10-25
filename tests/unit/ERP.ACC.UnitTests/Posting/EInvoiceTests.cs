using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs.Settings;
using ERP.ACC.Application.EInvoicing;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.EInvoicing;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-3: the e-invoice adapter, the issue / cancel flow and the lock of the invoice's VAT.</summary>
public class EInvoiceTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly List<EInvoice> _stored = new();
    private readonly Invoice _invoice;
    private readonly Mock<IEInvoiceProvider> _provider = new();
    private TenantTaxSettingsDto _settings;
    private int _next = 1;

    public EInvoiceTests()
    {
        _invoice = Invoice.Create("INV-E1", Guid.NewGuid(), Guid.NewGuid(), 1_100_000m, DateTime.UtcNow.AddDays(30));
        _invoice.ApplyVat(1_000_000m, 10m, 100_000m);
        _settings = new TenantTaxSettingsDto(_tenant, "0312345678", 10m, true, "sandbox", "C26TAA");
        _provider.SetupGet(p => p.Code).Returns("sandbox");
        _provider.Setup(p => p.IssueAsync(It.IsAny<EInvoicePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EInvoiceIssueResult(true, (_next++).ToString("D7"), "ABC123DEF4", "sandbox:ref", null));
        _provider.Setup(p => p.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new EInvoiceCancelResult(true, null));
    }

    private (IssueEInvoiceCommandHandler Issue, CancelEInvoiceCommandHandler Cancel, Mock<IEInvoiceRepository> Repo) Handlers()
    {
        var repo = new Mock<IEInvoiceRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<EInvoice>(), It.IsAny<CancellationToken>())).Callback<EInvoice, CancellationToken>((e, _) => _stored.Add(e)).Returns(Task.CompletedTask);
        repo.Setup(r => r.GetActiveByInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _stored.FirstOrDefault(e => e.InvoiceId == id && e.Status != (int)EInvoiceStatus.Cancelled));
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
                new CancelEInvoiceCommandHandler(uow.Object, repo.Object, factory.Object, NullLogger<CancelEInvoiceCommandHandler>.Instance), repo);
    }

    private IssueEInvoiceCommand Command() => new() { InvoiceId = _invoice.Id, BuyerName = "Mau Co Ltd", BuyerTaxCode = "0101234567", BuyerAddress = "1 Le Loi" };

    [Fact]
    public async Task Issuing_sends_the_invoice_split_and_the_parties_and_keeps_the_providers_answer()
    {
        EInvoicePayload? sent = null;
        _provider.Setup(p => p.IssueAsync(It.IsAny<EInvoicePayload>(), It.IsAny<CancellationToken>()))
            .Callback<EInvoicePayload, CancellationToken>((p, _) => sent = p).ReturnsAsync(new EInvoiceIssueResult(true, "0000001", "LOOKUP0001", "sandbox:C26TAA:0000001", null));

        var dto = await Handlers().Issue.Handle(Command(), default);

        sent!.Series.Should().Be("C26TAA");
        (sent.Net, sent.TaxRatePercent, sent.Tax, sent.Gross).Should().Be((1_000_000m, 10m, 100_000m, 1_100_000m));
        sent.Seller.TaxCode.Should().Be("0312345678");
        (sent.Buyer.Name, sent.Buyer.TaxCode).Should().Be(("Mau Co Ltd", "0101234567"));
        (dto.Status, dto.Number, dto.LookupCode, dto.Series).Should().Be(("Issued", "0000001", "LOOKUP0001", "C26TAA"));
        _stored.Should().ContainSingle().Which.Payload.Should().Contain("1100000");
    }

    [Fact]
    public async Task A_buyer_without_a_name_is_a_retail_customer()
    {
        var dto = await Handlers().Issue.Handle(new IssueEInvoiceCommand { InvoiceId = _invoice.Id }, default);

        dto.BuyerName.Should().Be("Retail customer");
        dto.BuyerTaxCode.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "0312345678", "C26TAA", "provider")]
    [InlineData("unknown", "0312345678", "C26TAA", "provider")]
    [InlineData("sandbox", null, "C26TAA", "tax code")]
    [InlineData("sandbox", "0312345678", null, "series")]
    public async Task Issuing_is_refused_until_the_tenant_has_a_provider_a_tax_code_and_a_series(string? provider, string? taxCode, string? series, string mentions)
    {
        _settings = new TenantTaxSettingsDto(_tenant, taxCode, 10m, true, provider, series);

        var act = () => Handlers().Issue.Handle(Command(), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain(mentions);
    }

    [Fact]
    public async Task A_cancelled_invoice_has_no_e_invoice_and_an_issued_one_is_not_issued_twice()
    {
        var handlers = Handlers();
        await handlers.Issue.Handle(Command(), default);

        var twice = () => handlers.Issue.Handle(Command(), default);
        await twice.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already has e-invoice*");

        _invoice.Cancel();
        var cancelled = () => Handlers().Issue.Handle(Command(), default);
        await cancelled.Should().ThrowAsync<BusinessRuleException>().WithMessage("*cancelled*");
    }

    [Fact]
    public async Task A_provider_failure_is_kept_and_the_retry_reuses_the_same_record()
    {
        var handlers = Handlers();
        _provider.SetupSequence(p => p.IssueAsync(It.IsAny<EInvoicePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EInvoiceIssueResult(false, null, null, null, "Invalid buyer tax code"))
            .ThrowsAsync(new HttpRequestException("timeout"))
            .ReturnsAsync(new EInvoiceIssueResult(true, "0000001", "LK", "ref", null));

        var first = await handlers.Issue.Handle(Command(), default);
        var second = await handlers.Issue.Handle(Command(), default);
        var third = await handlers.Issue.Handle(Command(), default);

        (first.Status, first.Error).Should().Be(("Failed", "Invalid buyer tax code"));
        (second.Status, second.Error).Should().Be(("Failed", "timeout"));
        third.Status.Should().Be("Issued");
        third.Error.Should().BeNull();
        _stored.Should().ContainSingle();
    }

    [Fact]
    public async Task Cancelling_needs_a_reason_and_an_issued_e_invoice_and_frees_the_invoice_for_a_new_one()
    {
        var handlers = Handlers();
        var noneYet = () => handlers.Cancel.Handle(new CancelEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "wrong buyer" }, default);
        await noneYet.Should().ThrowAsync<BusinessRuleException>().WithMessage("*no e-invoice*");

        await handlers.Issue.Handle(Command(), default);
        var noReason = () => handlers.Cancel.Handle(new CancelEInvoiceCommand { InvoiceId = _invoice.Id, Reason = " " }, default);
        await noReason.Should().ThrowAsync<BusinessRuleException>();

        var dto = await handlers.Cancel.Handle(new CancelEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "wrong buyer" }, default);
        (dto.Status, dto.CancelReason).Should().Be(("Cancelled", "wrong buyer"));

        var again = await handlers.Issue.Handle(Command(), default);       // a cancelled e-invoice does not block a new one
        again.Status.Should().Be("Issued");
        _stored.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_refusal_by_the_provider_to_cancel_leaves_the_e_invoice_issued()
    {
        var handlers = Handlers();
        await handlers.Issue.Handle(Command(), default);
        _provider.Setup(p => p.CancelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new EInvoiceCancelResult(false, "already declared"));

        var act = () => handlers.Cancel.Handle(new CancelEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "x" }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already declared*");
        _stored.Single().Status.Should().Be((int)EInvoiceStatus.Issued);
    }

    // ---- VAT lock ----

    [Fact]
    public async Task The_VAT_of_an_invoice_with_an_issued_e_invoice_cannot_be_adjusted_until_it_is_cancelled()
    {
        var handlers = Handlers();
        await handlers.Issue.Handle(Command(), default);
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(i => i.GetByIdAsync(_invoice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_invoice);
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => JournalEntry.Create("JE-1", Guid.NewGuid(), "x"));
        var adjust = new AdjustInvoiceVatCommandHandler(uow.Object, posting.Object, NullLogger<AdjustInvoiceVatCommandHandler>.Instance, handlers.Repo.Object);

        var locked = () => adjust.Handle(new AdjustInvoiceVatCommand { InvoiceId = _invoice.Id, TaxRatePercent = 8m }, default);
        await locked.Should().ThrowAsync<BusinessRuleException>().WithMessage("*e-invoice*");

        await handlers.Cancel.Handle(new CancelEInvoiceCommand { InvoiceId = _invoice.Id, Reason = "rate" }, default);
        (await adjust.Handle(new AdjustInvoiceVatCommand { InvoiceId = _invoice.Id, TaxRatePercent = 8m }, default)).TaxRatePercent.Should().Be(8m);
    }

    // ---- entity and provider ----

    [Fact]
    public void An_e_invoice_only_cancels_from_issued_and_never_prepares_again_after_it_was_issued()
    {
        var e = EInvoice.Start(_tenant, Guid.NewGuid(), "sandbox", "C26TAA", "Buyer", null, null, "{}");

        e.Invoking(x => x.MarkCancelled("r")).Should().Throw<InvalidOperationException>();
        e.MarkIssued("0000001", "LK", "ref", DateTime.UtcNow);
        e.Invoking(x => x.Prepare("sandbox", "C26TAA", "Buyer", null, null, "{}")).Should().Throw<InvalidOperationException>();
        e.Invoking(x => x.MarkCancelled(" ")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task The_sandbox_provider_refuses_a_declaration_without_a_seller_tax_code_and_cancels_only_its_own_references()
    {
        var sandbox = new SandboxEInvoiceProvider(null!);
        var payload = new EInvoicePayload("C26TAA", "INV", DateTime.UtcNow, new EInvoiceParty("S", "", ""), new EInvoiceParty("B", "", ""), "VND", "d", 100m, 10m, 10m, 110m);

        (await sandbox.IssueAsync(payload)).Success.Should().BeFalse();
        (await sandbox.CancelAsync("sandbox:C26TAA:0000001", "r")).Success.Should().BeTrue();
        (await sandbox.CancelAsync("other:1", "r")).Success.Should().BeFalse();
    }

    [Fact]
    public void The_factory_finds_a_provider_by_its_code_ignoring_case()
    {
        var factory = new EInvoiceProviderFactory(new IEInvoiceProvider[] { new SandboxEInvoiceProvider(null!) });

        factory.Get(" SANDBOX ").Should().NotBeNull();
        factory.Get("misa").Should().BeNull();
        factory.Get(null).Should().BeNull();
        factory.Codes.Should().Equal("sandbox");
    }
}
