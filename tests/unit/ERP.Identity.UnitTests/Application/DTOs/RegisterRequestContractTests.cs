using System.Reflection;
using ERP.Identity.Application.DTOs.Auth;
using FluentAssertions;
using Xunit;

namespace ERP.Identity.UnitTests.Application.DTOs;

/// <summary>
/// Risk R19 (browser walk-through): the storefront's register form got "The TenantCode field is required" because the
/// request record declared TenantCode as a non-nullable string, which MVC treats as required. The tenant is the request host
/// (the controller overwrites it), so the client must not have to send it.
/// </summary>
public class RegisterRequestContractTests
{
    [Fact]
    public void A_client_can_register_without_sending_a_tenant_code_or_user_type()
    {
        var parameters = typeof(RegisterDto.RegisterRequest).GetConstructors().Single().GetParameters();
        var nullability = new NullabilityInfoContext();

        var tenant = parameters.Single(p => p.Name == "TenantCode");
        nullability.Create(tenant).WriteState.Should().Be(NullabilityState.Nullable, "a non-nullable string is implicitly [Required] in MVC");
        tenant.HasDefaultValue.Should().BeTrue();
        parameters.Single(p => p.Name == "UserType").HasDefaultValue.Should().BeTrue();

        var request = System.Text.Json.JsonSerializer.Deserialize<RegisterDto.RegisterRequest>(
            "{\"email\":\"a@b.test\",\"password\":\"x\",\"userName\":\"A B\"}", new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        request!.TenantCode.Should().BeNull();
    }
}
