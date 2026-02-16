using ERP.Entity.Application.Interfaces.Repositories;
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

        public Task DeleteAsync(User user, CancellationToken ct)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            _dbContext.Users.Remove(user);
            return Task.CompletedTask;
        }

        public Task<IQueryable<User>> GetAllAsync(Expression<Func<User, bool>>? filter = null, Func<IQueryable<User>, IQueryable<User>>? orderBy = null, string? includeProperties = null, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<IQueryable<User>> GetAllUsersAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct)
        {
            var user = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                    .ThenInclude(x => x.RolePermissions)
                        .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Email == email && x.TenantId.Equals(tenantId), ct);
            return user;
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

        public Task UpdateAsync(User user, CancellationToken ct)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            _dbContext.Users.Update(user);
            return Task.CompletedTask;
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
    }
}
