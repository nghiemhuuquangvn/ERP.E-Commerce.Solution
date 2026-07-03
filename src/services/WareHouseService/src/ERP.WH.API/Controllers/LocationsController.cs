using Microsoft.AspNetCore.Mvc;
using MediatR;
using ERP.WH.Application.Commands.Locations;
using ERP.WH.Application.Queries.Locations;

namespace ERP.WH.API.Controllers;

[ApiController]
[Route("api/v1/locations")]
public class LocationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LocationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<LocationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LocationDto>>> GetLocations([FromQuery] bool? isActive = null)
    {
        var result = await _mediator.Send(new GetLocationsQuery { IsActive = isActive });
        return Ok(result);
    }

    [HttpGet("{locationId}")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationDto>> GetLocationById(Guid locationId)
    {
        var result = await _mediator.Send(new GetLocationByIdQuery { LocationId = locationId });
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] CreateLocationCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetLocationById), new { locationId = result.LocationId }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{locationId}")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationDto>> UpdateLocation(Guid locationId, [FromBody] UpdateLocationCommand command)
    {
        command.LocationId = locationId;

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

    [HttpPost("{locationId}/activate")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationDto>> Activate(Guid locationId)
    {
        try
        {
            var result = await _mediator.Send(new SetLocationActiveCommand { LocationId = locationId, IsActive = true });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{locationId}/deactivate")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationDto>> Deactivate(Guid locationId)
    {
        try
        {
            var result = await _mediator.Send(new SetLocationActiveCommand { LocationId = locationId, IsActive = false });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
