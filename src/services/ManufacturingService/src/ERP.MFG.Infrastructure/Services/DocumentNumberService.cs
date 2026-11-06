using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Common;
using ERP.MFG.Infrastructure.Persistence;
using ERP.Shared.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ERP.MFG.Infrastructure.Services;

/// <summary>
/// BR-GEN-04. One <c>INSERT ... ON CONFLICT DO UPDATE ... RETURNING</c> statement creates or increments the counter
/// row, so PostgreSQL's row lock serialises parallel callers and every one gets its own value (TC-GEN-006).
/// The statement runs on the DbContext's connection and transaction.
/// </summary>
public sealed class DocumentNumberService(MfgDbContext db, ITenantProvider tenant) : IDocumentNumberService
{
    private const string Sql = """
        INSERT INTO "DocumentSequences"
            ("Id", "TenantId", "IsDeleted", "CreatedAt", "UpdatedAt", "DocType", "PlantId", "Period", "LastValue")
        VALUES (@id, @tenant, false, now(), now(), @type, @plant, @period, 1)
        ON CONFLICT ("TenantId", "DocType", "PlantId", "Period")
        DO UPDATE SET "LastValue" = "DocumentSequences"."LastValue" + 1, "UpdatedAt" = now()
        RETURNING "LastValue" AS "Value"
        """;

    public async Task<string> NextAsync(string docType, Guid plantId, DateTime businessDate, CancellationToken ct = default)
    {
        if (!DocumentTypes.All.Contains(docType))
            throw MfgRules.Fail(MfgErrors.Gen002);
        var tenantId = tenant.CurrentTenantId ?? throw MfgRules.Fail(MfgErrors.Gen002);

        var period = new DateOnly(businessDate.Year, businessDate.Month, 1);
        var next = await db.Database.SqlQueryRaw<long>(Sql,
                new NpgsqlParameter("id", Guid.NewGuid()),
                new NpgsqlParameter("tenant", tenantId),
                new NpgsqlParameter("type", docType),
                new NpgsqlParameter("plant", plantId),
                new NpgsqlParameter("period", period))
            .ToListAsync(ct);
        return DocumentTypes.Format(docType, businessDate, next.Single());
    }
}
