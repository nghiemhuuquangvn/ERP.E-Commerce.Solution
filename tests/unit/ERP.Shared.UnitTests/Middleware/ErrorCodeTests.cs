using System.Text;
using System.Text.Json;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.Shared.UnitTests.Middleware;

/// <summary>BA spec BR-GEN-07: a business-rule refusal carries its rule code in the answer, so a client reacts to the code and not to the wording.</summary>
public class ErrorCodeTests
{
    private static async Task<(int Status, JsonElement Body)> Run(Exception toThrow)
    {
        var middleware = new GlobalExceptionMiddleware(_ => throw toThrow, NullLogger<GlobalExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddOptions().BuildServiceProvider() };
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);
        var text = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        return (context.Response.StatusCode, JsonDocument.Parse(text).RootElement);
    }

    [Fact]
    public async Task A_business_rule_refusal_names_its_rule_as_errorCode()
    {
        var (status, body) = await Run(new BusinessRuleException("MFG_PO_007", "Không đủ vật tư."));

        status.Should().Be(400);
        body.GetProperty("errorCode").GetString().Should().Be("MFG_PO_007");
        body.GetProperty("detail").GetString().Should().Be("Không đủ vật tư.");
    }

    [Fact]
    public async Task Other_refusals_have_no_errorCode()
    {
        var (status, body) = await Run(new EntityNotFoundException("Plant", Guid.NewGuid()));

        status.Should().Be(404);
        body.TryGetProperty("errorCode", out _).Should().BeFalse();
    }
}
