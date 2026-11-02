using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// R84 / R86 / R97 (2026-11-02): an <c>ICacheService</c> key is global, so a handler that caches tenant-owned data under
/// <c>crm:contacts:page:1</c> or <c>wh:barcode:{code}</c> serves one shop's rows to another. A service may write to the cache only
/// (a) in its settings / licence services (their keys carry the tenant id or are platform-wide on purpose), or
/// (b) when the service registers <c>AddTenantKeyedCache()</c>, which puts the tenant in front of every key.
/// </summary>
public class CacheKeyTenantTests
{
    private static readonly Regex Writes = new(@"[cC]ache\.(SetAsync|GetOrSetAsync)\(", RegexOptions.Compiled);
    private static readonly Regex SettingsLike = new(@"(Settings?Services?|LicenseService|FeatureFlagService)\.cs$", RegexOptions.Compiled);

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "services"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static IEnumerable<string> Sources(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    [Fact]
    public void ACacheWrite_IsEitherASettingsKey_OrInAServiceWithATenantKeyedCache()
    {
        var services = Path.Combine(RepositoryRoot(), "src", "services");
        var offenders = new List<string>();

        foreach (var service in Directory.GetDirectories(services))
        {
            var files = Sources(service).ToList();
            var tenantKeyed = files.Any(f => File.ReadAllText(f).Contains("AddTenantKeyedCache("));
            foreach (var file in files.Where(f => Writes.IsMatch(File.ReadAllText(f))))
                if (!tenantKeyed && !SettingsLike.IsMatch(file))
                    offenders.Add(Path.GetRelativePath(services, file));
        }

        offenders.Should().BeEmpty(
            "ICacheService keys are global: cache tenant-owned data only in a service that calls AddTenantKeyedCache() (or do not cache it), "
            + "and keep other caches to settings / licence services whose keys carry the tenant:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheWarehouse_RegistersTheTenantKeyedCache_AfterTheTenantProvider()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "services", "WareHouseService", "src", "ERP.WH.API", "Program.cs"));
        var tenant = program.IndexOf("AddRequestTenantProvider()", StringComparison.Ordinal);
        var cache = program.IndexOf("AddTenantKeyedCache(", StringComparison.Ordinal);

        tenant.Should().BeGreaterThan(-1);
        cache.Should().BeGreaterThan(tenant, "wh:barcode:* and wh:inventory:* are tenant-owned (R86)");
    }
}
