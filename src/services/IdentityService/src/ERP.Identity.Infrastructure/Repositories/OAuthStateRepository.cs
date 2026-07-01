using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using System.Threading;
using System.Threading.Tasks;
using ERP.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class OAuthStateRepository : GenericRepository<OAuthState>, IOAuthStateRepository
    {
        public OAuthStateRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<OAuthState?> GetByStateAsync(string state, CancellationToken ct)
        {
            return await _dbContext.Set<OAuthState>().FirstOrDefaultAsync(s => s.State == state, ct);
        }
    }
}
