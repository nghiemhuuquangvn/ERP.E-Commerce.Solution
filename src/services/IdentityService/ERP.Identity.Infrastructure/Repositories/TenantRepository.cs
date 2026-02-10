using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;


namespace ERP.Identity.Infrastructure.Repositories
{
    public class TenantRepository : GenericRepository<AuthTenant>, ITenantRepository
    {
        private readonly AuthDbContext _dbContext;
        public TenantRepository(AuthDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }
        

    }
}