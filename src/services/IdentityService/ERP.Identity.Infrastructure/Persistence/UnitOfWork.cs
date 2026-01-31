using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly AuthDbContext _db;

        public UnitOfWork(AuthDbContext db)
        {
            _db = db;
            Users = new UserRepository(db);
            Roles = new RoleRepository(db);
            Permissions = new PermissionRepository(db);
        }
        public IUserRepository Users { get; }

        public IRoleRepository Roles { get; }

        public IPermissionRepository Permissions { get; }


        public async Task SaveChangesAsync(CancellationToken ct) => await _db.SaveChangesAsync(ct);

        public void Dispose() => _db.Dispose();
        

    }
}
