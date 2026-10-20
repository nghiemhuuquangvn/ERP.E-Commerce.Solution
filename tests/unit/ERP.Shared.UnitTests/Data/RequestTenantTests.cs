using System.Security.Claims;
using ERP.Shared.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ERP.Shared.UnitTests.Data;

public class RequestTenantTests
{
    private static readonly string JwtTenant = Guid.NewGuid().ToString();
    private static readonly string HostTenant = Guid.NewGuid().ToString();

    private static HttpContext Request(string? tokenTenant, string? gatewayHeader)
    {
        var context = new DefaultHttpContext();
        if (tokenTenant != null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "u1"), new Claim("tenant_id", tokenTenant)], authenticationType: "Bearer"));
        if (gatewayHeader != null)
            context.Request.Headers[RequestTenant.GatewayTenantIdHeader] = gatewayHeader;
        return context;
    }

    [Fact]
    public void AuthenticatedCaller_UsesTheTokenTenant_NotTheHeader()
    {
        RequestTenant.Resolve(Request(JwtTenant, HostTenant)).Should().Be(JwtTenant);
    }

    [Fact]
    public void AnonymousCaller_UsesTheGatewayHostTenant()
    {
        RequestTenant.Resolve(Request(null, HostTenant)).Should().Be(HostTenant);
    }

    [Fact]
    public void AuthenticatedCaller_WithoutTenantClaim_NeverFallsBackToTheHeader()
    {
        var context = Request(null, HostTenant);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "Bearer"));

        RequestTenant.Resolve(context).Should().BeNull();
    }

    [Fact]
    public void NoRequest_HasNoTenant()
    {
        RequestTenant.Resolve(null).Should().BeNull();
        RequestTenant.Resolve(Request(null, null)).Should().BeNull();
    }
}
