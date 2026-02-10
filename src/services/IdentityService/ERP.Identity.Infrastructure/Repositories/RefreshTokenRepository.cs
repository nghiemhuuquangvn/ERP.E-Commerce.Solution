using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Persistence;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class RefreshRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
    {
        private readonly AuthDbContext _dbContext;

        public RefreshRepository(AuthDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task RevokeAsync(string token, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsAsync(string token, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}