using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Risk R10: a fresh database built with <c>src/Database/RunAllScripts.ps1</c> silently missed columns whenever someone added a
/// migration and forgot the runner (Warehouse 007-008, Purchasing 009-013, Payment 006-008 ... and 29 older files were found
/// unlisted). The Docker path (<c>deployment/docker/run-migrations.sh</c>) globs the folders and cannot drift; this keeps the
/// PowerShell runner honest: every .sql file is listed, every listed file exists, and the migrations of one service run in order.
/// </summary>
public class DatabaseRunnerTests
{
    private static readonly Regex Entry = new("\"(?<path>[A-Za-z]+\\\\[^\"]+\\.sql)\"", RegexOptions.Compiled);

    private static string DatabaseRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root (docker-compose.yml) not found."), "src", "Database");
    }

    private static List<string> Listed() =>
        File.ReadAllLines(Path.Combine(DatabaseRoot(), "RunAllScripts.ps1"))
            .Where(l => !l.TrimStart().StartsWith('#'))
            .Select(l => Entry.Match(l))
            .Where(m => m.Success)
            .Select(m => m.Groups["path"].Value)
            .ToList();

    private static List<string> OnDisk() =>
        Directory.GetDirectories(DatabaseRoot())
            .SelectMany(d => Directory.GetFiles(d, "*.sql").Select(f => Path.GetFileName(d) + "\\" + Path.GetFileName(f)))
            .ToList();

    [Fact]
    public void EveryMigrationFileIsInTheRunner()
    {
        var missing = OnDisk().Except(Listed(), StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

        missing.Should().BeEmpty("add these files to src/Database/RunAllScripts.ps1 (after the service's previous migration):\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryFileTheRunnerListsExists()
    {
        var gone = Listed().Except(OnDisk(), StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

        gone.Should().BeEmpty("the runner would stop on these:\n" + string.Join("\n", gone));
        Listed().Should().OnlyHaveUniqueItems("a script listed twice runs twice");
    }

    [Fact]
    public void TheMigrationsOfOneServiceRunInOrder()
    {
        // demo-data seeds run in a block of their own at the end, after every schema script
        var outOfOrder = Listed()
            .Where(p => !Regex.IsMatch(p, "Seed", RegexOptions.IgnoreCase))
            .GroupBy(p => p.Split('\\')[0])
            .SelectMany(g => g.Zip(g.Skip(1), (a, b) => (a, b)).Where(x => string.CompareOrdinal(x.a, x.b) > 0))
            .Select(x => $"{x.a} listed before {x.b}")
            .ToList();

        outOfOrder.Should().BeEmpty("run a service's schema migrations in file order:\n" + string.Join("\n", outOfOrder));
    }
}
