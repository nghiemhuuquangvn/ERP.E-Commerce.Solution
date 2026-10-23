using FluentAssertions;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Swagger / OpenAPI lists every endpoint, parameter and model of a service. Compose and Kubernetes run every service
/// as Production, so it must be switched on only when the environment is Development (ROADMAP P0-8).
/// A call counts as guarded when the closest preceding code lines contain <c>IsDevelopment()</c>, i.e. the shape
/// <c>if (app.Environment.IsDevelopment()) { app.UseSwagger(); … }</c>.
/// </summary>
public class SwaggerOnlyInDevelopmentTests
{
    private static readonly string[] DocumentationCalls = ["UseSwagger(", "UseSwaggerUI(", "MapOpenApi(", "MapSwagger("];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (docker-compose.yml) not found.");
    }

    private static IEnumerable<string> ProgramFiles(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "src"), "Program.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or "node_modules"));

    [Fact]
    public void EveryServiceExposesApiDocumentationOnlyInDevelopment()
    {
        var root = RepositoryRoot();
        var found = 0;
        var offenders = new List<string>();

        foreach (var file in ProgramFiles(root))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i].TrimStart();
                if (code.StartsWith("//") || !DocumentationCalls.Any(c => code.Contains("app." + c) || code.Contains("." + c)))
                    continue;

                found++;
                var preceding = lines.Take(i).Reverse().Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("//")).Take(3);
                if (!preceding.Any(l => l.Contains("IsDevelopment()")))
                    offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}  {code}");
            }
        }

        found.Should().BeGreaterThan(10, "the scan must actually see the services' Swagger calls, or it proves nothing");
        offenders.Should().BeEmpty("API documentation must sit inside if (app.Environment.IsDevelopment()):\n" + string.Join("\n", offenders));
    }
}
