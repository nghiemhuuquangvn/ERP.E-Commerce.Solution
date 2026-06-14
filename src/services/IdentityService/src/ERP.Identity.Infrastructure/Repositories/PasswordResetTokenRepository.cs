using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Persistence;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class PasswordResetTokenRepository : GenericRepository<PasswordResetToken>, IPasswordResetTokenRepository
    {
        public PasswordResetTokenRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct)
        {
            return await _dbContext.PasswordResetTokens.AsNoTracking()
                .FirstOrDefaultAsync(prt => prt.Token == token, ct);
        }
    }
}
