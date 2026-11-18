using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Security review 2026-11, wave S4a (docs/architecture/browser-security.md, docs/business/Identity/TokenRevocation.md):
/// no open CORS (SEC-M14), Redis always behind a password (SEC-M9), browser security headers on both SPAs (SEC-M14).
/// </summary>
public class InfrastructureSecurityTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (docker-compose.yml) not found.");
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "bin" or "obj" or "node_modules" or "dist");

    private static readonly Regex OpenCors = new(
        @"AllowAnyOrigin\s*\(|WithOrigins\(\s*""\*""\s*\)|SetIsOriginAllowed\(\s*_\s*=>\s*true\s*\)", RegexOptions.Compiled);

    [Fact]
    public void NoServiceAllowsEveryOrigin()
    {
        var root = RepositoryRoot();
        var offenders = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(x => !x.Text.TrimStart().StartsWith("//") && OpenCors.IsMatch(x.Text))
            .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}: {x.Text.Trim()}")
            .ToList();

        offenders.Should().BeEmpty("use AddErpCors (Cors:AllowedOrigins) - the SPAs call their own host:\n" + string.Join("\n", offenders));
    }

    private static IEnumerable<string> DeploymentFiles(string root) =>
        new[] { "docker-compose.yml", "docker-compose.debug.yml", "docker-compose.localsql.yml" }
            .Select(f => Path.Combine(root, f))
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "deployment"), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".yml") || f.EndsWith(".yaml") || f.EndsWith(".example")))
            .Where(File.Exists)
            .Where(f => !IsBuildOutput(f));

    [Fact]
    public void EveryRedisConnectionCarriesAPassword_AndTheServerRequiresOne()
    {
        var root = RepositoryRoot();
        var lines = DeploymentFiles(root)
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(x => !x.Text.TrimStart().StartsWith('#'))
            .ToList();

        var withoutPassword = lines
            .Where(x => Regex.IsMatch(x.Text, @"(ConnectionStrings__Redis|redis-connection)\s*:") && !x.Text.Contains("password=", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}: {x.Text.Trim()}")
            .ToList();
        withoutPassword.Should().BeEmpty("SEC-M9: a Redis connection without a password:\n" + string.Join("\n", withoutPassword));

        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        compose.Should().MatchRegex(@"redis-server --requirepass \$\{REDIS_PASSWORD", "the compose Redis must require a password");
        File.ReadAllText(Path.Combine(root, "deployment", "k8s", "infrastructure", "redis.yaml"))
            .Should().Contain("--requirepass", "the Kubernetes Redis must require a password");
    }

    private static readonly string[] EnforcedHeaders =
    [
        "X-Content-Type-Options \"nosniff\"",
        "X-Frame-Options \"DENY\"",
        "Referrer-Policy",
        "Permissions-Policy",
        "Content-Security-Policy",
        "frame-ancestors 'none'",
    ];

    [Fact]
    public void BothSpasSendTheBrowserSecurityHeaders()
    {
        var root = RepositoryRoot();
        var admin = Path.Combine(root, "src", "frontend", "admin");
        var storefront = Path.Combine(root, "src", "frontend", "storefront");

        var snippet = File.ReadAllText(Path.Combine(admin, "nginx.security-headers.conf"));
        foreach (var header in EnforcedHeaders)
            snippet.Should().Contain(header);
        snippet.Should().NotContain("Strict-Transport-Security", "HSTS belongs on the TLS ingress, not on the plain-http local nginx");

        File.ReadAllText(Path.Combine(storefront, "nginx.security-headers.conf")).Replace("\r\n", "\n")
            .Should().Be(snippet.Replace("\r\n", "\n"), "both images carry the same headers");

        foreach (var app in new[] { admin, storefront })
        {
            File.ReadAllText(Path.Combine(app, "nginx.docker.conf")).Should().Contain("include /etc/nginx/snippets/security-headers.conf;");
            File.ReadAllText(Path.Combine(app, "Dockerfile")).Should().Contain("nginx.security-headers.conf /etc/nginx/snippets/security-headers.conf");
        }
    }
}
