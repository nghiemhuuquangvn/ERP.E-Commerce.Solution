using Asp.Versioning;
using ERP.NTF.Application.Commands;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

/// <summary>
/// The signed-in user's phones that may receive push messages (Admin mobile app). Everything is about the caller's own account: the user id comes
/// from the token, never from the request. Registering the same device again replaces its token; signing out removes it.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/device-tokens")]
[AuthorizeAnyAuthenticatedUser]
public class DeviceTokensController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public DeviceTokensController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    public record RegisterDeviceTokenRequest(string Token, string DeviceId, string? Platform);

    /// <summary>Registers (or refreshes) the push token of one device for the signed-in user.</summary>
    [HttpPut]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceTokenRequest body, CancellationToken ct)
    {
        if (!Guid.TryParse(_userContext.UserId, out var userId)) return Forbid();
        await _mediator.Send(new RegisterDeviceTokenCommand { UserId = userId, Token = body.Token, DeviceId = body.DeviceId, Platform = body.Platform }, ct);
        return NoContent();
    }

    /// <summary>Removes the push token of one device of the signed-in user (sign-out). Removing one that is not there is fine.</summary>
    [HttpDelete("{deviceId}")]
    public async Task<IActionResult> Unregister([FromRoute] string deviceId, CancellationToken ct)
    {
        if (!Guid.TryParse(_userContext.UserId, out var userId)) return Forbid();
        await _mediator.Send(new UnregisterDeviceTokenCommand { UserId = userId, DeviceId = deviceId }, ct);
        return NoContent();
    }
}
