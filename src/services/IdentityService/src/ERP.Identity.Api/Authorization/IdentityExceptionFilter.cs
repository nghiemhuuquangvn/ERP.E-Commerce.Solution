using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.Identity.Api.Authorization;

/// <summary>
/// Maps IdentityService's own tenant-boundary/data-integrity exceptions to clean HTTP
/// status codes, scoped entirely to this Api project (registered only in this
/// service's Program.cs) — the shared GlobalExceptionMiddleware is a catch-all that
/// maps everything to 500, it does not inspect exception types. Runs as an MVC action
/// filter, so it catches the exception before it would ever reach that middleware.
/// </summary>
public class IdentityExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (statusCode, title) = context.Exception switch
        {
            // Deliberately 404, not 403 — confirming "this resource exists but belongs
            // to another tenant" to an unauthorized caller is itself an information leak.
            TenantIsolationException => (StatusCodes.Status404NotFound, "Resource not found."),
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
            DuplicateException => (StatusCodes.Status409Conflict, "Conflict."),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "Business rule violation."),
            _ => (0, string.Empty)
        };

        if (statusCode == 0)
            return; // not one of ours — let the shared GlobalExceptionMiddleware handle it as 500

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = context.Exception.Message,
            Instance = context.HttpContext.Request.Path
        })
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;
    }
}
