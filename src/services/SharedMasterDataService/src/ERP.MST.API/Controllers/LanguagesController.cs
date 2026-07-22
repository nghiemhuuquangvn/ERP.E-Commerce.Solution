using Asp.Versioning;
using ERP.MST.Application.Commands;
using ERP.MST.Application.DTOs;
using ERP.MST.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/masterdata/languages")]
public class LanguagesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LanguagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List supported languages (paginated).
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:languages:view")]
    [ProducesResponseType(typeof(PagedResult<SupportedLanguageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListLanguagesQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a supported language by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:languages:view")]
    [ProducesResponseType(typeof(SupportedLanguageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLanguageQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Language {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Create a new supported language.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:languages:manage")]
    [ProducesResponseType(typeof(SupportedLanguageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLanguageCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
