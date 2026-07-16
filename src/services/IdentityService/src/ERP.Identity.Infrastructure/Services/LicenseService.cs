using ERP.Identity.Application.DTOs.License;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Shared.Contracts;
using ERP.Shared.Infrastructure.Caching;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Services
{
    /// <summary>
    /// SuperAdmin-managed module/plan catalogs + per-tenant license lifecycle (issue/renew/
    /// suspend/revoke). GetEffectiveModulesAsync is the read path AuthService calls at every
    /// login/refresh to embed "licensed_module" JWT claims — see the short-TTL note on its
    /// cache key below.
    /// </summary>
    public class LicenseService : ILicenseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cache;
        private static readonly TimeSpan CatalogCacheTtl = TimeSpan.FromMinutes(15);

        // GetEffectiveModulesAsync is on the login/refresh hot path and must reflect a
        // SuperAdmin suspending/revoking a license promptly (the whole point of Suspend/
        // Revoke is to be able to lock a tenant out) — a 15-minute catalog-style TTL would
        // let a revoked tenant's users keep refreshing licensed tokens for too long. We use
        // a short 1-minute TTL here instead of skipping caching entirely (login happens
        // often enough that per-request DB round trips would add up), and additionally
        // invalidate it explicitly on every lifecycle action below so the common case
        // (SuperAdmin just changed this tenant's license) is immediate, not just "eventually
        // within a minute".
        private static readonly TimeSpan EffectiveModulesCacheTtl = TimeSpan.FromMinutes(1);

        public LicenseService(IUnitOfWork unitOfWork, ICacheService cache)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
        }

        public Task<List<ModuleDefinitionDto>> GetModulesAsync(CancellationToken ct)
        {
            return _cache.GetOrSetAsync(ModulesCacheKey, async () =>
            {
                var modules = await _unitOfWork.ModuleDefinitions.ListAllAsync(ct);
                return modules.Select(ToDto).ToList();
            }, CatalogCacheTtl, ct);
        }

        public async Task<ModuleDefinitionDto> UpsertModuleAsync(string moduleCode, UpsertModuleRequest request, CancellationToken ct)
        {
            var code = moduleCode.ToUpperInvariant();

            // The {moduleCode} route segment can't be checked by FluentValidation (it isn't
            // part of the body-bound UpsertModuleRequest — see that record's doc comment),
            // so it's validated here instead of silently letting an admin create a catalog
            // row for a code no service actually enforces.
            if (!ERP.Shared.Authorization.ModuleCodes.All.Contains(code))
                throw new ArgumentException($"'{moduleCode}' is not a recognized module code.", nameof(moduleCode));

            var module = await _unitOfWork.ModuleDefinitions.GetByCodeAsync(code, ct);
            if (module == null)
            {
                module = ModuleDefinition.Create(code, request.ModuleName, request.IsActive);
                await _unitOfWork.ModuleDefinitions.AddAsync(module, ct);
            }
            else
            {
                module.Update(request.ModuleName, request.IsActive);
                await _unitOfWork.ModuleDefinitions.UpdateAsync(module, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(ModulesCacheKey, ct);
            return ToDto(module);
        }

        public async Task<List<LicensePlanDto>> GetPlansAsync(CancellationToken ct)
        {
            return await _cache.GetOrSetAsync(PlansCacheKey, async () =>
            {
                var plans = await _unitOfWork.LicensePlans.ListAllAsync(ct);
                var result = new List<LicensePlanDto>();
                foreach (var plan in plans)
                {
                    var modules = await _unitOfWork.LicensePlanModules.GetByPlanIdAsync(plan.Id, ct);
                    result.Add(ToDto(plan, modules.Select(m => m.ModuleCode).ToArray()));
                }
                return result;
            }, CatalogCacheTtl, ct);
        }

        public async Task<LicensePlanDto> UpsertPlanAsync(string planCode, UpsertLicensePlanRequest request, CancellationToken ct)
        {
            var code = planCode.ToUpperInvariant();
            var plan = await _unitOfWork.LicensePlans.GetByCodeAsync(code, ct);
            if (plan == null)
            {
                plan = LicensePlan.Create(code, request.PlanName, request.Description, request.MaxUsers, request.IsActive);
                await _unitOfWork.LicensePlans.AddAsync(plan, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            else
            {
                plan.Update(request.PlanName, request.Description, request.MaxUsers, request.IsActive);
                await _unitOfWork.LicensePlans.UpdateAsync(plan, ct);
            }

            var moduleCodes = request.ModuleCodes.Select(c => c.ToUpperInvariant()).Distinct().ToArray();
            await _unitOfWork.LicensePlanModules.DeleteByPlanIdAsync(plan.Id, ct);
            foreach (var moduleCode in moduleCodes)
                await _unitOfWork.LicensePlanModules.AddAsync(LicensePlanModule.Create(plan.Id, moduleCode), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _cache.RemoveAsync(PlansCacheKey, ct);
            return ToDto(plan, moduleCodes);
        }

        public async Task<TenantLicenseDto?> GetTenantLicenseAsync(Guid tenantId, CancellationToken ct)
        {
            var license = await _unitOfWork.TenantLicenses.GetByTenantIdAsync(tenantId, ct);
            if (license == null)
                return null;

            var modules = await _unitOfWork.TenantLicenseModules.GetByTenantLicenseIdAsync(license.Id, ct);
            return ToDto(license, modules.Select(m => m.ModuleCode).ToArray());
        }

        public async Task<TenantLicenseDto> IssueLicenseAsync(Guid tenantId, IssueLicenseRequest request, string issuedByUserId, CancellationToken ct)
        {
            var moduleCodes = await ResolveModuleCodesAsync(request.PlanId, request.ModuleCodes, ct);
            var startsAt = request.StartsAt ?? DateTime.UtcNow;

            var existing = await _unitOfWork.TenantLicenses.GetByTenantIdAsync(tenantId, ct);
            TenantLicense license;
            if (existing == null)
            {
                license = TenantLicense.Create(tenantId, request.PlanId, request.MaxUsers, startsAt, request.ExpiresAt, issuedByUserId, request.Notes);
                await _unitOfWork.TenantLicenses.AddAsync(license, ct);
            }
            else
            {
                // Unique index on TenantId means a tenant only ever has one TenantLicense
                // row — re-issuing (e.g. after a prior Revoke) reuses that row.
                existing.Reissue(request.PlanId, request.MaxUsers, startsAt, request.ExpiresAt, issuedByUserId, request.Notes);
                await _unitOfWork.TenantLicenses.UpdateAsync(existing, ct);
                license = existing;
            }

            await ReplaceTenantLicenseModulesAsync(license.Id, moduleCodes, ct);
            await AddLicenseChangedOutboxMessageAsync(tenantId, "Issued", ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await InvalidateEffectiveModulesCacheAsync(tenantId, ct);

            return ToDto(license, moduleCodes);
        }

        public async Task<TenantLicenseDto> RenewLicenseAsync(Guid tenantId, RenewLicenseRequest request, CancellationToken ct)
        {
            var license = await _unitOfWork.TenantLicenses.GetByTenantIdAsync(tenantId, ct)
                ?? throw new InvalidOperationException($"No license exists for tenant '{tenantId}' to renew.");

            license.Renew(request.ExpiresAt, request.MaxUsers, request.Notes);
            await _unitOfWork.TenantLicenses.UpdateAsync(license, ct);

            string[] moduleCodes;
            if (request.ModuleCodes != null)
            {
                moduleCodes = request.ModuleCodes.Select(c => c.ToUpperInvariant()).Distinct().ToArray();
                await ReplaceTenantLicenseModulesAsync(license.Id, moduleCodes, ct);
            }
            else
            {
                var modules = await _unitOfWork.TenantLicenseModules.GetByTenantLicenseIdAsync(license.Id, ct);
                moduleCodes = modules.Select(m => m.ModuleCode).ToArray();
            }

            await AddLicenseChangedOutboxMessageAsync(tenantId, "Renewed", ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await InvalidateEffectiveModulesCacheAsync(tenantId, ct);

            return ToDto(license, moduleCodes);
        }

        public async Task SuspendLicenseAsync(Guid tenantId, CancellationToken ct)
        {
            var license = await _unitOfWork.TenantLicenses.GetByTenantIdAsync(tenantId, ct)
                ?? throw new InvalidOperationException($"No license exists for tenant '{tenantId}' to suspend.");

            license.Suspend();
            await _unitOfWork.TenantLicenses.UpdateAsync(license, ct);
            await AddLicenseChangedOutboxMessageAsync(tenantId, "Suspended", ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await InvalidateEffectiveModulesCacheAsync(tenantId, ct);
        }

        public async Task RevokeLicenseAsync(Guid tenantId, CancellationToken ct)
        {
            var license = await _unitOfWork.TenantLicenses.GetByTenantIdAsync(tenantId, ct)
                ?? throw new InvalidOperationException($"No license exists for tenant '{tenantId}' to revoke.");

            license.Revoke();
            await _unitOfWork.TenantLicenses.UpdateAsync(license, ct);
            await AddLicenseChangedOutboxMessageAsync(tenantId, "Revoked", ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await InvalidateEffectiveModulesCacheAsync(tenantId, ct);
        }

        public Task<string[]> GetEffectiveModulesAsync(Guid tenantId, CancellationToken ct)
        {
            return _cache.GetOrSetAsync(EffectiveModulesCacheKey(tenantId), async () =>
            {
                var license = await _unitOfWork.TenantLicenses.GetByTenantIdAsync(tenantId, ct);
                if (license == null || !license.IsCurrentlyValid)
                    return Array.Empty<string>();

                var modules = await _unitOfWork.TenantLicenseModules.GetByTenantLicenseIdAsync(license.Id, ct);

                // A module deactivated at the catalog level (ModuleDefinition.IsActive = false)
                // must stop being granted even to tenants whose TenantLicenseModules row for it
                // still exists — otherwise deactivating a module here would be a no-op for
                // anyone who already had it, defeating the point of the catalog-level switch.
                var catalog = await _unitOfWork.ModuleDefinitions.ListAllAsync(ct);
                var activeCodes = catalog.Where(m => m.IsActive).Select(m => m.ModuleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

                return modules.Select(m => m.ModuleCode).Where(activeCodes.Contains).ToArray();
            }, EffectiveModulesCacheTtl, ct);
        }

        // ----- helpers -----

        private async Task<string[]> ResolveModuleCodesAsync(Guid? planId, string[]? overrideModuleCodes, CancellationToken ct)
        {
            if (overrideModuleCodes != null)
                return overrideModuleCodes.Select(c => c.ToUpperInvariant()).Distinct().ToArray();

            if (planId.HasValue)
            {
                // A deactivated or nonexistent plan must not be silently usable to issue/renew
                // a license — without this check, an inactive plan's module set would still be
                // copied in, and a nonexistent one would only fail later at SaveChangesAsync via
                // the FK constraint (an unhandled 500, not a clean validation error).
                var plan = await _unitOfWork.LicensePlans.GetByIdAsync(planId.Value, ct);
                if (plan == null || !plan.IsActive)
                    throw new InvalidOperationException($"License plan '{planId}' does not exist or is not active.");

                var planModules = await _unitOfWork.LicensePlanModules.GetByPlanIdAsync(planId.Value, ct);
                return planModules.Select(m => m.ModuleCode).ToArray();
            }

            return Array.Empty<string>();
        }

        private async Task ReplaceTenantLicenseModulesAsync(Guid tenantLicenseId, string[] moduleCodes, CancellationToken ct)
        {
            await _unitOfWork.TenantLicenseModules.DeleteByTenantLicenseIdAsync(tenantLicenseId, ct);
            foreach (var code in moduleCodes)
                await _unitOfWork.TenantLicenseModules.AddAsync(TenantLicenseModule.Create(tenantLicenseId, code), ct);
        }

        private Task InvalidateEffectiveModulesCacheAsync(Guid tenantId, CancellationToken ct) =>
            _cache.RemoveAsync(EffectiveModulesCacheKey(tenantId), ct);

        private static ModuleDefinitionDto ToDto(ModuleDefinition m) =>
            new(m.ModuleCode, m.ModuleName, m.IsActive);

        private static LicensePlanDto ToDto(LicensePlan p, string[] moduleCodes) =>
            new(p.Id, p.PlanCode, p.PlanName, p.Description, p.MaxUsers, p.IsActive, moduleCodes);

        private static TenantLicenseDto ToDto(TenantLicense l, string[] moduleCodes) =>
            new(l.Id, l.TenantId, l.PlanId, l.MaxUsers, l.LicenseKey, l.IssuedAt, l.StartsAt, l.ExpiresAt,
                l.Status, l.IsCurrentlyValid, l.IssuedBy, l.Notes, moduleCodes);

        /// <summary>
        /// Same pattern as FeatureFlagService.AddSettingsChangedOutboxMessageAsync — note the
        /// deliberate ABSENCE of a `using ERP.Shared.Messaging;` in this file: IUnitOfWork.
        /// OutboxMessages.AddAsync binds to the LOCAL namespace-less OutboxMessage class
        /// (ERP.Identity.Domain/Entities/OutboxMessage.cs), not ERP.Shared.Messaging.
        /// OutboxMessage — importing that namespace here would silently rebind the type.
        /// </summary>
        private async Task AddLicenseChangedOutboxMessageAsync(Guid tenantId, string action, CancellationToken ct)
        {
            var @event = new TenantLicenseChangedIntegrationEvent { TenantId = tenantId, Action = action };
            var content = JsonConvert.SerializeObject(@event, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOnUtc = DateTime.UtcNow,
                Type = nameof(TenantLicenseChangedIntegrationEvent),
                Content = content
            };
            await _unitOfWork.OutboxMessages.AddAsync(outboxMessage, ct);
        }

        private static string ModulesCacheKey => "license:modules:all";
        private static string PlansCacheKey => "license:plans:all";
        private static string EffectiveModulesCacheKey(Guid tenantId) => $"license:effective-modules:{tenantId}";
    }
}
