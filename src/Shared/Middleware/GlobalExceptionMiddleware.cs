using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Middleware
{
    /// <summary>
    /// Catches any unhandled exception that reaches the outermost pipeline, logs it via
    /// <see cref="ILogger"/> (which — once a service calls AddSerilogLogging — flows through the
    /// Kafka sink into the centralized log store as a Kind=Exception entry), then returns an
    /// RFC7807 ProblemDetails response instead of leaking a raw stack trace to the client.
    /// Must be registered first in the pipeline, before authentication/routing/Ocelot.
    ///
    /// Maps well-known exception types to their intended HTTP status (below) instead of
    /// blanket 500 — this is the single authoritative place for that mapping across every
    /// service that calls UseGlobalExceptionHandling(). Previously this middleware treated
    /// every exception identically, which meant deliberate signals like `throw new
    /// UnauthorizedAccessException(...)` (used ~30x in IdentityService alone to mean "return
    /// 401") surfaced as 500 instead — IdentityService had worked around this locally with its
    /// own MVC filter (`IdentityExceptionFilter`, now removed/superseded by this), while every
    /// other service either had no mapping at all or none whatsoever. Consolidating here means
    /// every service gets the same correct behavior from the same one-line registration.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        // Resolved lazily per-request via HttpContext.RequestServices, not constructor-injected —
        // not every service that calls UseGlobalExceptionHandling() also calls
        // AddSharedLocalization()/AddLocalization() (Gateway and LoggingService don't), and a
        // constructor dependency on IStringLocalizer<SharedResource> would fail DI resolution
        // and crash those services at startup. Falls back to the same hardcoded English strings
        // this class always returned when no localizer is registered.
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var localizer = context.RequestServices.GetService<IStringLocalizer<SharedResource>>();
                var (statusCode, title, exposeDetail) = MapException(ex, localizer);

                // Only genuine 500s are "unhandled exceptions" worth an Error-level alert with
                // a full stack trace — a mapped 401/404/409/400 is expected control flow (e.g.
                // an expired refresh token on every session timeout) and logging it at Error
                // would bury real incidents in routine noise.
                if (statusCode == StatusCodes.Status500InternalServerError)
                {
                    _logger.LogError(
                        ex,
                        "Unhandled exception processing {Method} {Path}",
                        context.Request.Method,
                        context.Request.Path);
                }
                else
                {
                    _logger.LogWarning(
                        "Request rejected ({StatusCode} {Title}) processing {Method} {Path}: {Message}",
                        statusCode, title, context.Request.Method, context.Request.Path, ex.Message);
                }

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = exposeDetail
                        ? ex.Message
                        : statusCode == StatusCodes.Status500InternalServerError
                            ? Localize(localizer, "Detail_UnexpectedError", "The request could not be completed. The error has been logged.")
                            : title,
                    Instance = context.Request.Path
                };

                await context.Response.WriteAsJsonAsync(problemDetails);
            }
        }

        /// <summary>
        /// Deliberately does NOT map ArgumentException/InvalidOperationException — those are
        /// used throughout the domain layer as genuine invariant guards (a real bug slipped
        /// past validation), not as deliberate client-facing signals, and should stay 500.
        ///
        /// ExposeDetail controls whether ex.Message reaches the client at all. Most mapped
        /// exceptions carry a message that's safe/expected to show (e.g. "Product with ID X
        /// not found", "Email already exists" — normal, non-sensitive UX). Authentication and
        /// cross-tenant exceptions are different: their messages are exactly the signal an
        /// attacker wants (login enumeration via "User not found" vs "Invalid credentials" vs
        /// "Account is locked"; TenantIsolationException's message embeds the entity/tenant
        /// IDs it exists specifically to avoid confirming) — those always get a fixed, generic
        /// Detail instead, regardless of the real internal reason (which is still captured by
        /// the security-audit logging that already runs alongside every throw in AuthService).
        /// </summary>
        private static (int StatusCode, string Title, bool ExposeDetail) MapException(Exception ex, IStringLocalizer<SharedResource>? localizer) => ex switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, Localize(localizer, "Title_Unauthorized", "Unauthorized."), false),
            // 404, not 403, for tenant-isolation violations — confirming "this resource exists
            // but belongs to another tenant" to an unauthorized caller is itself an information leak.
            TenantIsolationException => (StatusCodes.Status404NotFound, Localize(localizer, "Title_NotFound", "Resource not found."), false),
            EntityNotFoundException => (StatusCodes.Status404NotFound, Localize(localizer, "Title_NotFound", "Resource not found."), true),
            KeyNotFoundException => (StatusCodes.Status404NotFound, Localize(localizer, "Title_NotFound", "Resource not found."), true),
            DuplicateException => (StatusCodes.Status409Conflict, Localize(localizer, "Title_Conflict", "Conflict."), true),
            ConcurrencyException => (StatusCodes.Status409Conflict, Localize(localizer, "Title_Conflict", "Conflict."), true),
            BusinessRuleException => (StatusCodes.Status400BadRequest, Localize(localizer, "Title_BusinessRuleViolation", "Business rule violation."), true),
            _ => (StatusCodes.Status500InternalServerError, Localize(localizer, "Title_UnexpectedError", "An unexpected error occurred."), false)
        };

        private static string Localize(IStringLocalizer<SharedResource>? localizer, string key, string fallback) =>
            localizer?[key].Value ?? fallback;
    }
}
