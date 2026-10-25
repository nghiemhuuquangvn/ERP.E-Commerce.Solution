using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public enum EInvoiceStatus
    {
        Issued = 1,
        Cancelled = 2,
        /// <summary>The provider refused or could not be reached; the error is kept and the invoice can be issued again.</summary>
        Failed = 3
    }

    /// <summary>
    /// The electronic invoice (hóa đơn điện tử, Decree 123/2020, Circular 78/2021) issued for an accounting invoice through the tenant's provider (ROADMAP P1-3).
    /// The accounting invoice is the source of the amounts; this record holds what the provider answered (series, number, lookup code) and what was sent.
    /// One active (not cancelled) e-invoice per invoice; a failed one is reused when the invoice is issued again.
    /// </summary>
    public class EInvoice : AggregateRoot
    {
        public Guid InvoiceId { get; private set; }
        public string Provider { get; private set; } = string.Empty;
        public int Status { get; private set; }
        public string Series { get; private set; } = string.Empty;
        public string? Number { get; private set; }
        public string? LookupCode { get; private set; }
        public string? ProviderReference { get; private set; }
        public DateTime? IssuedAt { get; private set; }
        public string BuyerName { get; private set; } = string.Empty;
        public string? BuyerTaxCode { get; private set; }
        public string? BuyerAddress { get; private set; }

        /// <summary>The payload sent to the provider (JSON), kept as the record of what was declared.</summary>
        public string Payload { get; private set; } = string.Empty;
        public string? Error { get; private set; }
        public string? CancelReason { get; private set; }
        public DateTime? CancelledAt { get; private set; }

        private EInvoice() { }

        public static EInvoice Start(Guid tenantId, Guid invoiceId, string provider, string series, string buyerName, string? buyerTaxCode, string? buyerAddress, string payload)
        {
            var e = new EInvoice { Id = Guid.NewGuid(), TenantId = tenantId, InvoiceId = invoiceId, CreatedAt = DateTime.UtcNow };
            e.Prepare(provider, series, buyerName, buyerTaxCode, buyerAddress, payload);
            return e;
        }

        /// <summary>Fills (or refills, for a retry after a failure) what is sent to the provider.</summary>
        public void Prepare(string provider, string series, string buyerName, string? buyerTaxCode, string? buyerAddress, string payload)
        {
            if (Status == (int)EInvoiceStatus.Issued || Status == (int)EInvoiceStatus.Cancelled)
                throw new InvalidOperationException("An issued or cancelled e-invoice cannot be prepared again.");
            Provider = provider;
            Series = series;
            BuyerName = buyerName.Trim();
            BuyerTaxCode = string.IsNullOrWhiteSpace(buyerTaxCode) ? null : buyerTaxCode.Trim();
            BuyerAddress = string.IsNullOrWhiteSpace(buyerAddress) ? null : buyerAddress.Trim();
            Payload = payload;
            Status = (int)EInvoiceStatus.Failed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkIssued(string number, string lookupCode, string providerReference, DateTime issuedAt)
        {
            Status = (int)EInvoiceStatus.Issued;
            Number = number;
            LookupCode = lookupCode;
            ProviderReference = providerReference;
            IssuedAt = issuedAt;
            Error = null;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkFailed(string error)
        {
            Status = (int)EInvoiceStatus.Failed;
            Error = error.Length > 1000 ? error.Substring(0, 1000) : error;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkCancelled(string reason)
        {
            if (Status != (int)EInvoiceStatus.Issued)
                throw new InvalidOperationException("Only an issued e-invoice can be cancelled.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A reason is required to cancel an e-invoice.", nameof(reason));
            Status = (int)EInvoiceStatus.Cancelled;
            CancelReason = reason.Trim();
            CancelledAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
