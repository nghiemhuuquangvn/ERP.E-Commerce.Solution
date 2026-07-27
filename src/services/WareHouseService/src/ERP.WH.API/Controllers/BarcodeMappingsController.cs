using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using ERP.Shared.Authorization;
using ERP.WH.Application.Commands.Barcodes;
using ERP.WH.Application.Queries.Barcodes;

namespace ERP.WH.API.Controllers;

/// <summary>Did not exist before — resolves a scanned barcode to a Product, critical for handheld scanners per the business doc.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/barcode")]
[Authorize]
public class BarcodeMappingsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContextService _userContextService;

    public BarcodeMappingsController(IMediator mediator, IUserContextService userContextService)
    {
        _mediator = mediator;
        _userContextService = userContextService;
    }

    [HttpGet("{code}/resolve")]
    [ProducesResponseType(typeof(ResolveBarcodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResolveBarcodeResponse>> Resolve(string code)
    {
        if (!_userContextService.HasPermission("inventory:barcode:view"))
            return Forbid();

        var result = await _mediator.Send(new ResolveBarcodeQuery { Barcode = code });
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BarcodeMappingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BarcodeMappingDto>> Create([FromBody] CreateBarcodeMappingCommand command)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(Resolve), new { code = result.Barcode }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!_userContextService.HasPermission("inventory:manage"))
            return Forbid();

        await _mediator.Send(new DeleteBarcodeMappingCommand { Id = id });
        return NoContent();
    }
}
