using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// There is no shared symmetric token key (2026-10-21): user tokens are RS256 signed only by
/// IdentityService, service-to-service tokens RS256 signed by each caller's own key, OpenIddict
/// tokens encrypted with a certificate. A <c>Jwt:Key</c> creeping back into configuration would
/// re-introduce a secret every service holds — fail the build instead.
/// </summary>
public class NoSharedTokenKeyTests
{
    private static readonly string[] ForbiddenSettings = ["Jwt__Key", "JWT_SIGNING_KEY", "jwt-signing-key"];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (docker-compose.yml) not found.");
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "node_modules");

    [Fact]
    public void NoAppSettingsFileHasAJwtKey()
    {
        var root = RepositoryRoot();
        var offenders = Directory.EnumerateFiles(Path.Combine(root, "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .Where(f =>
            {
                using var json = JsonDocument.Parse(File.ReadAllText(f), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                return json.RootElement.TryGetProperty("Jwt", out var jwt)
                       && jwt.ValueKind == JsonValueKind.Object
                       && jwt.EnumerateObject().Any(p => string.Equals(p.Name, "Key", StringComparison.OrdinalIgnoreCase));
            })
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        offenders.Should().BeEmpty("Jwt:Key no longer exists — tokens are signed with certificates:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void NoDeploymentConfigurationSetsASharedTokenKey()
    {
        var root = RepositoryRoot();
        var files = new[] { "docker-compose.yml", "docker-compose.debug.yml", ".env.example" }
            .Select(f => Path.Combine(root, f))
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "deployment"), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".yml") || f.EndsWith(".yaml") || f.EndsWith(".example") || f.EndsWith(".env")))
            .Where(File.Exists)
            .Where(f => !IsBuildOutput(f));

        var offenders = files
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(x => ForbiddenSettings.Any(s => x.Text.Contains(s, StringComparison.Ordinal)))
            .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}: {x.Text.Trim()}")
            .ToList();

        offenders.Should().BeEmpty("no deployment may configure a shared JWT key:\n" + string.Join("\n", offenders));
    }
}
