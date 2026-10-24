using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Repositories
{
    public class ReportProfileRepository : IReportProfileRepository
    {
        private readonly AccountingDbContext _db;

        public ReportProfileRepository(AccountingDbContext db) => _db = db;

        public async Task<ReportProfile?> GetAsync(CancellationToken ct = default) =>
            await _db.ReportProfiles.FirstOrDefaultAsync(ct);

        public async Task AddAsync(ReportProfile profile, CancellationToken ct = default) =>
            await _db.ReportProfiles.AddAsync(profile, ct);
    }
}
