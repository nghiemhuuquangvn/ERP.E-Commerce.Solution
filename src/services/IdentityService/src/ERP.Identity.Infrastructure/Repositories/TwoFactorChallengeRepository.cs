using ERP.Identity.Domain.Entities;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Infrastructure.Persistence;

namespace ERP.Identity.Infrastructure.Repositories
{
    public class TwoFactorChallengeRepository : GenericRepository<TwoFactorChallenge>, ITwoFactorChallengeRepository
    {
        public TwoFactorChallengeRepository(AuthDbContext dbContext) : base(dbContext)
        {
        }
    }
}
