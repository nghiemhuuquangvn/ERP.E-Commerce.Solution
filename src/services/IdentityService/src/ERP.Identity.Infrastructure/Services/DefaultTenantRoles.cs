using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Constants;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Infrastructure.Services;

/// <summary>
/// Roles every tenant needs from its first day: self-service registration (storefront / mobile) assigns CUSTOMER,
/// invited staff get STAFF, USER is the generic fallback. They carry no permissions by themselves
/// (same as the SYSTEM tenant's, migration 006); a tenant admin grants what the shop needs.
/// Idempotent — safe to call for an existing tenant. Without it, registration in a new tenant failed with
/// "Default role ... not found" (risk register R16).
/// </summary>
public static class DefaultTenantRoles
{
    public static readonly (string Code, string Name)[] All =
    {
        (SystemRoles.Customer, "Customer"),
        (SystemRoles.Staff, "Staff"),
        (SystemRoles.User, "User"),
    };

    /// <summary>Adds the missing default roles to the unit of work; the caller saves.</summary>
    public static async Task EnsureAsync(IUnitOfWork unitOfWork, Guid tenantId, CancellationToken ct)
    {
        foreach (var (code, name) in All)
        {
            if (await unitOfWork.Roles.ExistsAsync(code, tenantId, ct)) continue;
            await unitOfWork.Roles.AddAsync(Role.Create(code, name, tenantId), ct);
        }
    }
}
