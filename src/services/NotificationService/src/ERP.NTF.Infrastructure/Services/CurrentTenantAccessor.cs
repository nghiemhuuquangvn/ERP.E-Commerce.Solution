using ERP.NTF.Application.Interfaces;
using ERP.Shared.Infrastructure.Data;

namespace ERP.NTF.Infrastructure.Services;

/// <summary>The tenant of the request or of the consumer message being handled (JWT, gateway host or TenantScope).</summary>
public class CurrentTenantAccessor : ICurrentTenantAccessor
{
    private readonly ITenantProvider _tenant;
    public CurrentTenantAccessor(ITenantProvider tenant) => _tenant = tenant;
    public Guid? CurrentTenantId => _tenant.CurrentTenantId;
}
