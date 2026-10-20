using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ERP.Identity.Infrastructure.Services;

public sealed class TenantDomainService : ITenantDomainService
{
    private readonly AuthDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public TenantDomainService(AuthDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<TenantHostResolution?> ResolveAsync(string host, CancellationToken ct)
    {
        var normalized = TenantDomain.NormalizeHost(host);
        if (string.IsNullOrEmpty(normalized))
            return null;

        var platformHost = TenantDomain.NormalizeHost(_configuration["Tenancy:PlatformHost"]);
        if (!string.IsNullOrEmpty(platformHost) && normalized == platformHost)
            return await ByIdAsync(PlatformTenant.Id, ct);

        var customDomain = await _dbContext.Set<TenantDomain>().AsNoTracking()
            .Where(d => d.Host == normalized)
            .Select(d => (Guid?)d.TenantId)
            .FirstOrDefaultAsync(ct);
        if (customDomain is not null)
            return await ByIdAsync(customDomain.Value, ct);

        // The bare storefront domain ("shop.erp.com") would otherwise look like tenant "SHOP"
        // under BaseDomain — it never names a tenant.
        if (normalized == TenantDomain.NormalizeHost(_configuration["Tenancy:StorefrontBaseDomain"]))
            return null;

        // Admin hosts "<code>.<BaseDomain>" and storefront hosts "<code>.<StorefrontBaseDomain>"
        // (e.g. acme.erp.com / acme.shop.erp.com) both map to the tenant with that code.
        foreach (var key in new[] { "Tenancy:StorefrontBaseDomain", "Tenancy:BaseDomain" })
        {
            var baseDomain = TenantDomain.NormalizeHost(_configuration[key]);
            if (string.IsNullOrEmpty(baseDomain) || !normalized.EndsWith("." + baseDomain, StringComparison.Ordinal))
                continue;

            var label = normalized[..^(baseDomain.Length + 1)];
            // Exactly one label: "acme.erp.com" yes, "x.acme.erp.com" no.
            if (label.Length > 0 && !label.Contains('.'))
                return await ByCodeAsync(label, ct);
        }

        var fallbackCode = _configuration["Tenancy:FallbackTenantCode"];
        return string.IsNullOrWhiteSpace(fallbackCode) ? null : await ByCodeAsync(fallbackCode, ct);
    }

    public async Task<IReadOnlyList<TenantDomainDto>> ListAsync(Guid tenantId, CancellationToken ct) =>
        await _dbContext.Set<TenantDomain>().AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.IsPrimary).ThenBy(d => d.Host)
            .Select(d => new TenantDomainDto(d.Id, d.TenantId, d.Host, d.IsPrimary))
            .ToListAsync(ct);

    public async Task<TenantDomainDto> AddAsync(Guid tenantId, string host, bool isPrimary, CancellationToken ct)
    {
        if (!await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, ct))
            throw new EntityNotFoundException("Tenant", tenantId);

        TenantDomain domain;
        try
        {
            domain = TenantDomain.Create(tenantId, host, isPrimary);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException("InvalidTenantHost", ex.Message);
        }

        var platformHost = TenantDomain.NormalizeHost(_configuration["Tenancy:PlatformHost"]);
        if (domain.Host == platformHost || await _dbContext.Set<TenantDomain>().AnyAsync(d => d.Host == domain.Host, ct))
            throw new DuplicateException("Host", domain.Host);

        _dbContext.Set<TenantDomain>().Add(domain);
        await _dbContext.SaveChangesAsync(ct);
        return new TenantDomainDto(domain.Id, domain.TenantId, domain.Host, domain.IsPrimary);
    }

    public async Task RemoveAsync(Guid tenantId, Guid domainId, CancellationToken ct)
    {
        var domain = await _dbContext.Set<TenantDomain>()
            .FirstOrDefaultAsync(d => d.Id == domainId && d.TenantId == tenantId, ct)
            ?? throw new EntityNotFoundException("TenantDomain", domainId);

        _dbContext.Set<TenantDomain>().Remove(domain);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<TenantHostResolution?> ByIdAsync(Guid tenantId, CancellationToken ct) =>
        await _dbContext.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId && t.Status == 1)
            .Select(t => new TenantHostResolution(t.Id, t.TenantCode, t.Id == PlatformTenant.Id))
            .FirstOrDefaultAsync(ct);

    private async Task<TenantHostResolution?> ByCodeAsync(string code, CancellationToken ct)
    {
        var upper = code.Trim().ToUpperInvariant();
        return await _dbContext.Tenants.AsNoTracking()
            .Where(t => t.TenantCode == upper && t.Status == 1)
            .Select(t => new TenantHostResolution(t.Id, t.TenantCode, t.Id == PlatformTenant.Id))
            .FirstOrDefaultAsync(ct);
    }
}
