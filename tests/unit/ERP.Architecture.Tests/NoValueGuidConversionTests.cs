using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// <c>JToken.Value&lt;Guid&gt;(name)</c> / <c>Value&lt;Guid?&gt;(name)</c> throw
/// "Invalid cast from 'System.String' to 'System.Guid'" on a GUID string, which is how every
/// integration event carries its ids. The consumer then drops the whole message. Use
/// <c>EventJson.GuidValue</c> (Shared.Messaging) or <c>ToObject&lt;Guid&gt;()</c> instead.
/// </summary>
public class NoValueGuidConversionTests
{
    private static readonly Regex Forbidden = new(@"\.Value<Guid\??>\(", RegexOptions.Compiled);

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
    public void NoSourceFileReadsAGuidWithJTokenValue()
    {
        var root = RepositoryRoot();
        var offenders = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .Where(f => !f.EndsWith("EventJson.cs", StringComparison.Ordinal)) // documents the pitfall
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(x => Forbidden.IsMatch(x.Text))
            .Select(x => $"{Path.GetRelativePath(root, x.File)}:{x.Line}: {x.Text.Trim()}")
            .ToList();

        offenders.Should().BeEmpty("JToken.Value<Guid> throws on a GUID string — use EventJson.GuidValue:\n" + string.Join("\n", offenders));
    }
}
