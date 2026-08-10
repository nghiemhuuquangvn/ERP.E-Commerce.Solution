using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Application.Interfaces.Repositories
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct);

        /// <summary>Global (no tenant filter) — Email is unique per-(Email, TenantId), not
        /// globally, so this can legitimately match a user registered under a different
        /// tenant. Used only by the invite flow (UserService.InviteToTenantAsync) to find
        /// an existing account elsewhere and attach it to a new tenant; login/register use
        /// the tenant-scoped GetByEmailAsync instead.</summary>
        Task<User?> GetByEmailGloballyAsync(string email, CancellationToken ct);
        Task<User> GetByExternalProviderAsync(string provider, string providerKey, Guid tenantId, CancellationToken ct);
        Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct);

        /// <summary>Tenant-boundary-safe fetch — returns null if the user doesn't exist
        /// OR isn't a member (via UserTenants) of the given tenant, collapsing both
        /// outcomes so callers can't distinguish "doesn't exist" from "exists elsewhere."</summary>
        Task<User?> GetByIdInTenantAsync(Guid id, Guid tenantId, CancellationToken ct);
        Task<User?> GetByIdWithDetailsInTenantAsync(Guid id, Guid tenantId, CancellationToken ct);
        Task<bool> IsInTenantAsync(Guid userId, Guid tenantId, CancellationToken ct);

        Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct);
        Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct);
        Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct);
        Task RemoveRoleAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct);

        /// <summary>All user IDs currently holding roleId within tenantId — used to fan out
        /// a "permission granted to your role" notification to every affected user.</summary>
        Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct);
    }
}
