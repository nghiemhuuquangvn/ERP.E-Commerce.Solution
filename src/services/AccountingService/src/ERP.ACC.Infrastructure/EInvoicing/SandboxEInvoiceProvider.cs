using System.Security.Cryptography;
using System.Text;
using ERP.ACC.Application.EInvoicing;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.EInvoicing
{
    /// <summary>
    /// A provider that runs inside the service and needs no account (ROADMAP P1-3): it numbers the e-invoices of a series one after another, makes a lookup code from the
    /// declaration and answers at once. It stands in for VNPT / Viettel / MISA / BKAV until a real connector is registered under that provider's code; it does not talk to
    /// any tax authority.
    /// </summary>
    public class SandboxEInvoiceProvider : IEInvoiceProvider
    {
        public const string ProviderCode = "sandbox";
        private readonly AccountingDbContext _db;

        public SandboxEInvoiceProvider(AccountingDbContext db) => _db = db;

        public string Code => ProviderCode;

        public async Task<EInvoiceIssueResult> IssueAsync(EInvoicePayload payload, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(payload.Seller.TaxCode))
                return new EInvoiceIssueResult(false, null, null, null, "The seller's tax code is missing.");

            // the next number of the series for this tenant (the tenant query filter applies)
            var numbers = await _db.EInvoices.AsNoTracking()
                .Where(e => e.Provider == ProviderCode && e.Series == payload.Series && e.Number != null)
                .Select(e => e.Number!).ToListAsync(ct);
            var next = numbers.Select(n => int.TryParse(n, out var v) ? v : 0).DefaultIfEmpty(0).Max() + 1;
            var number = next.ToString("D7");

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{payload.Seller.TaxCode}|{payload.Series}|{number}|{payload.Gross}"));
            var lookup = Convert.ToHexString(hash).Substring(0, 10);
            return new EInvoiceIssueResult(true, number, lookup, $"sandbox:{payload.Series}:{number}", null);
        }

        public Task<EInvoiceCancelResult> CancelAsync(string providerReference, string reason, CancellationToken ct = default) =>
            Task.FromResult(providerReference.StartsWith("sandbox:", StringComparison.Ordinal)
                ? new EInvoiceCancelResult(true, null)
                : new EInvoiceCancelResult(false, "Unknown e-invoice reference."));
    }

    public class EInvoiceProviderFactory : IEInvoiceProviderFactory
    {
        private readonly IReadOnlyList<IEInvoiceProvider> _providers;

        public EInvoiceProviderFactory(IEnumerable<IEInvoiceProvider> providers) => _providers = providers.ToList();

        public IReadOnlyList<string> Codes => _providers.Select(p => p.Code).ToList();

        public IEInvoiceProvider? Get(string? code) =>
            string.IsNullOrWhiteSpace(code) ? null : _providers.FirstOrDefault(p => string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
