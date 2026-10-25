using Asp.Versioning;
using ERP.ACC.Application.Bank;
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

        public BankAccountsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Accounting_BankView)]
        [ProducesResponseType(typeof(List<BankAccountDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] bool? activeOnly, CancellationToken ct = default) =>
            Ok(await _mediator.Send(new ListBankAccountsQuery { ActiveOnly = activeOnly }, ct));

        [HttpGet("{id:guid}")]
        [AuthorizePermission(PermissionConstants.Accounting_BankView)]
        [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
        {
            var result = await _mediator.Send(new GetBankAccountQuery { BankAccountId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(BankAccountDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateBankAccountCommand command, CancellationToken ct = default)
        {
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        /// <summary>List bank statements for a bank account.</summary>
        [HttpGet("{id:guid}/statements")]
        [AuthorizePermission(PermissionConstants.Accounting_BankView)]
        [ProducesResponseType(typeof(List<BankStatementDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListStatements(Guid id, CancellationToken ct = default) =>
            Ok(await _mediator.Send(new ListBankStatementsQuery { BankAccountId = id }, ct));

        /// <summary>Upload a bank statement (opening/closing balances for a period).</summary>
        [HttpPost("{id:guid}/statements")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(BankStatementDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateStatement(Guid id, [FromBody] CreateStatementRequest request, CancellationToken ct = default)
        {
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

        /// <summary>Mark a bank statement as reconciled; a statement with lines needs every line matched and the books to explain the closing balance.</summary>
        [HttpPost("{id:guid}/statements/{statementId:guid}/reconcile")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(BankStatementDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReconcileStatement(Guid id, Guid statementId, [FromBody] ReconcileRequest? request, CancellationToken ct = default) =>
            Ok(await _mediator.Send(new ReconcileBankStatementCommand { BankStatementId = statementId, Notes = request?.Notes }, ct));

        // ---- line-level reconciliation (ROADMAP P1-4) ----

        /// <summary>The statement against the books: its lines, the ledger lines still unmatched, and the difference.</summary>
        [HttpGet("{id:guid}/statements/{statementId:guid}/reconciliation")]
        [AuthorizePermission(PermissionConstants.Accounting_BankView)]
        [ProducesResponseType(typeof(BankReconciliationDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Reconciliation(Guid id, Guid statementId, CancellationToken ct = default) =>
            Ok(await _mediator.Send(new GetBankReconciliationQuery { BankStatementId = statementId }, ct));

        /// <summary>Adds the lines of the bank's CSV export to a statement; nothing is added when any row is wrong.</summary>
        [HttpPost("{id:guid}/statements/{statementId:guid}/lines/import")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(ImportBankStatementLinesResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> ImportLines(Guid id, Guid statementId, [FromBody] ImportLinesRequest request, CancellationToken ct = default)
        {
            var result = await _mediator.Send(new ImportBankStatementLinesCommand { BankStatementId = statementId, Csv = request.Csv }, ct);
            return result.Errors.Count > 0 ? BadRequest(result) : Ok(result);
        }

        [HttpPost("{id:guid}/statements/{statementId:guid}/lines")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(BankStatementLineDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddLine(Guid id, Guid statementId, [FromBody] AddLineRequest request, CancellationToken ct = default)
        {
            var result = await _mediator.Send(new AddBankStatementLineCommand
            {
                BankStatementId = statementId,
                LineDate = request.LineDate,
                Description = request.Description,
                Reference = request.Reference,
                Amount = request.Amount,
            }, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }

        [HttpDelete("{id:guid}/statements/{statementId:guid}/lines/{lineId:guid}")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        public async Task<IActionResult> RemoveLine(Guid id, Guid statementId, Guid lineId, CancellationToken ct = default)
        {
            await _mediator.Send(new RemoveBankStatementLineCommand { LineId = lineId }, ct);
            return NoContent();
        }

        /// <summary>Matches the statement lines to ledger lines of the same amount within a few days (a reference in the entry wins).</summary>
        [HttpPost("{id:guid}/statements/{statementId:guid}/auto-match")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(BankReconciliationDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> AutoMatch(Guid id, Guid statementId, [FromQuery] int? toleranceDays, CancellationToken ct = default) =>
            Ok(await _mediator.Send(new AutoMatchBankStatementCommand { BankStatementId = statementId, ToleranceDays = toleranceDays ?? BankMatcher.DefaultToleranceDays }, ct));

        [HttpPost("{id:guid}/statements/{statementId:guid}/lines/{lineId:guid}/match")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(BankReconciliationDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> MatchLine(Guid id, Guid statementId, Guid lineId, [FromBody] MatchLineRequest request, CancellationToken ct = default) =>
            Ok(await _mediator.Send(new MatchBankStatementLineCommand { LineId = lineId, JournalLineId = request.JournalLineId }, ct));

        [HttpPost("{id:guid}/statements/{statementId:guid}/lines/{lineId:guid}/unmatch")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(BankReconciliationDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> UnmatchLine(Guid id, Guid statementId, Guid lineId, CancellationToken ct = default) =>
            Ok(await _mediator.Send(new UnmatchBankStatementLineCommand { LineId = lineId }, ct));

        public record CreateStatementRequest(
            DateTime StatementDate,
            decimal OpeningBalance,
            decimal ClosingBalance,
            string? Notes = null);

        public record ReconcileRequest(string? Notes = null);

        public record ImportLinesRequest(string Csv);

        public record AddLineRequest(DateTime LineDate, decimal Amount, string? Description = null, string? Reference = null);

        public record MatchLineRequest(Guid JournalLineId);
    }
}
