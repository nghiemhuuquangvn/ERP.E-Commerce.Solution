using ERP.CRM.Application.Commands;
using ERP.CRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.CRM.API.Controllers;

[ApiController]
[Route("api/v1/crm/contacts")]
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
}
