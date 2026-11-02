using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// R99 / R100 (2026-11-02): an expected refusal ("not in Pending status", "already exists") must reach the client as 400 / 404 / 409, never as a
/// server error. The central mapping now turns an InvalidOperationException of our own code into a 400, but new code should say what it means:
/// <c>BusinessRuleException</c>, <c>DuplicateException</c>, <c>EntityNotFoundException</c>. The old throws are counted per file in
/// <c>RefusalBaseline.txt</c>; a count may only go down.
/// </summary>
public class RefusalExceptionTests
{
    private static readonly Regex Throw = new(@"throw new (InvalidOperationException|ArgumentException)\(", RegexOptions.Compiled);
    private static readonly string[] SkippedFolders = ["obj", "bin", "Migrations"];

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docker-compose.yml"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static IEnumerable<string> Sources(string root, string pattern) =>
        Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(part => SkippedFolders.Contains(part)));

    private static Dictionary<string, int> Current()
    {
        var services = Path.Combine(RepositoryRoot(), "src", "services");
        return Sources(services, "*.cs")
            .Select(f => (Path: Path.GetRelativePath(services, f).Replace('\\', '/'), Count: Throw.Matches(File.ReadAllText(f)).Count))
            .Where(x => x.Count > 0)
            .ToDictionary(x => x.Path, x => x.Count);
    }

    private static Dictionary<string, int> Baseline() =>
        File.ReadAllLines(Path.Combine(RepositoryRoot(), "tests", "unit", "ERP.Architecture.Tests", "RefusalBaseline.txt"))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('|'))
            .ToDictionary(p => p[0], p => int.Parse(p[1]));

    [Fact]
    public void NoFileThrowsMoreInvalidOperationExceptionsThanBefore()
    {
        var baseline = Baseline();
        var offenders = Current()
            .Where(c => !baseline.TryGetValue(c.Key, out var allowed) || c.Value > allowed)
            .Select(c => $"{c.Key}: {c.Value} (allowed {(baseline.TryGetValue(c.Key, out var a) ? a : 0)})")
            .ToList();

        offenders.Should().BeEmpty("a refusal is a BusinessRuleException (400), DuplicateException (409) or EntityNotFoundException (404):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheBaselineShrinksWhenCodeIsTidied()
    {
        var current = Current();
        var stale = Baseline()
            .Where(b => !current.TryGetValue(b.Key, out var now) || now < b.Value)
            .Select(b => $"{b.Key}: baseline {b.Value}, now {(current.TryGetValue(b.Key, out var n) ? n : 0)}")
            .ToList();

        stale.Should().BeEmpty("lower the counts in RefusalBaseline.txt so the ratchet keeps the gain:\n" + string.Join("\n", stale));
    }

    [Fact]
    public void RequestLoggingIsRegisteredOutsideTheExceptionHandler()
    {
        var services = Path.Combine(RepositoryRoot(), "src", "services");
        var offenders = Sources(services, "Program.cs")
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            .Where(x => x.Text.Contains("UseSerilogRequestLogging(") && x.Text.Contains("UseGlobalExceptionHandling("))
            .Where(x => x.Text.IndexOf("UseSerilogRequestLogging(", StringComparison.Ordinal) > x.Text.IndexOf("UseGlobalExceptionHandling(", StringComparison.Ordinal))
            .Select(x => Path.GetRelativePath(services, x.File))
            .ToList();

        offenders.Should().BeEmpty("UseSerilogRequestLogging must come first, or a handled refusal is logged as 'responded 500' (R100):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryServiceLogsItsRequestsBeforeTheExceptionHandler()
    {
        var services = Path.Combine(RepositoryRoot(), "src", "services");
        var offenders = Sources(services, "Program.cs")
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            .Where(x => x.Text.Contains("UseGlobalExceptionHandling("))
            .Where(x =>
            {
                var log = new[] { "UseErpRequestLogging(", "UseSerilogRequestLogging(" }
                    .Select(k => x.Text.IndexOf(k, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(-1).Min();
                return log < 0 || log > x.Text.IndexOf("UseGlobalExceptionHandling(", StringComparison.Ordinal);
            })
            .Select(x => Path.GetRelativePath(services, x.File))
            .ToList();

        offenders.Should().BeEmpty("a service without a request log leaves nothing to alert on (R101): call app.UseErpRequestLogging() before UseGlobalExceptionHandling():\n" + string.Join("\n", offenders));
    }

    // A configuration fault is not a refusal: with a plain InvalidOperationException the central rule would answer 400 and show the key name.
    private static readonly Regex ConfigurationThrow = new(
        @"throw new (InvalidOperationException|ArgumentException)\(\s*\$?""[^""]*onfigur", RegexOptions.Compiled);

    [Fact]
    public void ConfigurationFaultsAreNotRefusals()
    {
        var root = RepositoryRoot();
        var offenders = Sources(Path.Combine(root, "src", "services"), "*.cs").Concat(Sources(Path.Combine(root, "src", "Shared"), "*.cs"))
            .Where(f => ConfigurationThrow.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .ToList();

        offenders.Should().BeEmpty("throw ServiceMisconfiguredException (500, message kept in the log) for a missing configuration value (R101):\n" + string.Join("\n", offenders));
    }
}
