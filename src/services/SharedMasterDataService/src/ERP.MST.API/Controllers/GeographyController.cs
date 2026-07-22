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
[Route("api/v{version:apiVersion}/masterdata/geography")]
public class GeographyController : ControllerBase
{
    private readonly IMediator _mediator;

    public GeographyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── Countries ──────────────────────────────────────────────────────────

    [HttpGet("countries")]
    [AuthorizePermission("masterdata:geography:view")]
    [ProducesResponseType(typeof(PagedResult<CountryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListCountries(
        [FromQuery] string? languageCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListCountriesQuery(languageCode, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("countries")]
    [AuthorizePermission("masterdata:geography:manage")]
    [ProducesResponseType(typeof(CountryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCountry(
        [FromBody] CreateCountryCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetCountry), new { countryId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("countries/{countryId:guid}")]
    [AuthorizePermission("masterdata:geography:view")]
    [ProducesResponseType(typeof(CountryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCountry(
        [FromRoute] Guid countryId,
        [FromQuery] string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCountryQuery(countryId, languageCode), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Country {countryId} not found." });

        return Ok(result);
    }

    // ─── Provinces ──────────────────────────────────────────────────────────

    [HttpGet("countries/{countryId:guid}/provinces")]
    [AuthorizePermission("masterdata:geography:view")]
    [ProducesResponseType(typeof(PagedResult<ProvinceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListProvinces(
        [FromRoute] Guid countryId,
        [FromQuery] string? languageCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListProvincesQuery(countryId, languageCode, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("countries/{countryId:guid}/provinces")]
    [AuthorizePermission("masterdata:geography:manage")]
    [ProducesResponseType(typeof(ProvinceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateProvince(
        [FromRoute] Guid countryId,
        [FromBody] CreateProvinceRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateProvinceCommand(countryId, request.Code, request.Name);
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetProvince), new { provinceId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("provinces/{provinceId:guid}")]
    [AuthorizePermission("masterdata:geography:view")]
    [ProducesResponseType(typeof(ProvinceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProvince(
        [FromRoute] Guid provinceId,
        [FromQuery] string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetProvinceQuery(provinceId, languageCode), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Province {provinceId} not found." });

        return Ok(result);
    }

    // ─── Districts ──────────────────────────────────────────────────────────

    [HttpGet("provinces/{provinceId:guid}/districts")]
    [AuthorizePermission("masterdata:geography:view")]
    [ProducesResponseType(typeof(PagedResult<DistrictDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListDistricts(
        [FromRoute] Guid provinceId,
        [FromQuery] string? languageCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListDistrictsQuery(provinceId, languageCode, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("provinces/{provinceId:guid}/districts")]
    [AuthorizePermission("masterdata:geography:manage")]
    [ProducesResponseType(typeof(DistrictDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDistrict(
        [FromRoute] Guid provinceId,
        [FromBody] CreateDistrictRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateDistrictCommand(provinceId, request.Code, request.Name);
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetDistrict), new { districtId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("districts/{districtId:guid}")]
    [AuthorizePermission("masterdata:geography:view")]
    [ProducesResponseType(typeof(DistrictDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDistrict(
        [FromRoute] Guid districtId,
        [FromQuery] string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetDistrictQuery(districtId, languageCode), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"District {districtId} not found." });

        return Ok(result);
    }

    // ─── Wards ──────────────────────────────────────────────────────────────

    [HttpGet("districts/{districtId:guid}/wards")]
    [AuthorizePermission("masterdata:geography:view")]
    [ProducesResponseType(typeof(PagedResult<WardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListWards(
        [FromRoute] Guid districtId,
        [FromQuery] string? languageCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListWardsQuery(districtId, languageCode, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("districts/{districtId:guid}/wards")]
    [AuthorizePermission("masterdata:geography:manage")]
    [ProducesResponseType(typeof(WardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateWard(
        [FromRoute] Guid districtId,
        [FromBody] CreateWardRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateWardCommand(districtId, request.Code, request.Name);
        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetWard), new { wardId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("wards/{wardId:guid}")]
    [AuthorizePermission("masterdata:geography:view")]
    [ProducesResponseType(typeof(WardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetWard(
        [FromRoute] Guid wardId,
        [FromQuery] string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetWardQuery(wardId, languageCode), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Ward {wardId} not found." });

        return Ok(result);
    }
}

public record CreateProvinceRequest(string Code, string Name);
public record CreateDistrictRequest(string Code, string Name);
public record CreateWardRequest(string Code, string Name);
