using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// Risk R14 / R107: the storefront and the mobile apps called order / product / payment / stock / shipment routes that did not exist,
/// had other field names, or were not routed by the Gateway, and nothing noticed. This guard reads every
/// <c>apiClient.get|post|put|patch|delete('/v1/...')</c> call in the storefront, the customer app and the staff app and requires
/// (1) a controller action with that HTTP method and route and (2) a Gateway route (ocelot.json) that carries it. (Field names are
/// not checked here; they are covered by the typed clients and the live checks.)
/// </summary>
public class ClientApiContractTests
{
    private static readonly string[] ClientRoots =
    [
        Path.Combine("src", "frontend", "storefront", "src"),
        Path.Combine("src", "frontend", "mobile", "customer"),
        Path.Combine("src", "frontend", "mobile", "staff"),
    ];

    // A client call the Gateway does not route yet. The list may only shrink: fix the route, then delete the entry (DEFERRED R110).
    private static readonly string[] GatewayKnownGaps = [];

    // apiClient.post<Foo>(`/v1/orders/${id}/checkout`  |  apiClient.get('/v1/x'
    private static readonly Regex Call = new(
        @"apiClient\s*\.\s*(?<verb>get|post|put|patch|delete)\s*(?:<(?:[^<>]|<[^<>]*>)*>)?\s*\(\s*(?<q>[`'""])(?<path>.*?)\k<q>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private record Route(string Verb, string[] Segments);

    [Fact]
    public void EveryRouteTheCustomerAppsCallExistsOnABackendController()
    {
        var routes = BackendRoutes();
        var offenders = new SortedSet<string>();

        foreach (var (file, verb, path) in ClientCalls())
        {
            var segments = Normalize(path);
            if (!routes.Any(r => r.Verb == verb && Matches(r.Segments, segments)))
                offenders.Add($"{verb.ToUpperInvariant()} {path}   ({Path.GetFileName(file)})");
        }

        offenders.Should().BeEmpty("a client calls a route no controller serves (or with another HTTP method):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryRouteTheClientsCallIsRoutedByTheGateway()
    {
        var gateway = GatewayRoutes();
        var offenders = new SortedSet<string>();

        foreach (var (file, verb, path) in ClientCalls())
        {
            var segments = Normalize(path);
            var key = $"{verb.ToUpperInvariant()} {path}";
            if (GatewayKnownGaps.Contains(key)) continue;
            if (!gateway.Any(r => r.Verbs.Contains(verb) && GatewayMatches(r.Segments, segments)))
                offenders.Add($"{key}   ({Path.GetFileName(file)})");
        }

        offenders.Should().BeEmpty("a client calls a route the Gateway (ocelot.json) does not carry, so the phone / browser gets a 404:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheGuardSeesTheCallsAndRoutesItChecks()
    {
        ClientCalls().Count().Should().BeGreaterThan(15, "the scan must find the apps' API calls, or the guard checks nothing");
        ClientCalls().Count(c => c.File.Contains(Path.Combine("mobile", "staff"))).Should().BeGreaterThan(5, "the staff app must be scanned");
        BackendRoutes().Count.Should().BeGreaterThan(200);
        BackendRoutes().Should().Contain(r => r.Verb == "post" && Matches(r.Segments, Normalize("/v1/orders")));
        GatewayRoutes().Count.Should().BeGreaterThan(40);
        // R107: "/api/v1/shipments/{everything}" alone does not carry the list call; the plain route must exist.
        GatewayRoutes().Any(r => r.Verbs.Contains("get") && GatewayMatches(r.Segments, Normalize("/v1/shipments"))).Should().BeTrue();
        GatewayMatches(Normalize("/api/v1/shipments/{everything}".Replace("/api/", "/v1/")), Normalize("/v1/shipments")).Should().BeFalse("a catch-all needs at least one more segment, like Ocelot");
    }

    private record GatewayRoute(string[] Segments, HashSet<string> Verbs);

    private static List<GatewayRoute> GatewayRoutes()
    {
        var file = Path.Combine(RepositoryRoot(), "src", "Gateway", "Ocelot.API", "ocelot.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var routes = new List<GatewayRoute>();
        foreach (var route in doc.RootElement.GetProperty("Routes").EnumerateArray())
        {
            var template = route.GetProperty("UpstreamPathTemplate").GetString() ?? "";
            var verbs = route.GetProperty("UpstreamHttpMethod").EnumerateArray().Select(v => v.GetString()!.ToLowerInvariant()).ToHashSet();
            routes.Add(new GatewayRoute(template.Split('?')[0].Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries), verbs));
        }
        return routes;
    }

    /// <summary>Ocelot: a {name} placeholder is one segment; a {everything} placeholder at the end takes one or more segments.</summary>
    private static bool GatewayMatches(string[] route, string[] call)
    {
        for (var i = 0; i < route.Length; i++)
        {
            if (i >= call.Length) return false;
            var r = route[i];
            if (r.Equals("{everything}", StringComparison.OrdinalIgnoreCase) && i == route.Length - 1) return true;
            var routeParam = r.StartsWith('{');
            var callParam = call[i].Contains("${") || call[i].StartsWith('{');
            if (routeParam || callParam) continue;
            if (!string.Equals(r, call[i], StringComparison.OrdinalIgnoreCase)) return false;
        }
        return route.Length == call.Length;
    }

    private static IEnumerable<(string File, string Verb, string Path)> ClientCalls()
    {
        var root = RepositoryRoot();
        foreach (var clientRoot in ClientRoots)
        {
            var dir = Path.Combine(root, clientRoot);
            if (!Directory.Exists(dir)) continue;
            var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => (f.EndsWith(".ts") || f.EndsWith(".tsx") || f.EndsWith(".vue"))
                            && !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}")
                            && !f.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}"));
            foreach (var file in files)
                foreach (Match m in Call.Matches(File.ReadAllText(file)))
                    if (m.Groups["path"].Value.StartsWith("/v1/"))
                        yield return (file, m.Groups["verb"].Value.ToLowerInvariant(), m.Groups["path"].Value);
        }
    }

    private static List<Route> BackendRoutes()
    {
        var routes = new List<Route>();
        foreach (var (controller, action) in EndpointAuthorizationTests.Actions())
        {
            var prefix = controller.GetCustomAttributes<RouteAttribute>(inherit: true).FirstOrDefault()?.Template ?? "";
            prefix = prefix.Replace("[controller]", controller.Name.Replace("Controller", ""), StringComparison.OrdinalIgnoreCase);
            foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
            {
                var template = http.Template ?? "";
                var full = template.StartsWith('/') ? template : $"{prefix}/{template}";
                foreach (var verb in http.HttpMethods)
                    routes.Add(new Route(verb.ToLowerInvariant(), Normalize(full.Replace("api/v{version:apiVersion}", "api/v1"))));
            }
        }
        return routes;
    }

    /// <summary>Segments without query string; the client's /v1/... gets the /api prefix every route has.</summary>
    private static string[] Normalize(string path)
    {
        var p = path.Split('?')[0].Trim('/');
        if (p.StartsWith("v1/")) p = "api/" + p;
        return p.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool Matches(string[] route, string[] call)
    {
        if (route.Length != call.Length) return false;
        for (var i = 0; i < route.Length; i++)
        {
            var r = route[i];
            var c = call[i];
            var routeParam = r.StartsWith('{');
            var callParam = c.Contains("${") || c.StartsWith('{');
            if (routeParam || callParam) continue;
            if (!string.Equals(r, c, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (docker-compose.yml) not found.");
    }
}
