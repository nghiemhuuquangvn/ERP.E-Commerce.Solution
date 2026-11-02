using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Constants;
using ERP.Identity.Infrastructure.Localization;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Localization;

namespace ERP.Identity.Infrastructure.Services;

/// <summary>
/// R91: a shop must never lock itself out. An <i>administrator</i> of a tenant is an active, unlocked member who holds a role that grants
/// <see cref="PermissionConstants.Identity_UsersManage"/> there (permissions decide, never role names). The actions that could remove the last one - and
/// the ones that make no sense at all (locking yourself, deleting a default role, deactivating the platform tenant) - are refused here with a
/// business-rule message the Admin shows as it is.
/// </summary>
public sealed class AdministratorGuard
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStringLocalizer<AuthMessages>? _localizer;

    public AdministratorGuard(IUnitOfWork unitOfWork, IStringLocalizer<AuthMessages>? localizer = null)
    {
        _unitOfWork = unitOfWork;
        _localizer = localizer;
    }

    private string Text(string key, string english, params object[] args)
    {
        var found = _localizer?[key, args];
        return found is { ResourceNotFound: false } ? found.Value : english;
    }

    /// <summary>Locking or deactivating your own account would end your own session and, for the only administrator, the shop's.</summary>
    public void RefuseSelf(Guid targetUserId, Guid? callerUserId, string key, string english)
    {
        if (callerUserId is { } caller && caller == targetUserId)
            throw new BusinessRuleException("CannotActOnSelf", Text(key, english));
    }

    /// <summary>Refuses when <paramref name="targetUserId"/> is an administrator and no other active, unlocked administrator would remain.</summary>
    public async Task RefuseLastAdministratorAsync(Guid targetUserId, Guid tenantId, CancellationToken ct)
    {
        if (!await _unitOfWork.Users.IsAdministratorAsync(targetUserId, tenantId, null, ct))
            return;
        if (await _unitOfWork.Users.CountAdministratorsAsync(tenantId, targetUserId, null, ct) == 0)
            throw LastAdministrator();
    }

    /// <summary>Refuses taking <paramref name="roleId"/> away from <paramref name="targetUserId"/> when that makes them stop being the shop's last administrator.</summary>
    public async Task RefuseLosingLastAdministratorByRoleRemovalAsync(Guid targetUserId, Guid roleId, Guid tenantId, CancellationToken ct)
    {
        if (!await _unitOfWork.Users.IsAdministratorAsync(targetUserId, tenantId, null, ct))
            return;
        if (await _unitOfWork.Users.IsAdministratorAsync(targetUserId, tenantId, roleId, ct))
            return; // another of their roles still grants it
        if (await _unitOfWork.Users.CountAdministratorsAsync(tenantId, targetUserId, null, ct) == 0)
            throw LastAdministrator();
    }

    /// <summary>Refuses deleting a role, or removing its user-management permission, when every current administrator holds it only through that role.</summary>
    public async Task RefuseLosingAdministratorsThroughRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
    {
        if (await _unitOfWork.Users.CountAdministratorsAsync(tenantId, null, null, ct) == 0)
            return; // nobody is an administrator through anything: nothing to protect
        if (await _unitOfWork.Users.CountAdministratorsAsync(tenantId, null, roleId, ct) == 0)
            throw LastAdministrator();
    }

    public void RefuseDefaultRoleDeletion(string roleCode)
    {
        if (SystemRoles.IsDefaultTenantRole(roleCode))
            throw new BusinessRuleException("DefaultRoleCannotBeDeleted",
                Text("Role_DefaultRoleCannotBeDeleted", $"The default role '{roleCode}' cannot be deleted: new accounts and sign-ups depend on it.", roleCode));
    }

    public void RefusePlatformTenantDeactivation(Guid tenantId)
    {
        if (PlatformTenant.Is(tenantId))
            throw new BusinessRuleException("PlatformTenantCannotBeDeactivated",
                Text("Tenant_PlatformCannotBeDeactivated", "The platform tenant cannot be deactivated: it holds the operators of every shop."));
    }

    private BusinessRuleException LastAdministrator() => new("LastAdministrator",
        Text("User_LastAdministrator", "This would leave the shop without an administrator (an active, unlocked user who can manage users). Give another user that permission first."));
}
