using Asp.Versioning;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/bank-accounts")]
    public class BankAccountsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public BankAccountsController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<BankAccountDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(
            [FromQuery] bool? activeOnly,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:bank:view"))
                return Forbid();

            var result = await _mediator.Send(new ListBankAccountsQuery { ActiveOnly = activeOnly }, ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:bank:view"))
                return Forbid();

            var result = await _mediator.Send(new GetBankAccountQuery { BankAccountId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateBankAccountCommand command, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:bank:manage"))
                return Forbid();

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>
        /// List bank statements for a bank account.
        /// </summary>
        [HttpGet("{id:guid}/statements")]
        [ProducesResponseType(typeof(List<BankStatementDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListStatements(Guid id, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:bank:view"))
                return Forbid();

            var result = await _mediator.Send(new ListBankStatementsQuery { BankAccountId = id }, ct);
            return Ok(result);
        }

        /// <summary>
        /// Upload a bank statement (opening/closing balances for a period).
        /// </summary>
        [HttpPost("{id:guid}/statements")]
        [ProducesResponseType(typeof(BankStatementDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateStatement(
            Guid id,
            [FromBody] CreateStatementRequest request,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:bank:manage"))
                return Forbid();

            var result = await _mediator.Send(new CreateBankStatementCommand
            {
                BankAccountId = id,
                StatementDate = request.StatementDate,
                OpeningBalance = request.OpeningBalance,
                ClosingBalance = request.ClosingBalance,
                Notes = request.Notes
            }, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        /// <summary>
        /// Mark a bank statement as reconciled.
        /// </summary>
        [HttpPost("{id:guid}/statements/{statementId:guid}/reconcile")]
        [ProducesResponseType(typeof(BankStatementDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReconcileStatement(
            Guid id,
            Guid statementId,
            [FromBody] ReconcileRequest? request,
            CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("accounting:bank:manage"))
                return Forbid();

            var result = await _mediator.Send(new ReconcileBankStatementCommand
            {
                BankStatementId = statementId,
                Notes = request?.Notes
            }, ct);
            return Ok(result);
        }

        public record CreateStatementRequest(
            DateTime StatementDate,
            decimal OpeningBalance,
            decimal ClosingBalance,
            string? Notes = null);

        public record ReconcileRequest(string? Notes = null);
    }
}
