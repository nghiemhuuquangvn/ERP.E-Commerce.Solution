using Asp.Versioning;
using ERP.NTF.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

/// <summary>
/// Every notification of the tenant (the admin history page, ROADMAP P1-6). Reading other people's messages is the elevated,
/// admin-scope permission (<c>notifications:send</c>), like a cross-recipient read in <see cref="NotificationsController"/>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notification-history")]
public class NotificationHistoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationHistoryController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Notifications_Send)]
    [ProducesResponseType(typeof(NotificationHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListNotificationsQuery { Status = status, Channel = channel, Search = search, Skip = skip, Take = take }, ct));
}
