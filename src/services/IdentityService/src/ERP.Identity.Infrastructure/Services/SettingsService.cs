using ERP.Identity.Application.DTOs.Settings;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Exceptions;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private const string ServiceName = "IdentityService";
        private const string Category = "Auth";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        public SettingsService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<SystemAuthSettingsDto> GetSystemSettingsAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(SystemCacheKey, async () =>
            {
                var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<SystemAuthSettingsDto> UpdateSystemSettingsAsync(UpdateSystemAuthSettingsRequest request, CancellationToken ct)
        {
            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.GoogleEnabled, request.MicrosoftEnabled, request.FacebookEnabled, request.TwitterEnabled, request.TwoFactorEnabled,
                request.MinPasswordLength, request.RequireUppercase, request.RequireLowercase, request.RequireDigit, request.RequireSymbol,
                request.MaxLoginAttempts, request.LockoutDurationMinutes, request.SessionTimeoutMinutes);
            await _unitOfWork.SystemAuthSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("System", null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(SystemCacheKey, ct);
            return ToDto(settings);
        }

        public Task<TenantAuthSettingsDto> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(TenantCacheKey(tenantId), async () =>
            {
                var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return ToDto(settings);
            }, CacheTtl, ct);
        }

        public async Task<TenantAuthSettingsDto> UpdateTenantSettingsAsync(Guid tenantId, UpdateTenantAuthSettingsRequest request, CancellationToken ct)
        {
            var systemSettings = await GetOrCreateSystemSettingsEntityAsync(ct);
            ValidateTenantNotWeakerThanSystemBaseline(request, systemSettings);

            var settings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            settings.Update(request.GoogleEnabled, request.MicrosoftEnabled, request.FacebookEnabled, request.TwitterEnabled, request.TwoFactorEnabled, request.TwoFactorRequired,
                request.MinPasswordLength, request.RequireUppercase, request.RequireLowercase, request.RequireDigit, request.RequireSymbol,
                request.MaxLoginAttempts, request.LockoutDurationMinutes, request.SessionTimeoutMinutes);
            await _unitOfWork.TenantAuthSettings.UpdateAsync(settings, ct);
            await AddSettingsChangedOutboxMessageAsync("Tenant", tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(TenantCacheKey(tenantId), ct);
            return ToDto(settings);
        }

        /// <summary>
        /// A tenant may tighten the platform baseline but never loosen it — e.g. a tenant
        /// can require a longer minimum password than System, but not a shorter one. Pure
        /// input validation (storage+CRUD scope) — does not itself enforce the policy at
        /// login/password-change time, that wiring is a follow-up.
        /// </summary>
        private static void ValidateTenantNotWeakerThanSystemBaseline(UpdateTenantAuthSettingsRequest request, SystemAuthSettings systemSettings)
        {
            if (request.MinPasswordLength < systemSettings.MinPasswordLength)
                throw new BusinessRuleException(
                    "TenantPasswordPolicyWeakerThanSystem",
                    $"Tenant's minimum password length ({request.MinPasswordLength}) cannot be shorter than the platform baseline ({systemSettings.MinPasswordLength}).");

            if (systemSettings.RequireUppercase && !request.RequireUppercase)
                throw new BusinessRuleException("TenantPasswordPolicyWeakerThanSystem", "Tenant cannot disable the uppercase requirement mandated at the platform level.");

            if (systemSettings.RequireLowercase && !request.RequireLowercase)
                throw new BusinessRuleException("TenantPasswordPolicyWeakerThanSystem", "Tenant cannot disable the lowercase requirement mandated at the platform level.");

            if (systemSettings.RequireDigit && !request.RequireDigit)
                throw new BusinessRuleException("TenantPasswordPolicyWeakerThanSystem", "Tenant cannot disable the digit requirement mandated at the platform level.");

            if (systemSettings.RequireSymbol && !request.RequireSymbol)
                throw new BusinessRuleException("TenantPasswordPolicyWeakerThanSystem", "Tenant cannot disable the symbol requirement mandated at the platform level.");

            if (request.MaxLoginAttempts > systemSettings.MaxLoginAttempts)
                throw new BusinessRuleException(
                    "TenantLockoutPolicyWeakerThanSystem",
                    $"Tenant's max login attempts ({request.MaxLoginAttempts}) cannot exceed the platform baseline ({systemSettings.MaxLoginAttempts}).");

            if (request.LockoutDurationMinutes < systemSettings.LockoutDurationMinutes)
                throw new BusinessRuleException(
                    "TenantLockoutPolicyWeakerThanSystem",
                    $"Tenant's lockout duration ({request.LockoutDurationMinutes} min) cannot be shorter than the platform baseline ({systemSettings.LockoutDurationMinutes} min).");

            if (request.SessionTimeoutMinutes > systemSettings.SessionTimeoutMinutes)
                throw new BusinessRuleException(
                    "TenantSessionPolicyWeakerThanSystem",
                    $"Tenant's session timeout ({request.SessionTimeoutMinutes} min) cannot exceed the platform baseline ({systemSettings.SessionTimeoutMinutes} min).");
        }

        public async Task<PublicAuthOptionsDto> GetPublicOptionsAsync(string tenantCode, CancellationToken ct)
        {
            if (!await _unitOfWork.Tenants.ExistsByCodeAsync(tenantCode, ct))
                return new PublicAuthOptionsDto(false, false, false, false, false);

            var tenant = await _unitOfWork.Tenants.GetTenantByCodeAsync(tenantCode, ct);

            var systemSettings = await GetOrCreateSystemSettingsEntityAsync(ct);
            var tenantSettings = await GetOrCreateTenantSettingsEntityAsync(tenant.Id, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return new PublicAuthOptionsDto(
                GoogleEnabled: systemSettings.GoogleEnabled && tenantSettings.GoogleEnabled,
                MicrosoftEnabled: systemSettings.MicrosoftEnabled && tenantSettings.MicrosoftEnabled,
                FacebookEnabled: systemSettings.FacebookEnabled && tenantSettings.FacebookEnabled,
                TwitterEnabled: systemSettings.TwitterEnabled && tenantSettings.TwitterEnabled,
                TwoFactorEnabled: systemSettings.TwoFactorEnabled && tenantSettings.TwoFactorEnabled);
        }

        public async Task<bool> IsProviderEnabledAsync(Guid tenantId, string provider, CancellationToken ct)
        {
            var systemSettings = await GetOrCreateSystemSettingsEntityAsync(ct);
            var tenantSettings = await GetOrCreateTenantSettingsEntityAsync(tenantId, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return provider.ToUpperInvariant() switch
            {
                "GOOGLE" => systemSettings.GoogleEnabled && tenantSettings.GoogleEnabled,
                "MICROSOFT" => systemSettings.MicrosoftEnabled && tenantSettings.MicrosoftEnabled,
                "FACEBOOK" => systemSettings.FacebookEnabled && tenantSettings.FacebookEnabled,
                "TWITTER" => systemSettings.TwitterEnabled && tenantSettings.TwitterEnabled,
                _ => false
            };
        }

        public bool IsTwoFactorRequiredForUser(Domain.Entities.User user, SystemAuthSettings systemSettings, TenantAuthSettings tenantSettings)
        {
            return systemSettings.TwoFactorEnabled
                && tenantSettings.TwoFactorEnabled
                && (tenantSettings.TwoFactorRequired || user.TwoFactorEnabled);
        }

        public async Task<SystemAuthSettings> GetOrCreateSystemSettingsEntityAsync(CancellationToken ct)
        {
            var settings = await _unitOfWork.SystemAuthSettings.GetAsync(ct);
            if (settings != null)
                return settings;

            settings = SystemAuthSettings.CreateDefault();
            await _unitOfWork.SystemAuthSettings.AddAsync(settings, ct);
            return settings;
        }

        public async Task<TenantAuthSettings> GetOrCreateTenantSettingsEntityAsync(Guid tenantId, CancellationToken ct)
        {
            var settings = await _unitOfWork.TenantAuthSettings.GetByTenantIdAsync(tenantId, ct);
            if (settings != null)
                return settings;

            settings = TenantAuthSettings.CreateDefault(tenantId);
            await _unitOfWork.TenantAuthSettings.AddAsync(settings, ct);
            return settings;
        }

        private static SystemAuthSettingsDto ToDto(SystemAuthSettings s) =>
            new(s.GoogleEnabled, s.MicrosoftEnabled, s.FacebookEnabled, s.TwitterEnabled, s.TwoFactorEnabled,
                s.MinPasswordLength, s.RequireUppercase, s.RequireLowercase, s.RequireDigit, s.RequireSymbol,
                s.MaxLoginAttempts, s.LockoutDurationMinutes, s.SessionTimeoutMinutes);

        private static TenantAuthSettingsDto ToDto(TenantAuthSettings s) =>
            new(s.TenantId, s.GoogleEnabled, s.MicrosoftEnabled, s.FacebookEnabled, s.TwitterEnabled, s.TwoFactorEnabled, s.TwoFactorRequired,
                s.MinPasswordLength, s.RequireUppercase, s.RequireLowercase, s.RequireDigit, s.RequireSymbol,
                s.MaxLoginAttempts, s.LockoutDurationMinutes, s.SessionTimeoutMinutes);

        private async Task AddSettingsChangedOutboxMessageAsync(string scope, Guid? tenantId, CancellationToken ct)
        {
            var @event = new SettingsChangedIntegrationEvent { ServiceName = ServiceName, Category = Category, Scope = scope, TenantId = tenantId };
            var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOnUtc = DateTime.UtcNow,
                Type = nameof(SettingsChangedIntegrationEvent),
                Content = content
            };
            await _unitOfWork.OutboxMessages.AddAsync(outboxMessage, ct);
        }

        private static string SystemCacheKey => "settings:identity:auth:system";
        private static string TenantCacheKey(Guid tenantId) => $"settings:identity:auth:tenant:{tenantId}";
    }
}
