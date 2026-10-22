using System.Reflection;
using System.Text.Json;
using ERP.Shared.Authorization.ServiceAuth;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Service-to-service endpoints (2026-10-21) live under <c>internal/</c>, accept only service
/// tokens ([AuthorizeInternalService]) and are never routed by the Gateway. A service-only
/// endpoint under <c>api/</c> (anonymous "because only another service calls it") is exactly
/// what this replaced.
/// </summary>
public class InternalEndpointTests
{
    private static IEnumerable<string> Routes(Type controller, MethodInfo action)
    {
        var prefixes = controller.GetCustomAttributes<RouteAttribute>(inherit: true).Select(r => r.Template).DefaultIfEmpty("");
        var templates = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Select(h => h.Template ?? "");
        return prefixes.SelectMany(prefix => templates.Select(t =>
            t.StartsWith('/') || t.StartsWith("~/") ? t.TrimStart('~', '/') : $"{prefix?.Trim('/')}/{t}".Trim('/')));
    }

    private static bool IsInternal(string route) => route.StartsWith("internal/", StringComparison.OrdinalIgnoreCase);

    private static bool Has<T>(MemberInfo member) where T : Attribute => member.GetCustomAttributes<T>(inherit: true).Any();

    [Fact]
    public void InternalRoutes_AndInternalServiceAuthorization_GoTogether()
    {
        var offenders = EndpointAuthorizationTests.Actions()
            .SelectMany(x => Routes(x.Controller, x.Action).Select(route => (x.Controller, x.Action, Route: route)))
            .Where(x =>
            {
                var isInternalOnly = Has<AuthorizeInternalServiceAttribute>(x.Action) || Has<AuthorizeInternalServiceAttribute>(x.Controller);
                var anonymous = Has<AllowAnonymousAttribute>(x.Action) || Has<AllowAnonymousAttribute>(x.Controller);
                return IsInternal(x.Route) != isInternalOnly || (isInternalOnly && anonymous);
            })
            .Select(x => $"{x.Controller.FullName}.{x.Action.Name} → {x.Route}")
            .ToList();

        offenders.Should().BeEmpty(
            "routes under internal/ need [AuthorizeInternalService] (and no [AllowAnonymous]); [AuthorizeInternalService] belongs on internal/ routes only:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Gateway_NeverRoutesToInternalEndpoints()
    {
        var ocelot = Path.Combine(RepositoryRoot(), "src", "Gateway", "Ocelot.API", "ocelot.json");
        using var document = JsonDocument.Parse(File.ReadAllText(ocelot));

        var downstream = document.RootElement.GetProperty("Routes").EnumerateArray()
            .Select(r => r.GetProperty("DownstreamPathTemplate").GetString() ?? "")
            .ToList();

        downstream.Should().NotBeEmpty();
        downstream.Should().NotContain(t => t.StartsWith("/internal", StringComparison.OrdinalIgnoreCase) || t.StartsWith("/{"),
            "a route to /internal (or a catch-all) would expose service-to-service endpoints publicly");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (docker-compose.yml) not found.");
    }
}
