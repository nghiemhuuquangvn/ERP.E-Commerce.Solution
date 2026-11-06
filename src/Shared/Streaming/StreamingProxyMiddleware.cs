using System.Buffers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Shared.Streaming;

/// <summary>One path the Gateway passes through without buffering (the streaming imports and their SSE progress).</summary>
public sealed class StreamingProxyRoute
{
    /// <summary>The path at the Gateway, e.g. <c>/prd/api/v1/products/import</c> (the sub-paths <c>/{id}/events</c>, <c>/template</c> match too).</summary>
    public string UpstreamPrefix { get; set; } = string.Empty;

    /// <summary>Where it goes, e.g. <c>http://productservice:5008/api/v1/products/import</c>.</summary>
    public string DownstreamBase { get; set; } = string.Empty;
}

public sealed class StreamingProxyOptions
{
    public const string SectionName = "StreamingProxy";
    public List<StreamingProxyRoute> Routes { get; set; } = new();
    public long MaxRequestBodyBytes { get; set; } = 30_000_000;
}

/// <summary>
/// A reverse proxy that <b>streams</b> in both directions: the request body is passed on while it is still arriving and every chunk of the answer is
/// flushed to the client as soon as the service writes it. Ocelot cannot do this (it reads the whole downstream answer before it answers, and cuts a
/// long request at its 90 s timeout - measured on 2026-11-06), so these few routes are handled here, before <c>UseOcelot()</c>.
/// <para>Everything the Gateway does for a request stays in front of this middleware: the JWT is validated, the token's tenant must be the host's
/// tenant, the client's X-Tenant-* / X-User-* headers are replaced by the Gateway's own (<c>GatewayAuthorizationMiddleware</c>). This one only
/// refuses an unauthenticated call and forwards the rest. The service still validates the JWT and the permission itself.</para>
/// </summary>
public sealed class StreamingProxyMiddleware
{
    public const string HttpClientName = "streaming-proxy";

    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Connection", "Transfer-Encoding", "Upgrade", "TE", "Trailer", "Host",
    };

    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _clients;
    private readonly StreamingProxyOptions _options;
    private readonly ILogger<StreamingProxyMiddleware> _logger;

    public StreamingProxyMiddleware(RequestDelegate next, IHttpClientFactory clients, IOptions<StreamingProxyOptions> options, ILogger<StreamingProxyMiddleware> logger)
    {
        _next = next;
        _clients = clients;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var route = Match(context.Request.Path.Value ?? string.Empty, out var rest);
        if (route == null)
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Authentication is required." });
            return;
        }

        var ct = context.RequestAborted;
        var hasBody = HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method);
        if (hasBody && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeLimit)
            sizeLimit.MaxRequestBodySize = _options.MaxRequestBodyBytes;

        using var downstream = new HttpRequestMessage(new HttpMethod(context.Request.Method), route.DownstreamBase.TrimEnd('/') + rest + context.Request.QueryString);
        if (hasBody)
        {
            // The body is read from the client and written to the service as one stream: nothing is buffered here.
            downstream.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentLength.HasValue)
                downstream.Content.Headers.ContentLength = context.Request.ContentLength;
        }
        foreach (var header in context.Request.Headers)
        {
            if (HopByHop.Contains(header.Key) || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
            if (!downstream.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                downstream.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        HttpResponseMessage response;
        try
        {
            response = await _clients.CreateClient(HttpClientName).SendAsync(downstream, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (OperationCanceledException)
        {
            return;   // the client went away
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Streaming proxy: {Downstream} is unreachable", route.DownstreamBase);
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new { message = "The service is unavailable." });
            return;
        }

        using (response)
        {
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers.Concat(response.Content.Headers))
            {
                if (HopByHop.Contains(header.Key)) continue;
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            context.Response.Headers["X-Accel-Buffering"] = "no";   // nginx in front: do not buffer this answer
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
            try
            {
                await using var body = await response.Content.ReadAsStreamAsync(ct);
                int read;
                while ((read = await body.ReadAsync(buffer, ct)) > 0)
                {
                    await context.Response.Body.WriteAsync(buffer.AsMemory(0, read), ct);
                    await context.Response.Body.FlushAsync(ct);   // every chunk reaches the client at once (a row line, an SSE event)
                }
            }
            catch (OperationCanceledException)
            {
                // the client closed the connection
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private StreamingProxyRoute? Match(string path, out string rest)
    {
        rest = string.Empty;
        foreach (var route in _options.Routes)
        {
            if (!path.StartsWith(route.UpstreamPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var tail = path[route.UpstreamPrefix.Length..];
            if (tail.Length != 0 && tail[0] != '/') continue;   // "/import-x" is not "/import"
            rest = tail;
            return route;
        }
        return null;
    }
}

public static class StreamingProxyExtensions
{
    /// <summary>Registers the options (section <c>StreamingProxy</c>) and the HTTP client of the streaming proxy (no total timeout: an import may run long).</summary>
    public static IServiceCollection AddStreamingProxy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StreamingProxyOptions>(configuration.GetSection(StreamingProxyOptions.SectionName));
        services.AddHttpClient(StreamingProxyMiddleware.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = System.Net.DecompressionMethods.None,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            });
        return services;
    }

    /// <summary>Place after authentication and the tenant check, before the buffering reverse proxy (Ocelot).</summary>
    public static IApplicationBuilder UseStreamingProxy(this IApplicationBuilder app) => app.UseMiddleware<StreamingProxyMiddleware>();
}
