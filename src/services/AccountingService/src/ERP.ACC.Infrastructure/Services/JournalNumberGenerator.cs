using ERP.ACC.Application.Posting;
using ERP.ACC.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ERP.ACC.Infrastructure.Services
{
    /// <summary>
    /// Atomically increments the tenant's counter for the year with one MERGE ... OUTPUT (a read-then-update would hand the same
    /// number to two concurrent postings) and formats it as "JE-2026-000001". The increment commits on its own, so a posting
    /// that is rolled back leaves a gap in the numbers; the entries themselves stay balanced.
    /// </summary>
    public class JournalNumberGenerator : IJournalNumberGenerator
    {
        private readonly AccountingDbContext _db;

        public JournalNumberGenerator(AccountingDbContext db) => _db = db;

        public async Task<string> NextAsync(Guid tenantId, int year, CancellationToken ct = default)
        {
            // ToListAsync, not SingleAsync: Single wraps the statement in TOP(2) and MERGE ... OUTPUT cannot be composed over.
            var values = await _db.Database.SqlQueryRaw<int>(
                @"MERGE JournalNumberSequences AS target
                  USING (SELECT @tenantId AS TenantId, @year AS [Year]) AS src
                  ON target.TenantId = src.TenantId AND target.[Year] = src.[Year]
                  WHEN MATCHED THEN UPDATE SET LastValue = target.LastValue + 1
                  WHEN NOT MATCHED THEN INSERT (TenantId, [Year], LastValue) VALUES (src.TenantId, src.[Year], 1)
                  OUTPUT inserted.LastValue;",
                new SqlParameter("@tenantId", tenantId),
                new SqlParameter("@year", year))
                .ToListAsync(ct);

            return $"JE-{year}-{values.Single():D6}";
        }
    }
}
