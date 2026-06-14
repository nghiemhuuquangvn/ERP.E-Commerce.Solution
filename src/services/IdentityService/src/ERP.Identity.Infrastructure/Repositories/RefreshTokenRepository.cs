using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Persistence;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
    {

        public RefreshTokenRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            return await _dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
        }

        public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.RefreshTokens.AsNoTracking()
                .Where(rt => rt.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
        {
            var refreshToken = await GetByTokenAsync(token, cancellationToken);
            if (refreshToken != null)
            {
                refreshToken.Revoke();
            }
        }

        public async Task<bool> ExistsAsync(string token, CancellationToken cancellationToken = default)
        {
            return await _dbContext.RefreshTokens.AsNoTracking()
                .AnyAsync(rt => rt.Token == token, cancellationToken);
        }

        public async Task RevokeAllByUserIdAsync(Guid userId, CancellationToken ct)
        {
            var tokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.RevokedAt.HasValue)
                .ToListAsync(ct);

            foreach (var token in tokens)
            {
                token.Revoke();
            }
        }
    }
}