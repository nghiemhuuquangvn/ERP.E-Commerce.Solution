using Asp.Versioning;
using ERP.ACC.Application.Reports;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    /// <summary>
    /// A tenant's own placement of accounts on the rows of B01 / B02 (ROADMAP P1-2j): for a chart that is neither the Vietnamese nor the 14-account one, the accounts the
    /// standard layout cannot place show up as "unmapped"; map their number prefixes to rows here and they are counted.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/statement-mappings")]
    public class StatementMappingsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StatementMappingsController(IMediator mediator) => _mediator = mediator;

        /// <summary>The rows of the statement, what is already mapped, and the accounts with postings that no row takes.</summary>
        [HttpGet]
        [AuthorizePermission(PermissionConstants.Accounting_AccountsView)]
        [ProducesResponseType(typeof(StatementMappingDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get([FromQuery] string statementId = StatementLayouts.BalanceSheet, CancellationToken ct = default) =>
            Ok(await _mediator.Send(new GetStatementMappingQuery { StatementId = statementId }, ct));

        /// <summary>Replaces the mapping of one statement. A prefix must match an account, be placed once, and not already be placed by the standard layout.</summary>
        [HttpPut]
        [AuthorizePermission(PermissionConstants.Accounting_AccountsManage)]
        [ProducesResponseType(typeof(StatementMappingDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update([FromBody] UpdateStatementMappingCommand command, CancellationToken ct) =>
            Ok(await _mediator.Send(command, ct));
    }
}
