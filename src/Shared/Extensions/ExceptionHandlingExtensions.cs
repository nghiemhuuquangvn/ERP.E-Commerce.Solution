using ERP.Shared.Middleware;
using Microsoft.AspNetCore.Builder;

namespace ERP.Shared.Extensions;

public static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Registers <see cref="GlobalExceptionMiddleware"/>. Call this first in the pipeline
    /// (before UseAuthentication/UseAuthorization/UseOcelot) so every unhandled exception is
    /// logged centrally and returned as a ProblemDetails response.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
