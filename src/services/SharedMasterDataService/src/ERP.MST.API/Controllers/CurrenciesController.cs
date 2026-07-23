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
[Route("api/v{version:apiVersion}/masterdata/currencies")]
public class CurrenciesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CurrenciesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── Currencies ─────────────────────────────────────────────────────────

    /// <summary>
    /// List currencies (paginated).
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:currencies:view")]
    [ProducesResponseType(typeof(PagedResult<CurrencyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListCurrenciesQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new currency.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:currencies:manage")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCurrencyCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a currency by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:currencies:view")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCurrencyQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Currency {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing currency.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:currencies:manage")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateCurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateCurrencyCommand(id, request.Name, request.Symbol, request.DecimalPlaces);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a currency.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission("masterdata:currencies:manage")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivateCurrencyCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Activate a currency.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission("masterdata:currencies:manage")]
    [ProducesResponseType(typeof(CurrencyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ActivateCurrencyCommand(id), cancellationToken);
        return Ok(result);
    }

    // ─── Exchange Rates ─────────────────────────────────────────────────────

    /// <summary>
    /// Get the effective exchange rate (latest EffectiveDate &lt;= asOfDate) between two currencies.
    /// </summary>
    [HttpGet("exchange-rates/effective")]
    [AuthorizePermission("masterdata:currencies:view")]
    [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEffectiveExchangeRate(
        [FromQuery] Guid fromCurrencyId,
        [FromQuery] Guid toCurrencyId,
        [FromQuery] DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetEffectiveExchangeRateQuery(fromCurrencyId, toCurrencyId, asOfDate), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"No effective exchange rate found for {fromCurrencyId} -> {toCurrencyId} as of {asOfDate:yyyy-MM-dd}." });

        return Ok(result);
    }

    [HttpGet("{currencyId:guid}/exchange-rates")]
    [AuthorizePermission("masterdata:currencies:view")]
    [ProducesResponseType(typeof(PagedResult<ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListExchangeRates(
        [FromRoute] Guid currencyId,
        [FromQuery] Guid? toCurrencyId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListExchangeRatesQuery(currencyId, toCurrencyId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{currencyId:guid}/exchange-rates")]
    [AuthorizePermission("masterdata:currencies:manage")]
    [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateExchangeRate(
        [FromRoute] Guid currencyId,
        [FromBody] CreateExchangeRateRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateExchangeRateCommand(currencyId, request.ToCurrencyId, request.Rate, request.EffectiveDate);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetExchangeRate), new { currencyId, id = result.Id }, result);
    }

    [HttpGet("{currencyId:guid}/exchange-rates/{id:guid}")]
    [AuthorizePermission("masterdata:currencies:view")]
    [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetExchangeRate(
        [FromRoute] Guid currencyId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetExchangeRateQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"ExchangeRate {id} not found." });

        return Ok(result);
    }

    [HttpPut("{currencyId:guid}/exchange-rates/{id:guid}")]
    [AuthorizePermission("masterdata:currencies:manage")]
    [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateExchangeRate(
        [FromRoute] Guid currencyId,
        [FromRoute] Guid id,
        [FromBody] UpdateExchangeRateRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateExchangeRateCommand(id, request.Rate, request.EffectiveDate);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public record UpdateCurrencyRequest(string Name, string Symbol, int DecimalPlaces);
public record CreateExchangeRateRequest(Guid ToCurrencyId, decimal Rate, DateTime EffectiveDate);
public record UpdateExchangeRateRequest(decimal Rate, DateTime EffectiveDate);
