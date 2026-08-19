using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Localization;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using System.Linq;

namespace ERP.Identity.Infrastructure.Services
{
    public class PasswordPolicyService : IPasswordPolicyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly ISettingsService _settingsService;
        private readonly IStringLocalizer<AuthMessages> _localizer;

        public PasswordPolicyService(
            IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, IConfiguration configuration,
            ISettingsService settingsService, IStringLocalizer<AuthMessages> localizer)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _settingsService = settingsService;
            _localizer = localizer;
        }

        public async Task ValidateNewPasswordAsync(Guid tenantId, Guid userId, string newPlaintextPassword, CancellationToken ct)
        {
            // Complexity rules come from this tenant's own settings (System-ceiling-validated
            // when set — see SettingsService.ValidateTenantNotWeakerThanSystemBaseline), not a
            // single global appsettings default shared by every tenant.
            var tenantSettings = await _settingsService.GetOrCreateTenantSettingsEntityAsync(tenantId, ct);

            if (string.IsNullOrWhiteSpace(newPlaintextPassword) || newPlaintextPassword.Length < tenantSettings.MinPasswordLength)
                throw new BusinessRuleException("PasswordTooShort", _localizer["Password_TooShort", tenantSettings.MinPasswordLength].Value);

            if (tenantSettings.RequireUppercase && !newPlaintextPassword.Any(char.IsUpper))
                throw new BusinessRuleException("PasswordMissingUppercase", _localizer["Password_MissingUppercase"].Value);

            if (tenantSettings.RequireLowercase && !newPlaintextPassword.Any(char.IsLower))
                throw new BusinessRuleException("PasswordMissingLowercase", _localizer["Password_MissingLowercase"].Value);

            if (tenantSettings.RequireDigit && !newPlaintextPassword.Any(char.IsDigit))
                throw new BusinessRuleException("PasswordMissingDigit", _localizer["Password_MissingDigit"].Value);

            if (tenantSettings.RequireSymbol && !newPlaintextPassword.Any(c => !char.IsLetterOrDigit(c)))
                throw new BusinessRuleException("PasswordMissingSymbol", _localizer["Password_MissingSymbol"].Value);

            // HistoryCount has no per-tenant equivalent (unlike the fields above) — stays a
            // system-wide appsettings value.
            var historyCount = GetConfigInt("PasswordPolicy:HistoryCount", 5);
            if (historyCount <= 0)
                return;

            var recent = await _unitOfWork.PasswordHistories.GetRecentAsync(userId, historyCount, ct);
            if (recent.Any(h => _passwordHasher.VerifyPassword(h.PasswordHash, newPlaintextPassword)))
                throw new BusinessRuleException("PasswordRecentlyUsed", _localizer["Password_RecentlyUsed", historyCount].Value);
        }

        public async Task RecordPasswordHistoryAsync(Guid userId, string previousPasswordHash, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(previousPasswordHash))
                return;

            var entry = PasswordHistoryEntry.Create(userId, previousPasswordHash);
            await _unitOfWork.PasswordHistories.AddAsync(entry, ct);

            var historyCount = GetConfigInt("PasswordPolicy:HistoryCount", 5);
            var all = await _unitOfWork.PasswordHistories.GetRecentAsync(userId, historyCount + 50, ct);
            foreach (var stale in all.Skip(historyCount))
                await _unitOfWork.PasswordHistories.DeleteAsync(stale, ct);
        }

        private int GetConfigInt(string key, int fallback)
        {
            var raw = _configuration[key];
            return int.TryParse(raw, out var value) ? value : fallback;
        }
    }
}
