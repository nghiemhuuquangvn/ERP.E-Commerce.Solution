using ERP.ACC.Application.Posting;

namespace ERP.ACC.Application.Interfaces
{
    /// <summary>The VAT rate and price convention of the current tenant (TenantTaxSettings); rate 0 when the tenant has not configured one.</summary>
    public interface IVatPolicy
    {
        Task<VatSettings> GetAsync(CancellationToken ct = default);
    }
}
