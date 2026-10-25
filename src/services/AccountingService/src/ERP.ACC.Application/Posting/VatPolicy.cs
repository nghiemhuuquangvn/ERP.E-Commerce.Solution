using ERP.ACC.Application.Interfaces;
using ERP.Shared.Infrastructure.Data;

namespace ERP.ACC.Application.Posting
{
    public class VatPolicy : IVatPolicy
    {
        private readonly ITaxSettingsService _settings;
        private readonly ITenantProvider _tenant;

        public VatPolicy(ITaxSettingsService settings, ITenantProvider tenant)
        {
            _settings = settings;
            _tenant = tenant;
        }

        public async Task<VatSettings> GetAsync(CancellationToken ct = default)
        {
            if (_tenant.CurrentTenantId is not { } tenantId || tenantId == Guid.Empty)
                return new VatSettings(0m, true);
            var s = await _settings.GetTenantSettingsAsync(tenantId, ct);
            return new VatSettings(s.DefaultTaxRatePercent, s.PricesIncludeTax, string.IsNullOrWhiteSpace(s.BaseCurrencyCode) ? "VND" : s.BaseCurrencyCode);
        }
    }
}
