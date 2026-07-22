using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Queries;
using Asp.Versioning;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/tax-rates")]
    public class TaxRatesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public TaxRatesController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<TaxRateDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(
            [FromQuery] bool? activeOnly,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:tax:view"))
                return Forbid();

            var result = await _mediator.Send(new ListTaxRatesQuery { ActiveOnly = activeOnly }, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(TaxRateDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:tax:view"))
                return Forbid();

            var result = await _mediator.Send(new GetTaxRateQuery { TaxRateId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(TaxRateDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateTaxRateCommand command, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:tax:manage"))
                return Forbid();

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(TaxRateDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateTaxRateRequest request,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:tax:manage"))
                return Forbid();

            var result = await _mediator.Send(
                new UpdateTaxRateCommand
                {
                    TaxRateId = id,
                    Name = request.Name,
                    Code = request.Code,
                    Rate = request.Rate,
                    Description = request.Description
                }, ct);
            return Ok(result);
        }

        [HttpPost("{id:guid}/deactivate")]
        [ProducesResponseType(typeof(TaxRateDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:tax:manage"))
                return Forbid();

            var result = await _mediator.Send(new DeactivateTaxRateCommand { TaxRateId = id }, ct);
            return Ok(result);
        }

        public record UpdateTaxRateRequest(string Name, string Code, decimal Rate, string? Description);
    }
}
