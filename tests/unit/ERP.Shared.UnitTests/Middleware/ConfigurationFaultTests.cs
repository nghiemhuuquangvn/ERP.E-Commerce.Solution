using System.Text;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Events;
using Xunit;

namespace ERP.Shared.UnitTests.Middleware;

/// <summary>R101: a missing configuration value is a deployment fault (500, key name only in the log); the request log is quiet for the health probe and loud for a 5xx.</summary>
public class ConfigurationFaultTests
{
    private static async Task<(int Status, string Body)> Run(Exception toThrow)
    {
        var middleware = new GlobalExceptionMiddleware(_ => throw toThrow, NullLogger<GlobalExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddOptions().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()));
    }

    [Fact]
    public async Task A_missing_configuration_value_is_a_500_that_does_not_show_the_key()
    {
        var (status, body) = await Run(new ServiceMisconfiguredException("Authentication:Twitter:ClientId configuration is missing."));

        status.Should().Be(500);
        body.Should().NotContain("Twitter").And.NotContain("ClientId");
    }

    [Fact]
    public async Task A_plain_refusal_of_our_code_is_still_a_400_with_its_message()
    {
        Exception ex;
        try { throw new InvalidOperationException("Bill is not in Pending status"); } catch (Exception e) { ex = e; }

        var (status, body) = await Run(ex);

        status.Should().Be(400);
        body.Should().Contain("Bill is not in Pending status");
    }

    [Fact]
    public void The_misconfiguration_exception_is_never_taken_for_a_refusal()
    {
        Exception ex;
        try { throw new ServiceMisconfiguredException("Jwt:Issuer configuration is missing."); } catch (Exception e) { ex = e; }

        GlobalExceptionMiddleware.IsRefusalOfOurOwnCode(ex).Should().BeFalse();
    }

    [Theory]
    [InlineData("/api/v1/payments", 200, LogEventLevel.Information)]
    [InlineData("/api/v1/payments", 400, LogEventLevel.Information)]
    [InlineData("/api/v1/payments", 500, LogEventLevel.Error)]
    [InlineData("/health", 200, LogEventLevel.Verbose)]
    public void The_request_log_level_follows_the_status(string path, int status, LogEventLevel expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.StatusCode = status;

        LoggingExtensions.RequestLogLevel(context, 5, null).Should().Be(expected);
    }
}
