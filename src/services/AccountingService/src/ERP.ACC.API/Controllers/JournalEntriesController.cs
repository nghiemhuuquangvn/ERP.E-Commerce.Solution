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
    [Route("api/v{version:apiVersion}/journal-entries")]
    public class JournalEntriesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public JournalEntriesController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] Guid? fiscalPeriodId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:journals:view"))
                return Forbid();

            var result = await _mediator.Send(new ListJournalEntriesQuery
            {
                FiscalPeriodId = fiscalPeriodId,
                PageNumber = pageNumber,
                PageSize = pageSize
            }, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:journals:view"))
                return Forbid();

            var result = await _mediator.Send(new GetJournalEntryQuery { JournalEntryId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("{id:guid}/reverse")]
        public async Task<IActionResult> Reverse(Guid id, [FromBody] ReverseJournalEntryCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:journals:manage"))
                return Forbid();

            command.JournalEntryId = id;
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }

        [HttpPost("{id:guid}/void")]
        public async Task<IActionResult> Void(Guid id, [FromBody] VoidJournalEntryCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:journals:manage"))
                return Forbid();

            command.JournalEntryId = id;
            return Ok(await _mediator.Send(command, ct));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] PostJournalEntryCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:journals:manage"))
                return Forbid();

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
    }
}
