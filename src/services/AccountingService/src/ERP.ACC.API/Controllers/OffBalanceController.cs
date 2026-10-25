using Asp.Versioning;
using ERP.ACC.Application.Handlers;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    /// <summary>
    /// The off-balance register (ROADMAP P1-2i): movements of the class 0 accounts (001 – 008), kept outside the double entry. The register is a book of
    /// entries, so it uses the journal permissions.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/off-balance")]
    public class OffBalanceController : ControllerBase
    {
        private readonly IMediator _mediator;

        public OffBalanceController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Accounting_JournalsView)]
        [ProducesResponseType(typeof(List<OffBalanceEntryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(
            [FromQuery] Guid? accountId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken ct) =>
            Ok(await _mediator.Send(new ListOffBalanceEntriesQuery { AccountId = accountId, FromDate = fromDate, ToDate = toDate }, ct));

        /// <summary>Records one movement of an off-balance account: direction In raises the balance, Out lowers it.</summary>
        [HttpPost]
        [AuthorizePermission(PermissionConstants.Accounting_JournalsManage)]
        [ProducesResponseType(typeof(OffBalanceEntryDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> Post([FromBody] PostOffBalanceEntryCommand command, CancellationToken ct)
        {
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(List), new { accountId = result.AccountId }, result);
        }

        /// <summary>Voids a posted entry (reason required); refused when its date is in a closed fiscal period.</summary>
        [HttpPost("{id:guid}/void")]
        [AuthorizePermission(PermissionConstants.Accounting_JournalsManage)]
        [ProducesResponseType(typeof(OffBalanceEntryDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Void(Guid id, [FromBody] VoidOffBalanceEntryCommand command, CancellationToken ct)
        {
            command.EntryId = id;
            return Ok(await _mediator.Send(command, ct));
        }
    }
}
