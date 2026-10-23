using System.Net;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

namespace Ocelot.API.RateLimiting;

/// <summary>Which limit a request counts against.</summary>
public enum RateLimitClass
{
    /// <summary>Sign-in, sign-up, password reset, token refresh, 2FA, external login, /connect/token.</summary>
    Auth,

    /// <summary>Anonymous chat start / messages.</summary>
    Chat,
}

public sealed class GatewayRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>Requests per client IP per minute against the auth endpoints.</summary>
    public int AuthPermitPerMinute { get; set; } = 30;

    /// <summary>Requests per client IP per minute against the chat endpoints.</summary>
    public int ChatPermitPerMinute { get; set; } = 30;
}

/// <summary>
/// Outer, coarse brute-force / abuse limit in front of Ocelot. IdentityService keeps its own finer limits (10 sign-ins and
/// 5 2FA checks per minute per IP) and the per-account lockout; this layer adds what they cannot see: endpoints Identity does
/// not limit (refresh, /connect/token, confirm-email), the anonymous chatbot, and traffic that Identity must not even have to
/// process. Counters live in memory, so with several Gateway replicas each one counts for itself.
/// </summary>
public sealed class GatewayRateLimiter : IDisposable
{
    // Identity route templates are "/api/v1/auth/{everything}" and "/id/api/{everything}" (the SPAs call both forms).
    private static readonly Regex AuthPath = new(
        @"^(/id)?/api/v\d+/auth/(login|register|tenant-signup|forgot-password|reset-password|refresh|confirm-email|external-login|2fa)(/|$)|^/connect/(token|authorize)(/|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ChatPath = new(@"^/api/v\d+/chatbot(/|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IPNetwork[] _trustedProxies;
    private readonly PartitionedRateLimiter<HttpContext> _limiter;

    public GatewayRateLimiter(GatewayRateLimitOptions options, IEnumerable<string> trustedNetworks)
    {
        Options = options;
        _trustedProxies = trustedNetworks.Select(IPNetwork.Parse).ToArray();
        _limiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var cls = Classify(context.Request);
            if (cls is null || !options.Enabled)
                return RateLimitPartition.GetNoLimiter("none");

            var permits = cls == RateLimitClass.Auth ? options.AuthPermitPerMinute : options.ChatPermitPerMinute;
            return RateLimitPartition.GetFixedWindowLimiter($"{cls}:{ClientIp(context)}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permits),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
        });
    }

    public GatewayRateLimitOptions Options { get; }

    public RateLimitLease Acquire(HttpContext context) => _limiter.AttemptAcquire(context);

    public static RateLimitClass? Classify(HttpRequest request)
    {
        // CORS preflights carry no credentials and are answered before any controller runs.
        if (HttpMethods.IsOptions(request.Method))
            return null;

        var path = request.Path.Value ?? string.Empty;
        if (AuthPath.IsMatch(path))
            return RateLimitClass.Auth;
        if (!HttpMethods.IsGet(request.Method) && ChatPath.IsMatch(path))
            return RateLimitClass.Chat;
        return null;
    }

    /// <summary>
    /// The caller's address without changing the request (downstream services read X-Forwarded-For themselves).
    /// Walks X-Forwarded-For from the right, skipping hops that are our own trusted proxies (nginx, the SPA nginx, ingress):
    /// the first address that is not one of them is the client. Entries further left are client-supplied and ignored. A
    /// request that did not come through a trusted proxy ignores the header altogether, so it cannot pick its own address.
    /// </summary>
    public string ClientIp(HttpContext context)
    {
        var peer = Normalize(context.Connection.RemoteIpAddress);
        if (peer is null)
            return "unknown";
        if (!IsTrusted(peer) || !context.Request.Headers.TryGetValue("X-Forwarded-For", out var header))
            return peer.ToString();

        var hops = header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IPAddress? client = null;
        for (var i = hops.Length - 1; i >= 0; i--)
        {
            if (!IPAddress.TryParse(hops[i], out var hop))
                break; // garbage from here left is not an address we can reason about
            client = Normalize(hop);
            if (!IsTrusted(client!))
                break;
        }

        return (client ?? peer).ToString();
    }

    private bool IsTrusted(IPAddress address) => _trustedProxies.Any(n => n.Contains(address));

    private static IPAddress? Normalize(IPAddress? address) =>
        address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4() : address;

    public void Dispose() => _limiter.Dispose();
}

public sealed class GatewayRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly GatewayRateLimiter _limiter;
    private readonly ILogger<GatewayRateLimitMiddleware> _logger;

    public GatewayRateLimitMiddleware(RequestDelegate next, GatewayRateLimiter limiter, ILogger<GatewayRateLimitMiddleware> logger)
    {
        _next = next;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var lease = _limiter.Acquire(context);
        if (lease.IsAcquired)
        {
            await _next(context);
            return;
        }

        var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var delay) ? (int)Math.Ceiling(delay.TotalSeconds) : 60;
        _logger.LogWarning("Rate limit exceeded for {Client} on {Method} {Path}", _limiter.ClientIp(context), context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = retryAfter.ToString();
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"title\":\"Too many requests\",\"status\":429}");
    }
}

public static class GatewayRateLimitingExtensions
{
    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(GatewayRateLimitOptions.SectionName).Get<GatewayRateLimitOptions>() ?? new GatewayRateLimitOptions();
        // Same setting Identity trusts for X-Forwarded-For; default: only a local proxy.
        var trusted = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? ["127.0.0.0/8", "::1/128"];
        return services.AddSingleton(new GatewayRateLimiter(options, trusted));
    }

    public static IApplicationBuilder UseGatewayRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<GatewayRateLimitMiddleware>();
}
