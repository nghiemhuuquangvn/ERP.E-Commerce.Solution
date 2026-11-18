using ERP.Shared.Authorization.TokenRevocation;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ERP.Shared.UnitTests.Authorization;

/// <summary>S4a / SEC-M8: a service outside Development never runs without the shared revocation store by accident.</summary>
public class RevocationStoreGuardTests
{
    private static IConfiguration Config(string? environment, string? allowInMemory = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = environment,
            [TokenRevocationServiceCollectionExtensions.AllowInMemoryKey] = allowInMemory,
        }).Build();

    [Fact]
    public void Production_WithoutRedis_RefusesToStart()
    {
        var act = () => TokenRevocationServiceCollectionExtensions.EnsureSharedStoreOutsideDevelopment(Config("Production"), null);

        act.Should().Throw<ServiceMisconfiguredException>().WithMessage("*ConnectionStrings:Redis*");
    }

    [Theory]
    [InlineData("Production", "redis:6379,password=x", null)]   // configured
    [InlineData("Development", null, null)]                      // local development
    [InlineData(null, null, null)]                               // no environment named (unit tests, tools)
    [InlineData("Production", null, "true")]                     // single process on purpose
    public void AllowedSetups_Start(string? environment, string? redis, string? allowInMemory)
    {
        var act = () => TokenRevocationServiceCollectionExtensions.EnsureSharedStoreOutsideDevelopment(Config(environment, allowInMemory), redis);

        act.Should().NotThrow();
    }
}
