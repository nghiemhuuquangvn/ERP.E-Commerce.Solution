using System.Text.Json;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EInvoicing
{
    /// <summary>Replaces the issued e-invoice of an invoice by a corrected one (ROADMAP P1-3b). A new VAT split can be given: it is applied to the invoice first, the replacement is issued with the invoice's amounts, the original becomes Replaced.</summary>
    public class ReplaceEInvoiceCommand : IRequest<EInvoiceDto>
    {
        public Guid InvoiceId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? BuyerName { get; set; }
        public string? BuyerTaxCode { get; set; }
        public string? BuyerAddress { get; set; }

        /// <summary>A new single VAT rate for the invoice (percent); empty keeps the split.</summary>
        public decimal? TaxRatePercent { get; set; }

        /// <summary>New VAT lines (their gross amounts add up to the invoice amount); win over the rate.</summary>
        public List<VatLineInput> Lines { get; set; } = new();
    }

    /// <summary>Issues an adjustment e-invoice: signed changes of net and VAT referring to the invoice's active e-invoice (ROADMAP P1-3b). The accounting invoice is not changed.</summary>
    public class AdjustEInvoiceCommand : IRequest<EInvoiceDto>
    {
        public Guid InvoiceId { get; set; }
        public decimal AdjustNet { get; set; }
        public decimal AdjustTax { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    internal static class EInvoiceKindSupport
    {
        public static async Task<(IEInvoiceProvider Provider, TenantTaxSettingsView Settings, Guid TenantId)> RequireAsync(
            ITaxSettingsService settingsService, IEInvoiceProviderFactory providers, ITenantProvider tenant, CancellationToken ct)
        {
            var tenantId = tenant.CurrentTenantId ?? throw new BusinessRuleException("Accounting", "No tenant in the request.");
            var s = await settingsService.GetTenantSettingsAsync(tenantId, ct);
            var provider = providers.Get(s.EInvoiceProvider)
                ?? throw new BusinessRuleException("Accounting", "No e-invoice provider is configured for this tenant: choose one in the tax settings.");
            if (string.IsNullOrWhiteSpace(s.CompanyTaxCode))
                throw new BusinessRuleException("Accounting", "The company tax code is missing: set it in the tax settings before issuing e-invoices.");
            if (string.IsNullOrWhiteSpace(s.EInvoiceSeries))
                throw new BusinessRuleException("Accounting", "The e-invoice series is missing: set it in the tax settings.");
            return (provider, new TenantTaxSettingsView(s.CompanyTaxCode!, s.EInvoiceSeries!), tenantId);
        }

        public static async Task<EInvoiceIssueResult> CallAsync(IEInvoiceProvider provider, EInvoicePayload payload, ILogger logger, CancellationToken ct)
        {
            try
            {
                return await provider.IssueAsync(payload, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "E-invoice provider {Provider} failed for {Invoice}", provider.Code, payload.InvoiceNumber);
                return new EInvoiceIssueResult(false, null, null, null, ex.Message);
            }
        }
    }

    internal readonly record struct TenantTaxSettingsView(string CompanyTaxCode, string Series);

    public class ReplaceEInvoiceCommandHandler : IRequestHandler<ReplaceEInvoiceCommand, EInvoiceDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IEInvoiceRepository _einvoices;
        private readonly IEInvoiceProviderFactory _providers;
        private readonly ITaxSettingsService _settings;
        private readonly ITenantProvider _tenant;
        private readonly IMediator _mediator;
        private readonly IVatLineRepository? _vatLines;
        private readonly ILogger<ReplaceEInvoiceCommandHandler> _logger;

        public ReplaceEInvoiceCommandHandler(
            IAccountingUnitOfWork uow, IEInvoiceRepository einvoices, IEInvoiceProviderFactory providers, ITaxSettingsService settings, ITenantProvider tenant,
            IMediator mediator, ILogger<ReplaceEInvoiceCommandHandler> logger, IVatLineRepository? vatLines = null)
        {
            _uow = uow;
            _einvoices = einvoices;
            _providers = providers;
            _settings = settings;
            _tenant = tenant;
            _mediator = mediator;
            _logger = logger;
            _vatLines = vatLines;
        }

        public async Task<EInvoiceDto> Handle(ReplaceEInvoiceCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new BusinessRuleException("Accounting", "A reason is required to replace an e-invoice.");
            var (provider, tenantSettings, tenantId) = await EInvoiceKindSupport.RequireAsync(_settings, _providers, _tenant, cancellationToken);
            var original = await _einvoices.GetActiveByInvoiceAsync(request.InvoiceId, cancellationToken);
            if (original is not { Status: (int)EInvoiceStatus.Issued })
                throw new BusinessRuleException("Accounting", "This invoice has no issued e-invoice to replace: issue one first.");

            // a corrected VAT split goes into the books first; if the provider then fails the replacement can be retried with no split
            if (request.Lines is { Count: > 0 } || request.TaxRatePercent.HasValue)
                await _mediator.Send(new AdjustInvoiceVatCommand
                {
                    InvoiceId = request.InvoiceId, TaxRatePercent = request.TaxRatePercent ?? 0m, Lines = request.Lines ?? new(), ForReplacement = true,
                }, cancellationToken);

            var invoice = await _uow.Invoices.GetByIdAsync(request.InvoiceId, cancellationToken)
                ?? throw new KeyNotFoundException($"Invoice {request.InvoiceId} not found.");
            var profile = await _uow.ReportProfiles.GetAsync(cancellationToken);
            var buyerName = string.IsNullOrWhiteSpace(request.BuyerName) ? original.BuyerName : request.BuyerName!;
            var buyerTax = string.IsNullOrWhiteSpace(request.BuyerName) && string.IsNullOrWhiteSpace(request.BuyerTaxCode) ? original.BuyerTaxCode : request.BuyerTaxCode;
            var buyerAddress = string.IsNullOrWhiteSpace(request.BuyerName) && string.IsNullOrWhiteSpace(request.BuyerAddress) ? original.BuyerAddress : request.BuyerAddress;
            var payload = new EInvoicePayload(
                tenantSettings.Series, invoice.InvoiceNumber, DateTime.UtcNow.Date,
                new EInvoiceParty(profile?.CompanyName ?? string.Empty, tenantSettings.CompanyTaxCode, profile?.Address ?? string.Empty),
                new EInvoiceParty(buyerName, buyerTax?.Trim() ?? string.Empty, buyerAddress?.Trim() ?? string.Empty),
                "VND", $"Order {invoice.OrderId:N}", invoice.EffectiveNet, invoice.TaxRatePercent, invoice.TaxAmount, invoice.Amount)
            {
                Kind = "Replacement",
                Reference = new EInvoiceReference(original.Series, original.Number ?? string.Empty, original.IssuedAt ?? DateTime.UtcNow, request.Reason.Trim()),
                Lines = _vatLines == null ? Array.Empty<EInvoiceLine>()
                    : (await _vatLines.ListAsync(VatLine.InvoiceDocument, new[] { invoice.Id }, cancellationToken))
                        .Select(l => new EInvoiceLine(l.Text ?? $"Order {invoice.OrderId:N}", l.Category == VatLine.NotTaxable, l.RatePercent, l.Net, l.Tax)).ToList(),
            };

            var result = await EInvoiceKindSupport.CallAsync(provider, payload, _logger, cancellationToken);
            if (!result.Success)
                throw new BusinessRuleException("Accounting", $"The provider did not issue the replacement e-invoice: {result.Error}");

            var replacement = EInvoice.StartKind(tenantId, invoice.Id, EInvoiceKind.Replacement, original.Id, request.Reason, null, null,
                provider.Code, payload.Series, buyerName, buyerTax, buyerAddress, JsonSerializer.Serialize(payload));
            replacement.MarkIssued(result.Number ?? string.Empty, result.LookupCode ?? string.Empty, result.ProviderReference ?? string.Empty, DateTime.UtcNow);
            original.MarkReplaced();
            await _einvoices.AddAsync(replacement, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return EInvoiceDto.From(replacement);
        }
    }

    public class AdjustEInvoiceCommandHandler : IRequestHandler<AdjustEInvoiceCommand, EInvoiceDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IEInvoiceRepository _einvoices;
        private readonly IEInvoiceProviderFactory _providers;
        private readonly ITaxSettingsService _settings;
        private readonly ITenantProvider _tenant;
        private readonly ILogger<AdjustEInvoiceCommandHandler> _logger;

        public AdjustEInvoiceCommandHandler(
            IAccountingUnitOfWork uow, IEInvoiceRepository einvoices, IEInvoiceProviderFactory providers, ITaxSettingsService settings, ITenantProvider tenant,
            ILogger<AdjustEInvoiceCommandHandler> logger)
        {
            _uow = uow;
            _einvoices = einvoices;
            _providers = providers;
            _settings = settings;
            _tenant = tenant;
            _logger = logger;
        }

        public async Task<EInvoiceDto> Handle(AdjustEInvoiceCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new BusinessRuleException("Accounting", "A reason is required for an adjustment e-invoice.");
            if (request.AdjustNet == 0m && request.AdjustTax == 0m)
                throw new BusinessRuleException("Accounting", "An adjustment needs a change of the net amount or of the VAT.");
            var (provider, tenantSettings, tenantId) = await EInvoiceKindSupport.RequireAsync(_settings, _providers, _tenant, cancellationToken);
            var active = await _einvoices.GetActiveByInvoiceAsync(request.InvoiceId, cancellationToken);
            if (active is not { Status: (int)EInvoiceStatus.Issued })
                throw new BusinessRuleException("Accounting", "This invoice has no issued e-invoice to adjust.");
            var invoice = await _uow.Invoices.GetByIdAsync(request.InvoiceId, cancellationToken)
                ?? throw new KeyNotFoundException($"Invoice {request.InvoiceId} not found.");

            var net = Math.Round(request.AdjustNet, 2);
            var tax = Math.Round(request.AdjustTax, 2);
            var profile = await _uow.ReportProfiles.GetAsync(cancellationToken);
            var payload = new EInvoicePayload(
                tenantSettings.Series, invoice.InvoiceNumber, DateTime.UtcNow.Date,
                new EInvoiceParty(profile?.CompanyName ?? string.Empty, tenantSettings.CompanyTaxCode, profile?.Address ?? string.Empty),
                new EInvoiceParty(active.BuyerName, active.BuyerTaxCode ?? string.Empty, active.BuyerAddress ?? string.Empty),
                "VND", $"Adjustment of {active.Series}-{active.Number}", net, invoice.TaxRatePercent, tax, net + tax)
            {
                Kind = "Adjustment",
                Reference = new EInvoiceReference(active.Series, active.Number ?? string.Empty, active.IssuedAt ?? DateTime.UtcNow, request.Reason.Trim()),
            };

            var result = await EInvoiceKindSupport.CallAsync(provider, payload, _logger, cancellationToken);
            if (!result.Success)
                throw new BusinessRuleException("Accounting", $"The provider did not issue the adjustment e-invoice: {result.Error}");

            var adjustment = EInvoice.StartKind(tenantId, invoice.Id, EInvoiceKind.Adjustment, active.Id, request.Reason, net, tax,
                provider.Code, payload.Series, active.BuyerName, active.BuyerTaxCode, active.BuyerAddress, JsonSerializer.Serialize(payload));
            adjustment.MarkIssued(result.Number ?? string.Empty, result.LookupCode ?? string.Empty, result.ProviderReference ?? string.Empty, DateTime.UtcNow);
            await _einvoices.AddAsync(adjustment, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return EInvoiceDto.From(adjustment);
        }
    }
}
