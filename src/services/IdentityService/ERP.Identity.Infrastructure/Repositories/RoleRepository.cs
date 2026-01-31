using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly AuthDbContext _db;
        public RoleRepository(AuthDbContext db) { _db = db; }
        public async Task<IReadOnlyList<Role>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _db.UserRoles
            .Where(x => x.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r)
            .ToListAsync(ct);
        }
    }
}
