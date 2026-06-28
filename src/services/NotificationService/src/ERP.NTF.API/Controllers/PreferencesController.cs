using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

[ApiController]
[Route("api/v1/preferences")]
public class PreferencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PreferencesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPreferenceQuery { UserId = userId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Update(Guid userId, [FromBody] UpdatePreferenceCommand command, CancellationToken ct)
    {
        command.UserId = userId;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{userId:guid}/unsubscribe")]
    public async Task<IActionResult> Unsubscribe(Guid userId, CancellationToken ct)
    {
        await _mediator.Send(new UnsubscribeCommand { UserId = userId }, ct);
        return NoContent();
    }
}
