using ERP.Identity.Infrastructure.Services;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ERP.Identity.UnitTests.Security;

/// <summary>SEC-H2: a fallback tenant (unknown host = that tenant) is a local-only switch.</summary>
public class TenancyConfigurationGuardTests
{
    private static IConfiguration Config(string? fallback, string? allow = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [TenancyConfigurationGuard.FallbackKey] = fallback,
            [TenancyConfigurationGuard.AllowKey] = allow,
        }).Build();

    [Fact]
    public void FallbackOutsideDevelopment_WithoutTheOptIn_RefusesToStart()
    {
        var act = () => TenancyConfigurationGuard.EnsureSafe(Config("SYSTEM"), isDevelopment: false);

        act.Should().Throw<ServiceMisconfiguredException>().WithMessage("*Tenancy:FallbackTenantCode*");
    }

    [Theory]
    [InlineData("false")]
    [InlineData("yes")]
    public void FallbackOutsideDevelopment_WithAnythingButTrue_RefusesToStart(string allow)
    {
        var act = () => TenancyConfigurationGuard.EnsureSafe(Config("SYSTEM", allow), isDevelopment: false);

        act.Should().Throw<ServiceMisconfiguredException>();
    }

    [Theory]
    [InlineData(null, null, false)]          // production without a fallback: the normal case
    [InlineData("", null, false)]
    [InlineData("SYSTEM", null, true)]        // Development keeps its fallback (appsettings.Development.json)
    [InlineData("SYSTEM", "true", false)]     // local docker-compose: explicit opt-in
    public void AllowedConfigurations_Start(string? fallback, string? allow, bool isDevelopment)
    {
        var act = () => TenancyConfigurationGuard.EnsureSafe(Config(fallback, allow), isDevelopment);

        act.Should().NotThrow();
    }
}
