using ERP.Entity.Application.Interfaces.Repositories;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class UserRepository : IGenericRepository<User>, IUserRepository
    {
        private readonly AuthDbContext _dbContext;
        public UserRepository(AuthDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(User user, CancellationToken ct)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            await _dbContext.Users.AddAsync(user, ct);
        }

        public async Task DeleteAsync(User user, CancellationToken ct)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            _dbContext.Users.Remove(user);
        }

        public async Task<IQueryable<User>> GetAllAsync(Expression<Func<User, bool>>? filter = null, Func<IQueryable<User>, IQueryable<User>>? orderBy = null, string? includeProperties = null, CancellationToken ct = default)
        {
            IQueryable<User> query = _dbContext.Users;

            if (filter != null)
                query = query.Where(filter);

            if (!string.IsNullOrEmpty(includeProperties))
            {
                foreach (var property in includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    query = query.Include(property);
            }

            if (orderBy != null)
                query = orderBy(query);

            return await Task.FromResult(query);
        }

        public async Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct)
        {
            // Membership-based (AuthUserTenant), not User.TenantId equality — matches
            // GetByIdInTenantAsync/ListByTenantAsync/IsInTenantAsync below, so a user
            // invited into a second tenant (UserService.InviteToTenantAsync) can log
            // into that tenant too, not just their original home tenant.
            var user = await _dbContext.Users.AsNoTracking()
                                            .FirstOrDefaultAsync(x => x.Email == email && x.UserTenants.Any(ut => ut.TenantId == tenantId), ct);
            return user;
        }

        public async Task<User?> GetByEmailGloballyAsync(string email, CancellationToken ct)
        {
            return await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email, ct);
        }

        public async Task<User?> GetByIdInTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.UserTenants.Any(ut => ut.TenantId == tenantId), ct);
        }

        public async Task<User?> GetByIdWithDetailsInTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .ThenInclude(x => x.RolePermissions)
                .ThenInclude(x => x.Permission)
                .FirstOrDefaultAsync(u => u.Id == id && u.UserTenants.Any(ut => ut.TenantId == tenantId), ct);
        }

        public async Task<bool> IsInTenantAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Set<AuthUserTenant>().AsNoTracking()
                .AnyAsync(ut => ut.UserId == userId && ut.TenantId == tenantId, ct);
        }

        public async Task<User> GetByIdAsync(Guid id, CancellationToken ct)
        {
           var user = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                    .ThenInclude(x => x.RolePermissions)
                        .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException($"User with id '{id}' was not found.");
            return user;
        }

        public async Task UpdateAsync(User user, CancellationToken ct)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            _dbContext.Users.Update(user);
        }

        public async Task<User> GetByExternalProviderAsync(string provider, string providerKey,
             Guid tenantId, CancellationToken ct)
        {
            var logins = await _dbContext.ExternalLogins
                .Where(x => x.Provider == provider && x.ProviderKey == providerKey && x.TenantId == tenantId)
                .ToListAsync(ct);

            var userIds = logins.Select(x => x.UserId);

            return await _dbContext.Users.FirstOrDefaultAsync(x => userIds.Contains(x.Id), ct)
                ?? throw new InvalidOperationException("User not found for the given external provider.");
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken ct)
        {
            return await _dbContext.Users
                        .AsNoTracking()
                        .Select(u => new UserDto(
                            u.Id,
                            u.Email,
                            u.UserRoles.Select(ur => ur.Role.RoleName).ToList(),
                            u.UserRoles
                                .SelectMany(ur => ur.Role.RolePermissions)
                                .Select(rp => rp.Permission.PermissionCode)
                                .Distinct()
                                .ToList()
                        ))
                        .ToListAsync(ct);
        }

        public async Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct)
        {
            return await _dbContext.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .ThenInclude(x => x.RolePermissions)
                .ThenInclude(x => x.Permission)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public async Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct)
        {
            // Scoped through AuthUserTenant membership (not User.TenantId equality) so a
            // user invited into a second tenant shows up there too — matches
            // GetByIdInTenantAsync/IsInTenantAsync's membership model. Eager-loads
            // Role/RolePermissions/Permission since UserService projects Roles/
            // Permissions/RoleAssignments straight off these navigations.
            return await _dbContext.Users
                .AsNoTracking()
                .Where(x => x.UserTenants.Any(ut => ut.TenantId == tenantId))
                .Include(x => x.UserRoles.Where(ur => ur.TenantId == tenantId))
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .OrderBy(x => x.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        public async Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Users.AsNoTracking()
                .AnyAsync(x => x.Email == email && x.TenantId == tenantId, ct);
        }

        public async Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.Users.AsNoTracking()
                .Where(x => x.UserTenants.Any(ut => ut.TenantId == tenantId))
                .CountAsync(ct);
        }

        public async Task RemoveRoleAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
        {
            // Fetched directly from the DbSet (not via a loaded User.UserRoles
            // collection) — UserService.RemoveRoleAsync previously tried removing from
            // an in-memory collection and gave up ("tricky with EF Core"), silently
            // doing nothing. A direct query + Remove() is unambiguous.
            var userRole = await _dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.TenantId == tenantId, ct);

            if (userRole != null)
                _dbContext.UserRoles.Remove(userRole);
        }

        public async Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
        {
            return await _dbContext.UserRoles
                .Where(ur => ur.RoleId == roleId && ur.TenantId == tenantId)
                .Select(ur => ur.UserId)
                .ToListAsync(ct);
        }
    }
}
