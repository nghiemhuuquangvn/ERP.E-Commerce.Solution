using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// SEC-H2 (security review 2026-11): <c>Tenancy:FallbackTenantCode</c> turns every unknown host into that tenant, and
/// the opt-in <c>Tenancy:AllowFallbackTenantOutsideDevelopment</c> lets IdentityService start with it outside
/// Development. Both belong to the single-machine docker-compose stack only; no manifest under <c>deployment/</c>
/// (Kubernetes, LAN test, ...) may set either.
/// </summary>
public class NoTenantFallbackInDeploymentTests
{
    private static readonly string[] ForbiddenSettings =
    [
        "FallbackTenantCode:", "FallbackTenantCode=", "FallbackTenantCode\":",
        "AllowFallbackTenantOutsideDevelopment",
    ];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (docker-compose.yml) not found.");
    }

    [Fact]
    public void NoDeploymentManifestSetsAFallbackTenant()
    {
        var root = RepositoryRoot();
        var offenders = Directory.EnumerateFiles(Path.Combine(root, "deployment"), "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".yml") || f.EndsWith(".yaml") || f.EndsWith(".env") || f.EndsWith(".json") || f.EndsWith(".example"))
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or "node_modules"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(x => !x.Text.TrimStart().StartsWith('#'))
            .Where(x => ForbiddenSettings.Any(s => x.Text.Contains(s, StringComparison.Ordinal)))
            .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}: {x.Text.Trim()}")
            .ToList();

        offenders.Should().BeEmpty("an unknown host must answer 404 in every shared deployment:\n" + string.Join("\n", offenders));
    }
}
