using Asp.Versioning;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/fiscal-periods")]
    public class FiscalPeriodsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public FiscalPeriodsController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:periods:view"))
                return Forbid();

            var result = await _mediator.Send(new ListFiscalPeriodsQuery(), ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:periods:view"))
                return Forbid();

            var result = await _mediator.Send(new GetFiscalPeriodQuery { FiscalPeriodId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Open([FromBody] OpenFiscalPeriodCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:periods:manage"))
                return Forbid();

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>Closes the period: checks the books, writes the period-end transfer (unless postClosingEntries is false) and locks the period.</summary>
        [HttpPost("{id:guid}/close")]
        public async Task<IActionResult> Close(Guid id, [FromQuery] bool postClosingEntries = true, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:periods:manage"))
                return Forbid();

            var result = await _mediator.Send(new CloseFiscalPeriodCommand { FiscalPeriodId = id, PostClosingEntries = postClosingEntries }, ct);
            return Ok(result);
        }

        /// <summary>Reopens the latest closed period; its closing entry is voided.</summary>
        [HttpPost("{id:guid}/reopen")]
        public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenFiscalPeriodCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:periods:manage"))
                return Forbid();

            command.FiscalPeriodId = id;
            return Ok(await _mediator.Send(command, ct));
        }
    }
}
