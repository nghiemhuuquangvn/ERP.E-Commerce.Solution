using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Infrastructure.Security;
using ERP.Shared.Authorization;

namespace ERP.Identity.Infrastructure.Services;

public sealed class TenantAccessPolicy : ITenantAccessPolicy
{
    private readonly IUserContextService _userContext;
    private readonly IUnitOfWork _unitOfWork;

    public TenantAccessPolicy(IUserContextService userContext, IUnitOfWork unitOfWork)
    {
        _userContext = userContext;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> CanAccessTenantAsync(Guid targetTenantId, string platformPermission, CancellationToken ct)
    {
        if (!TryGetCaller(out _, out var callerTenantId))
            return false;

        return targetTenantId == callerTenantId
            || await HasPlatformPermissionAsync(platformPermission, ct);
    }

    public async Task<bool> HasPlatformPermissionAsync(string platformPermission, CancellationToken ct)
    {
        if (!TryGetCaller(out var callerUserId, out var callerTenantId) || !PlatformTenant.Is(callerTenantId))
            return false;

        var permissions = await EffectivePermissions.ResolveForUserAsync(_unitOfWork, callerUserId, callerTenantId, ct);
        return permissions.Contains(platformPermission, StringComparer.OrdinalIgnoreCase);
    }

    private bool TryGetCaller(out Guid userId, out Guid tenantId)
    {
        var caller = _userContext.GetCurrentUser();
        tenantId = Guid.Empty;
        return Guid.TryParse(caller.UserId, out userId) && Guid.TryParse(caller.TenantId, out tenantId);
    }
}
