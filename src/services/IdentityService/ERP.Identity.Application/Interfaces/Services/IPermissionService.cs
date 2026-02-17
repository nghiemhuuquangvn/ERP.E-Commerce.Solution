namespace ERP.Identity.Application.Interfaces.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, IEnumerable<string> permissionCodes);
}