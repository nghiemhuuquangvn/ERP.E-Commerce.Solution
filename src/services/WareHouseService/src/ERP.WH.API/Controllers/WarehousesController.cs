using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using ERP.Shared.Authorization;
using ERP.WH.Application.Commands.Warehouses;
using ERP.WH.Application.Queries.Warehouses;

namespace ERP.WH.API.Controllers;

/// <summary>Physical site/branch — did not exist anywhere in this system before; parent of WarehouseLocation.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/warehouses")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContextService;

    public WarehousesController(IMediator mediator, IUserContextService userContextService)
    {
        _mediator = mediator;
        _userContextService = userContextService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<WarehouseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WarehouseDto>>> List()
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var result = await _mediator.Send(new GetWarehousesQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDto>> GetById(Guid id)
    {
        if (!_userContextService.HasPermission("inventory:view"))
            return Forbid();

        var result = await _mediator.Send(new GetWarehouseByIdQuery { Id = id });
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WarehouseDto>> Create([FromBody] CreateWarehouseCommand command)
    {
        if (!_userContextService.HasPermission("inventory:warehouse:manage"))
            return Forbid();

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDto>> Update(Guid id, [FromBody] UpdateWarehouseCommand command)
    {
        if (!_userContextService.HasPermission("inventory:warehouse:manage"))
            return Forbid();

        command.Id = id;

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDto>> Activate(Guid id)
    {
        if (!_userContextService.HasPermission("inventory:warehouse:manage"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new SetWarehouseActiveCommand { Id = id, IsActive = true });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDto>> Deactivate(Guid id)
    {
        if (!_userContextService.HasPermission("inventory:warehouse:manage"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(new SetWarehouseActiveCommand { Id = id, IsActive = false });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
