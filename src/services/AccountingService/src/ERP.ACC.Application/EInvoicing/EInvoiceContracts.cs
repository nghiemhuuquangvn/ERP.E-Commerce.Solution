namespace ERP.ACC.Application.EInvoicing
{
    public record EInvoiceParty(string Name, string TaxCode, string Address);

    /// <summary>What is declared to the provider for one invoice (Decree 123/2020, Circular 78/2021): seller, buyer, one aggregated line, rate and amounts in VND.</summary>
    /// <summary>One line of the declaration: a rate (or goods not subject to VAT) with its net and VAT (ROADMAP P1-3b).</summary>
    public record EInvoiceLine(string Description, bool NotTaxable, decimal RatePercent, decimal Net, decimal Tax);

    /// <summary>The e-invoice a replacement or an adjustment refers to (series, number, date) and why.</summary>
    public record EInvoiceReference(string Series, string Number, DateTime IssuedAt, string Reason);

    public record EInvoicePayload(
        string Series, string InvoiceNumber, DateTime IssueDate, EInvoiceParty Seller, EInvoiceParty Buyer, string Currency,
        string Description, decimal Net, decimal TaxRatePercent, decimal Tax, decimal Gross)
    {
        /// <summary>The VAT lines of the invoice when it has several; empty = one line from the totals.</summary>
        public IReadOnlyList<EInvoiceLine> Lines { get; init; } = Array.Empty<EInvoiceLine>();

        /// <summary>"Original", "Replacement" or "Adjustment"; the amounts of an adjustment are signed changes.</summary>
        public string Kind { get; init; } = "Original";

        /// <summary>Set for a replacement and an adjustment.</summary>
        public EInvoiceReference? Reference { get; init; }
    }

    public record EInvoiceIssueResult(bool Success, string? Number, string? LookupCode, string? ProviderReference, string? Error);

    public record EInvoiceCancelResult(bool Success, string? Error);

    /// <summary>The adapter of an e-invoice provider (VNPT, Viettel, MISA, BKAV …). Real connectors register under their own code; the sandbox one needs no account.</summary>
    public interface IEInvoiceProvider
    {
        string Code { get; }
        Task<EInvoiceIssueResult> IssueAsync(EInvoicePayload payload, CancellationToken ct = default);
        Task<EInvoiceCancelResult> CancelAsync(string providerReference, string reason, CancellationToken ct = default);
    }

    public interface IEInvoiceProviderFactory
    {
        IReadOnlyList<string> Codes { get; }
        IEInvoiceProvider? Get(string? code);
    }
}
