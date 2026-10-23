using ERP.SO.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace ERP.SO.Infrastructure.Services;

/// <summary>
/// Atomically increments the per-tenant/per-month counter in OrderNumberSequences via a
/// single MERGE...OUTPUT statement (avoids the read-then-update race a plain
/// SELECT + UPDATE would have under concurrent order creation) and formats the result as
/// "{prefix}-{yyyyMM}-{00001}".
/// </summary>
public class OrderNumberGenerator : IOrderNumberGenerator
{
    private readonly SalesOrderDbContext _context;

    public OrderNumberGenerator(SalesOrderDbContext context)
    {
        _context = context;
    }

    public async Task<string> NextAsync(Guid tenantId, string prefix, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // ToListAsync, not SingleAsync: Single adds TOP(2) around the statement, and MERGE ... OUTPUT cannot be
        // composed over ("'FromSql' or 'SqlQuery' was called with non-composable SQL"), so no order could be created.
        var values = await _context.Database.SqlQueryRaw<int>(
            @"MERGE OrderNumberSequences AS target
              USING (SELECT @tenantId AS TenantId, @prefix AS Prefix, @year AS Year, @month AS Month) AS src
              ON target.TenantId = src.TenantId AND target.Prefix = src.Prefix AND target.Year = src.Year AND target.Month = src.Month
              WHEN MATCHED THEN UPDATE SET LastValue = target.LastValue + 1
              WHEN NOT MATCHED THEN INSERT (Id, TenantId, Prefix, Year, Month, LastValue) VALUES (NEWID(), src.TenantId, src.Prefix, src.Year, src.Month, 1)
              OUTPUT inserted.LastValue;",
            new SqlParameter("@tenantId", tenantId),
            new SqlParameter("@prefix", prefix),
            new SqlParameter("@year", now.Year),
            new SqlParameter("@month", now.Month))
            .ToListAsync(ct);

        return $"{prefix}-{now:yyyyMM}-{values.Single():D5}";
    }
}
