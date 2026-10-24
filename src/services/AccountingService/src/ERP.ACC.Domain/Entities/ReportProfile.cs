using ERP.ACC.Domain.Enums;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// What a tenant's statements say about the company and in which unit they are shown (ROADMAP P1-2e): the header (name, tax code, address), the signatories
    /// and the amount unit (VND, thousand, million). One row per tenant; a tenant with no row prints plain VND with no header.
    /// </summary>
    public class ReportProfile : AggregateRoot
    {
        public const int MaxNameLength = 200;
        public const int MaxTaxCodeLength = 20;
        public const int MaxTextLength = 300;

        public string CompanyName { get; private set; } = string.Empty;
        public string TaxCode { get; private set; } = string.Empty;
        public string Address { get; private set; } = string.Empty;
        public string PreparedBy { get; private set; } = string.Empty;
        public string ChiefAccountant { get; private set; } = string.Empty;
        public string Director { get; private set; } = string.Empty;
        public int AmountUnit { get; private set; } = (int)Enums.AmountUnit.Vnd;

        private ReportProfile() { }

        public static ReportProfile Create(Guid tenantId)
        {
            return new ReportProfile { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = DateTime.UtcNow };
        }

        public void Set(string? companyName, string? taxCode, string? address, string? preparedBy, string? chiefAccountant, string? director, AmountUnit unit)
        {
            if (!Enum.IsDefined(typeof(AmountUnit), unit))
                throw new ArgumentException("Unknown amount unit.", nameof(unit));

            CompanyName = Clean(companyName, MaxNameLength, "Company name");
            TaxCode = Clean(taxCode, MaxTaxCodeLength, "Tax code");
            Address = Clean(address, MaxTextLength, "Address");
            PreparedBy = Clean(preparedBy, MaxTextLength, "Prepared by");
            ChiefAccountant = Clean(chiefAccountant, MaxTextLength, "Chief accountant");
            Director = Clean(director, MaxTextLength, "Director");
            AmountUnit = (int)unit;
            UpdatedAt = DateTime.UtcNow;
        }

        private static string Clean(string? value, int max, string field)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length > max)
                throw new ArgumentException($"{field} is longer than {max} characters.");
            return text;
        }
    }
}
