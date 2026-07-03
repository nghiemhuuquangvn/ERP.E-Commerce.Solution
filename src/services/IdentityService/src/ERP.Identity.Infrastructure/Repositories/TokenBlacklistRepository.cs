using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class TokenBlacklistRepository : ITokenBlacklistRepository
    {
        private readonly AuthDbContext _dbContext;

        public TokenBlacklistRepository(AuthDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(TokenBlacklistEntry entry, CancellationToken ct)
        {
            await _dbContext.Set<TokenBlacklistEntry>().AddAsync(entry, ct);
        }

        public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jti))
                return false;

            return await _dbContext.Set<TokenBlacklistEntry>().AsNoTracking()
                .AnyAsync(t => t.Jti == jti, ct);
        }
    }
}
