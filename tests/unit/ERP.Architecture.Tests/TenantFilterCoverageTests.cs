using System.Linq.Expressions;
using System.Reflection;
using ERP.Shared.Authorization;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Builds the real EF model of every tenant-scoped DbContext and checks that EVERY
/// tenant-owned root entity got the tenant query filter — i.e. ApplyTenantQueryFilters ran
/// last, after all entity configuration, and no type slipped through.
/// </summary>
public class TenantFilterCoverageTests
{
    private sealed class FixedTenantProvider(Guid? tenantId) : ITenantProvider
    {
        public Guid? CurrentTenantId => tenantId;
    }

    public static IEnumerable<object[]> ScopedContexts() =>
        TenantIsolationArchitectureTests.AllDbContextTypes()
            .Where(t => typeof(ITenantScopedDbContext).IsAssignableFrom(t))
            .Select(t => new object[] { t.FullName! });

    internal static DbContext Create(Type contextType, Guid? tenantId, string? database = null)
    {
        var options = (DbContextOptions)typeof(TenantFilterCoverageTests)
            .GetMethod(nameof(Options), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(contextType)
            .Invoke(null, [database ?? Guid.NewGuid().ToString()])!;

        var userContext = new Mock<IUserContextService>();
        userContext.Setup(u => u.GetCurrentUser()).Returns(new UserContext { TenantId = tenantId?.ToString() ?? string.Empty });
        userContext.Setup(u => u.GetTenantId()).Returns(tenantId?.ToString() ?? string.Empty);

        var services = new ServiceCollection()
            .AddSingleton<ITenantProvider>(new FixedTenantProvider(tenantId))
            .AddSingleton(userContext.Object)
            .AddSingleton(options.GetType(), options)
            .BuildServiceProvider();

        return (DbContext)ActivatorUtilities.CreateInstance(services, contextType);
    }

    private static DbContextOptions<T> Options<T>(string database) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseInMemoryDatabase(database).Options;

    [Theory]
    [MemberData(nameof(ScopedContexts))]
    public void EveryTenantOwnedEntity_HasTheTenantFilter(string contextName)
    {
        var contextType = TenantIsolationArchitectureTests.AllDbContextTypes().Single(t => t.FullName == contextName);
        using var context = Create(contextType, Guid.NewGuid());

        var unfiltered = context.Model.GetEntityTypes()
            .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType)
                        && !typeof(IPlatformWideEntity).IsAssignableFrom(e.ClrType)
                        && e.BaseType == null && !e.IsOwned())
            .Where(e => !ReferencesCurrentTenant(e.GetQueryFilter()))
            .Select(e => e.ClrType.Name)
            .ToList();

        unfiltered.Should().BeEmpty($"{contextName} must call modelBuilder.ApplyTenantQueryFilters(this) LAST in OnModelCreating");
    }

    private static bool ReferencesCurrentTenant(LambdaExpression? filter)
    {
        if (filter == null)
            return false;
        var finder = new MemberFinder();
        finder.Visit(filter);
        return finder.Found;
    }

    private sealed class MemberFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.Name == nameof(ITenantScopedDbContext.CurrentTenantId))
                Found = true;
            return base.VisitMember(node);
        }
    }
}
