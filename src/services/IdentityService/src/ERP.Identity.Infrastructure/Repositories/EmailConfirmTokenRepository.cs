using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Persistence;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class EmailConfirmTokenRepository : GenericRepository<EmailConfirmToken>, IEmailConfirmTokenRepository
    {
        public EmailConfirmTokenRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<EmailConfirmToken?> GetByTokenAsync(string tokenHash, CancellationToken ct)
        {
            return await _dbContext.EmailConfirmTokens.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Token == tokenHash, ct);
        }
    }
}
