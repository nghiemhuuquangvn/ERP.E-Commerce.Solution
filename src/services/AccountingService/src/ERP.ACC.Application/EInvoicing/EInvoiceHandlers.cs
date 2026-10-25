using System.Text.Json;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EInvoicing
{
    public class EInvoiceDto
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public string Provider { get; set; } = string.Empty;

        /// <summary>"Issued", "Cancelled" or "Failed".</summary>
        public string Status { get; set; } = string.Empty;
        public string Series { get; set; } = string.Empty;
        public string? Number { get; set; }
        public string? LookupCode { get; set; }
        public DateTime? IssuedAt { get; set; }
        public string BuyerName { get; set; } = string.Empty;
        public string? BuyerTaxCode { get; set; }
        public string? BuyerAddress { get; set; }
        public string? Error { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? CancelledAt { get; set; }

        internal static EInvoiceDto From(EInvoice e) => new()
        {
            Id = e.Id, InvoiceId = e.InvoiceId, Provider = e.Provider, Status = ((EInvoiceStatus)e.Status).ToString(), Series = e.Series, Number = e.Number,
            LookupCode = e.LookupCode, IssuedAt = e.IssuedAt, BuyerName = e.BuyerName, BuyerTaxCode = e.BuyerTaxCode, BuyerAddress = e.BuyerAddress,
            Error = e.Error, CancelReason = e.CancelReason, CancelledAt = e.CancelledAt,
        };
    }

    /// <summary>Issues the e-invoice of an accounting invoice through the tenant's provider; after it the invoice's VAT is locked.</summary>
    public class IssueEInvoiceCommand : IRequest<EInvoiceDto>
    {
        public Guid InvoiceId { get; set; }

        /// <summary>The buyer as it must appear on the e-invoice; a buyer without a tax code is a consumer ("Retail customer" when no name is given).</summary>
        public string? BuyerName { get; set; }
        public string? BuyerTaxCode { get; set; }
        public string? BuyerAddress { get; set; }
    }

    public class CancelEInvoiceCommand : IRequest<EInvoiceDto>
    {
        public Guid InvoiceId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class GetEInvoiceQuery : IRequest<EInvoiceDto?>
    {
        public Guid InvoiceId { get; set; }
    }

    public class ListEInvoiceProvidersQuery : IRequest<List<string>> { }

    public class ListEInvoiceProvidersQueryHandler : IRequestHandler<ListEInvoiceProvidersQuery, List<string>>
    {
        private readonly IEInvoiceProviderFactory _providers;

        public ListEInvoiceProvidersQueryHandler(IEInvoiceProviderFactory providers) => _providers = providers;

        public Task<List<string>> Handle(ListEInvoiceProvidersQuery request, CancellationToken cancellationToken) => Task.FromResult(_providers.Codes.ToList());
    }

    public class GetEInvoiceQueryHandler : IRequestHandler<GetEInvoiceQuery, EInvoiceDto?>
    {
        private readonly IEInvoiceRepository _einvoices;

        public GetEInvoiceQueryHandler(IEInvoiceRepository einvoices) => _einvoices = einvoices;

        public async Task<EInvoiceDto?> Handle(GetEInvoiceQuery request, CancellationToken cancellationToken)
        {
            var e = await _einvoices.GetLatestByInvoiceAsync(request.InvoiceId, cancellationToken);
            return e == null ? null : EInvoiceDto.From(e);
        }
    }

    public class IssueEInvoiceCommandHandler : IRequestHandler<IssueEInvoiceCommand, EInvoiceDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IEInvoiceRepository _einvoices;
        private readonly IEInvoiceProviderFactory _providers;
        private readonly ITaxSettingsService _settings;
        private readonly ITenantProvider _tenant;
        private readonly ILogger<IssueEInvoiceCommandHandler> _logger;

        public IssueEInvoiceCommandHandler(
            IAccountingUnitOfWork uow, IEInvoiceRepository einvoices, IEInvoiceProviderFactory providers, ITaxSettingsService settings, ITenantProvider tenant,
            ILogger<IssueEInvoiceCommandHandler> logger)
        {
            _uow = uow;
            _einvoices = einvoices;
            _providers = providers;
            _settings = settings;
            _tenant = tenant;
            _logger = logger;
        }

        public async Task<EInvoiceDto> Handle(IssueEInvoiceCommand request, CancellationToken cancellationToken)
        {
            var tenantId = _tenant.CurrentTenantId ?? throw new BusinessRuleException("Accounting", "No tenant in the request.");
            var settings = await _settings.GetTenantSettingsAsync(tenantId, cancellationToken);
            var provider = _providers.Get(settings.EInvoiceProvider)
                ?? throw new BusinessRuleException("Accounting", "No e-invoice provider is configured for this tenant: choose one in the tax settings.");
            if (string.IsNullOrWhiteSpace(settings.CompanyTaxCode))
                throw new BusinessRuleException("Accounting", "The company tax code is missing: set it in the tax settings before issuing e-invoices.");
            if (string.IsNullOrWhiteSpace(settings.EInvoiceSeries))
                throw new BusinessRuleException("Accounting", "The e-invoice series is missing: set it in the tax settings.");

            var invoice = await _uow.Invoices.GetByIdAsync(request.InvoiceId, cancellationToken)
                ?? throw new KeyNotFoundException($"Invoice {request.InvoiceId} not found.");
            if (invoice.Status == (int)InvoiceStatus.Cancelled)
                throw new BusinessRuleException("Accounting", $"Invoice {invoice.InvoiceNumber} is cancelled: no e-invoice can be issued for it.");
            if (invoice.Amount <= 0m)
                throw new BusinessRuleException("Accounting", "An invoice without an amount has no e-invoice.");

            var existing = await _einvoices.GetActiveByInvoiceAsync(invoice.Id, cancellationToken);
            if (existing is { Status: (int)EInvoiceStatus.Issued })
                throw new BusinessRuleException("Accounting", $"Invoice {invoice.InvoiceNumber} already has e-invoice {existing.Series}-{existing.Number}: cancel it first.");

            var profile = await _uow.ReportProfiles.GetAsync(cancellationToken);
            var buyerName = string.IsNullOrWhiteSpace(request.BuyerName) ? "Retail customer" : request.BuyerName!;
            var payload = new EInvoicePayload(
                settings.EInvoiceSeries!, invoice.InvoiceNumber, DateTime.UtcNow.Date,
                new EInvoiceParty(profile?.CompanyName ?? string.Empty, settings.CompanyTaxCode!, profile?.Address ?? string.Empty),
                new EInvoiceParty(buyerName, request.BuyerTaxCode?.Trim() ?? string.Empty, request.BuyerAddress?.Trim() ?? string.Empty),
                "VND", $"Order {invoice.OrderId:N}", invoice.EffectiveNet, invoice.TaxRatePercent, invoice.TaxAmount, invoice.Amount);
            var json = JsonSerializer.Serialize(payload);

            EInvoice e;
            if (existing != null)
            {
                e = existing;
                e.Prepare(provider.Code, payload.Series, buyerName, request.BuyerTaxCode, request.BuyerAddress, json);
            }
            else
            {
                e = EInvoice.Start(tenantId, invoice.Id, provider.Code, payload.Series, buyerName, request.BuyerTaxCode, request.BuyerAddress, json);
                await _einvoices.AddAsync(e, cancellationToken);
            }

            try
            {
                var result = await provider.IssueAsync(payload, cancellationToken);
                if (result.Success)
                    e.MarkIssued(result.Number ?? string.Empty, result.LookupCode ?? string.Empty, result.ProviderReference ?? string.Empty, DateTime.UtcNow);
                else
                    e.MarkFailed(result.Error ?? "The provider refused the e-invoice.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "E-invoice provider {Provider} failed for invoice {Invoice}", provider.Code, invoice.InvoiceNumber);
                e.MarkFailed(ex.Message);
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return EInvoiceDto.From(e);
        }
    }

    public class CancelEInvoiceCommandHandler : IRequestHandler<CancelEInvoiceCommand, EInvoiceDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IEInvoiceRepository _einvoices;
        private readonly IEInvoiceProviderFactory _providers;
        private readonly ILogger<CancelEInvoiceCommandHandler> _logger;

        public CancelEInvoiceCommandHandler(IAccountingUnitOfWork uow, IEInvoiceRepository einvoices, IEInvoiceProviderFactory providers, ILogger<CancelEInvoiceCommandHandler> logger)
        {
            _uow = uow;
            _einvoices = einvoices;
            _providers = providers;
            _logger = logger;
        }

        public async Task<EInvoiceDto> Handle(CancelEInvoiceCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new BusinessRuleException("Accounting", "A reason is required to cancel an e-invoice.");
            var e = await _einvoices.GetActiveByInvoiceAsync(request.InvoiceId, cancellationToken)
                ?? throw new BusinessRuleException("Accounting", "This invoice has no e-invoice to cancel.");
            if (e.Status != (int)EInvoiceStatus.Issued)
                throw new BusinessRuleException("Accounting", "Only an issued e-invoice can be cancelled.");
            var provider = _providers.Get(e.Provider)
                ?? throw new BusinessRuleException("Accounting", $"The provider {e.Provider} of this e-invoice is no longer available.");

            EInvoiceCancelResult result;
            try
            {
                result = await provider.CancelAsync(e.ProviderReference ?? string.Empty, request.Reason, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "E-invoice provider {Provider} failed to cancel {Series}-{Number}", provider.Code, e.Series, e.Number);
                result = new EInvoiceCancelResult(false, ex.Message);
            }
            if (!result.Success)
                throw new BusinessRuleException("Accounting", $"The provider did not cancel the e-invoice: {result.Error}");

            e.MarkCancelled(request.Reason);
            await _uow.SaveChangesAsync(cancellationToken);
            return EInvoiceDto.From(e);
        }
    }
}
