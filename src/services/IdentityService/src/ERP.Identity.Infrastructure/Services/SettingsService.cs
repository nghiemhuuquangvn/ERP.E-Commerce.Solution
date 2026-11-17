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
            ValidateSessionPolicy(request.SessionTimeoutMinutes, request.SessionAbsoluteHours,
                request.CustomerSessionTimeoutMinutes, request.CustomerSessionAbsoluteDays);
            ValidateAccountLockPolicy(request.MaxLoginAttempts, request.AccountLockThreshold, request.AccountLockWindowMinutes);

            var settings = await GetOrCreateSystemSettingsEntityAsync(ct);
            settings.Update(request.GoogleEnabled, request.MicrosoftEnabled, request.FacebookEnabled, request.TwitterEnabled, request.TwoFactorEnabled,
                request.MinPasswordLength, request.RequireUppercase, request.RequireLowercase, request.RequireDigit, request.RequireSymbol,
                request.MaxLoginAttempts, request.LockoutDurationMinutes, request.SessionTimeoutMinutes);
            settings.UpdateSessionPolicy(request.SessionTimeoutMinutes, request.SessionAbsoluteHours,
                request.CustomerSessionTimeoutMinutes, request.CustomerSessionAbsoluteDays);
            settings.UpdateAccountLockPolicy(request.AccountLockThreshold, request.AccountLockWindowMinutes);
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
            settings.UpdateSessionPolicy(request.SessionTimeoutMinutes, request.SessionAbsoluteHours,
                request.CustomerSessionTimeoutMinutes, request.CustomerSessionAbsoluteDays);
            settings.UpdateAccountLockPolicy(request.AccountLockThreshold, request.AccountLockWindowMinutes);
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

            // Session policy (SEC-M3, BR-SES-05): every value within range and at most the platform's.
            ValidateSessionPolicy(request.SessionTimeoutMinutes, request.SessionAbsoluteHours,
                request.CustomerSessionTimeoutMinutes, request.CustomerSessionAbsoluteDays);
            if (request.SessionAbsoluteHours > systemSettings.SessionAbsoluteHours)
                throw new BusinessRuleException("TenantSessionPolicyWeakerThanSystem",
                    $"Tenant's session limit ({request.SessionAbsoluteHours} h) cannot exceed the platform baseline ({systemSettings.SessionAbsoluteHours} h).");
            if (request.CustomerSessionTimeoutMinutes > systemSettings.CustomerSessionTimeoutMinutes)
                throw new BusinessRuleException("TenantSessionPolicyWeakerThanSystem",
                    $"Tenant's customer session timeout ({request.CustomerSessionTimeoutMinutes} min) cannot exceed the platform baseline ({systemSettings.CustomerSessionTimeoutMinutes} min).");
            if (request.CustomerSessionAbsoluteDays > systemSettings.CustomerSessionAbsoluteDays)
                throw new BusinessRuleException("TenantSessionPolicyWeakerThanSystem",
                    $"Tenant's customer session limit ({request.CustomerSessionAbsoluteDays} days) cannot exceed the platform baseline ({systemSettings.CustomerSessionAbsoluteDays} days).");

            // Brute-force protection (S3, BR-BF-06): a shop may lock sooner and for longer, never later or shorter.
            ValidateAccountLockPolicy(request.MaxLoginAttempts, request.AccountLockThreshold, request.AccountLockWindowMinutes);
            if (request.AccountLockThreshold > systemSettings.AccountLockThreshold)
                throw new BusinessRuleException("TenantLockoutPolicyWeakerThanSystem",
                    $"Tenant's account lock threshold ({request.AccountLockThreshold}) cannot exceed the platform baseline ({systemSettings.AccountLockThreshold}).");
            if (request.AccountLockWindowMinutes < systemSettings.AccountLockWindowMinutes)
                throw new BusinessRuleException("TenantLockoutPolicyWeakerThanSystem",
                    $"Tenant's account lock window ({request.AccountLockWindowMinutes} min) cannot be shorter than the platform baseline ({systemSettings.AccountLockWindowMinutes} min).");
        }

        /// <summary>BR-BF-06: per-IP limit 3..20; the account threshold above the per-IP limit (else one IP locks the
        /// account) and at most 1000; window 5 minutes .. 1 day.</summary>
        private static void ValidateAccountLockPolicy(int maxLoginAttempts, int accountLockThreshold, int accountLockWindowMinutes)
        {
            if (maxLoginAttempts < 3 || maxLoginAttempts > 20)
                throw new BusinessRuleException("LockoutPolicyOutOfRange", "Failed attempts per device must be between 3 and 20.");
            if (accountLockThreshold <= maxLoginAttempts || accountLockThreshold > 1000)
                throw new BusinessRuleException("LockoutPolicyOutOfRange",
                    "The account lock threshold must be higher than the per-device limit (one device must not lock the account) and at most 1000.");
            if (accountLockWindowMinutes < 5 || accountLockWindowMinutes > 1440)
                throw new BusinessRuleException("LockoutPolicyOutOfRange", "The account lock window must be between 5 minutes and 1 day.");
        }

        /// <summary>
        /// BR-SES-05: an idle timeout of at least 5 minutes (the access token lives a third of it), an absolute limit
        /// of at least an hour / a day, and the idle timeout never longer than the absolute limit.
        /// </summary>
        private static void ValidateSessionPolicy(int idleMinutes, int absoluteHours, int customerIdleMinutes, int customerAbsoluteDays)
        {
            if (idleMinutes < 5 || customerIdleMinutes < 5)
                throw new BusinessRuleException("SessionPolicyOutOfRange", "A session idle timeout must be at least 5 minutes.");
            if (absoluteHours < 1 || absoluteHours > 24 * 30)
                throw new BusinessRuleException("SessionPolicyOutOfRange", "The back-office session limit must be between 1 hour and 30 days.");
            if (customerAbsoluteDays < 1 || customerAbsoluteDays > 365)
                throw new BusinessRuleException("SessionPolicyOutOfRange", "The customer session limit must be between 1 and 365 days.");
            if (idleMinutes > absoluteHours * 60)
                throw new BusinessRuleException("SessionPolicyOutOfRange", "The back-office idle timeout cannot be longer than the session limit.");
            if (customerIdleMinutes > customerAbsoluteDays * 24 * 60)
                throw new BusinessRuleException("SessionPolicyOutOfRange", "The customer idle timeout cannot be longer than the customer session limit.");
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
            // SEC-M6 (BR-BF-08): a shop may REQUIRE 2FA but never switch off the 2FA a person turned on for themselves;
            // only the platform master switch (an emergency off) does that.
            return systemSettings.TwoFactorEnabled
                && (user.TwoFactorEnabled || (tenantSettings.TwoFactorEnabled && tenantSettings.TwoFactorRequired));
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
                s.MaxLoginAttempts, s.LockoutDurationMinutes, s.SessionTimeoutMinutes,
                s.SessionAbsoluteHours, s.CustomerSessionTimeoutMinutes, s.CustomerSessionAbsoluteDays,
                s.AccountLockThreshold, s.AccountLockWindowMinutes);

        private static TenantAuthSettingsDto ToDto(TenantAuthSettings s) =>
            new(s.TenantId, s.GoogleEnabled, s.MicrosoftEnabled, s.FacebookEnabled, s.TwitterEnabled, s.TwoFactorEnabled, s.TwoFactorRequired,
                s.MinPasswordLength, s.RequireUppercase, s.RequireLowercase, s.RequireDigit, s.RequireSymbol,
                s.MaxLoginAttempts, s.LockoutDurationMinutes, s.SessionTimeoutMinutes,
                s.SessionAbsoluteHours, s.CustomerSessionTimeoutMinutes, s.CustomerSessionAbsoluteDays,
                s.AccountLockThreshold, s.AccountLockWindowMinutes);

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
