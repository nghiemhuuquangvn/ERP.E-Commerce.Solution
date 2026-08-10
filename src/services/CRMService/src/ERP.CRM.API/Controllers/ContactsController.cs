using Asp.Versioning;
using ERP.CRM.Application.Commands;
using ERP.CRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.CRM.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/contacts")]
public class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContext;

    public ContactsController(IMediator mediator, IUserContextService userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// List contacts with optional search and pagination.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("crm:contacts:view")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListContactsQuery(search, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Look up the contact linked to an IdentityService user (see GetContactByUserIdQuery —
    /// added for ChatBotService's escalate-to-human flow, which needs a ContactId to open a
    /// Case). Registered before "{id:guid}" so "by-user" isn't captured as a guid route value.
    /// </summary>
    [HttpGet("by-user/{userId:guid}")]
    [AuthorizePermission("crm:contacts:view")]
    public async Task<IActionResult> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetContactByUserIdQuery(userId), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"No CRM contact linked to user {userId}." });

        return Ok(result);
    }

    /// <summary>
    /// Get a single contact by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("crm:contacts:view")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetContactQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Contact {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Create a new contact.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("crm:contacts:manage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateContactCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing contact.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("crm:contacts:manage")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateContactCommand command,
        CancellationToken cancellationToken = default)
    {
        if (id != command.ContactId)
            return BadRequest(new { message = "Route id does not match body ContactId." });

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // ─── Tag endpoints ─────────────────────────────────────────────────────────

    /// <summary>
    /// Get all tags assigned to a contact.
    /// </summary>
    [HttpGet("{id:guid}/tags")]
    [AuthorizePermission("crm:contacts:view")]
    public async Task<IActionResult> GetTags(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetContactTagsQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Add a tag to a contact. Idempotent — duplicate tags are ignored.
    /// </summary>
    [HttpPost("{id:guid}/tags")]
    [AuthorizePermission("crm:contacts:manage")]
    public async Task<IActionResult> AddTag(
        Guid id,
        [FromBody] TagRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new AddContactTagCommand(id, request.Tag), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Remove a tag from a contact.
    /// </summary>
    [HttpDelete("{id:guid}/tags/{tag}")]
    [AuthorizePermission("crm:contacts:manage")]
    public async Task<IActionResult> RemoveTag(Guid id, string tag, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new RemoveContactTagCommand(id, tag), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// List contacts that have a specific tag applied.
    /// </summary>
    [HttpGet("by-tag/{tag}")]
    [AuthorizePermission("crm:contacts:view")]
    public async Task<IActionResult> ListByTag(
        string tag,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListContactsByTagQuery(tag, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// List all distinct tags used across all contacts.
    /// </summary>
    [HttpGet("tags/all")]
    [AuthorizePermission("crm:contacts:view")]
    public async Task<IActionResult> GetAllTags(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAllTagsQuery(), cancellationToken);
        return Ok(result);
    }

    public record TagRequest(string Tag);
}
