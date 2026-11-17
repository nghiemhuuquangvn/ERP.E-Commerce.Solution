using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Identity.Api.Authorization;

/// <summary>
/// Refresh token as an HttpOnly cookie for the web apps (security review 2026-11, SEC-H3; docs/business/Identity/Sessions.md).
/// A browser client asks for it with the header <c>X-Auth-Transport: cookie</c>: the token then never reaches page
/// script (no localStorage copy for an XSS to steal) and the JSON body carries <c>refreshToken: null</c>. Refresh and
/// logout read the cookie ONLY when the same header is present: a custom header forces a CORS preflight, so another
/// site cannot make the browser use the cookie (CSRF), on top of <c>SameSite=Strict</c>. Mobile apps omit the header
/// and keep the token in the body (stored in the device keystore).
/// </summary>
public static class RefreshTokenCookie
{
    public const string CookieName = "erp_rt";
    public const string TransportHeader = "X-Auth-Transport";
    public const string CookieTransport = "cookie";
    public const string AllowInsecureKey = "Auth:RefreshCookie:AllowInsecure";
    public const string PathKey = "Auth:RefreshCookie:Path";
    public const string DefaultPath = "/api/v1/auth";

    public static bool Requested(HttpRequest? request) =>
        request is not null
        && string.Equals(request.Headers[TransportHeader].ToString(), CookieTransport, StringComparison.OrdinalIgnoreCase);

    /// <summary>The refresh token of this request: the cookie in cookie mode, else the body value.</summary>
    public static string? Read(HttpRequest? request, string? bodyValue) =>
        Requested(request) && request!.Cookies.TryGetValue(CookieName, out var cookie) && !string.IsNullOrEmpty(cookie)
            ? cookie
            : bodyValue;

    /// <summary>In cookie mode: sets the cookie and returns null (nothing for the body); otherwise returns the token.</summary>
    public static string? Write(HttpContext? context, string? refreshToken, DateTime? expiresAt)
    {
        if (context is null || refreshToken is null || !Requested(context.Request))
            return refreshToken;

        context.Response.Cookies.Append(CookieName, refreshToken, Options(context, expiresAt));
        return null;
    }

    public static void Delete(HttpContext? context)
    {
        if (context is null || !Requested(context.Request))
            return;
        context.Response.Cookies.Delete(CookieName, Options(context, null));
    }

    private static CookieOptions Options(HttpContext context, DateTime? expiresAt)
    {
        var configuration = context.RequestServices?.GetService<IConfiguration>();
        var allowInsecure = bool.TryParse(configuration?[AllowInsecureKey], out var insecure) && insecure;
        return new CookieOptions
        {
            HttpOnly = true,
            // Behind TLS in every shared deployment; only the single-machine compose stack (plain http) opts out.
            Secure = !allowInsecure,
            SameSite = SameSiteMode.Strict,
            Path = configuration?[PathKey] is { Length: > 0 } path ? path : DefaultPath,
            Expires = expiresAt is { } at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : null,
            IsEssential = true,
        };
    }
}
