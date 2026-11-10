using ERP.MFG.Domain.Common;
using ERP.MFG.Infrastructure.Persistence;
using ERP.MFG.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ERP.MFG.UnitTests.Common;

/// <summary>General rules of the module (BA spec §5.1) that no feature test names: concurrency answers 409 (the API-shape rules TC-GEN-002 / 008 are in ERP.Architecture.Tests).</summary>
public class GeneralRulesTests
{
    /// <summary>A context whose save always fails like PostgreSQL does when the xmin of a row moved on.</summary>
    private sealed class ConflictingContext(DbContextOptions<MfgDbContext> options, ITenantProvider tenants) : MfgDbContext(options, tenants)
    {
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
            => throw new DbUpdateConcurrencyException("the row was changed by another session");
    }

    [Fact] // TC-GEN-005 (BR-GEN-03): the second of two sessions that change the same row is answered with the conflict, not a 500
    public async Task TC_GEN_005_A_Concurrent_Update_Is_A_Conflict_With_The_Gen001_Text()
    {
        var tenants = new Mock<ITenantProvider>();
        tenants.SetupGet(t => t.CurrentTenantId).Returns(Guid.NewGuid());
        await using var db = new ConflictingContext(new DbContextOptionsBuilder<MfgDbContext>().UseInMemoryDatabase("gen-005-" + Guid.NewGuid()).Options, tenants.Object);

        var act = () => new MfgUnitOfWork(db).SaveChangesAsync();

        var refusal = (await act.Should().ThrowAsync<ConcurrencyException>()).Which;
        refusal.Message.Should().Be(MfgErrors.Format(MfgErrors.Gen001));
        refusal.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
    }
}
