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
[Route("api/v{version:apiVersion}/masterdata/payment-terms")]
public class PaymentTermsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentTermsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List payment terms (paginated).
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:payment-terms:view")]
    [ProducesResponseType(typeof(PagedResult<PaymentTermDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListPaymentTermsQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new payment term.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:payment-terms:manage")]
    [ProducesResponseType(typeof(PaymentTermDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentTermCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a payment term by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:payment-terms:view")]
    [ProducesResponseType(typeof(PaymentTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetPaymentTermQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"PaymentTerm {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing payment term.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:payment-terms:manage")]
    [ProducesResponseType(typeof(PaymentTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdatePaymentTermRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdatePaymentTermCommand(id, request.Name, request.NetDays, request.DepositPercent);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a payment term.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission("masterdata:payment-terms:manage")]
    [ProducesResponseType(typeof(PaymentTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivatePaymentTermCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Activate a payment term.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission("masterdata:payment-terms:manage")]
    [ProducesResponseType(typeof(PaymentTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ActivatePaymentTermCommand(id), cancellationToken);
        return Ok(result);
    }
}

public record UpdatePaymentTermRequest(string Name, int NetDays, decimal? DepositPercent);
