using Asp.Versioning;
using ERP.ACC.Application.Handlers;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    /// <summary>
    /// The loan ledger (ROADMAP P1-2i): borrowings with their repayment schedule. Drawing and repaying a loan are still journal entries; the ledger gives the
    /// balance sheet the real due dates. Loans belong to the financing side of the books, so they use the bank permissions.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/loans")]
    public class LoansController : ControllerBase
    {
        private readonly IMediator _mediator;

        public LoansController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Accounting_BankView)]
        [ProducesResponseType(typeof(List<LoanDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new ListLoansQuery(), ct));

        [HttpGet("{id:guid}")]
        [AuthorizePermission(PermissionConstants.Accounting_BankView)]
        [ProducesResponseType(typeof(LoanDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var loan = await _mediator.Send(new GetLoanQuery { LoanId = id }, ct);
            return loan == null ? NotFound() : Ok(loan);
        }

        /// <summary>Registers a loan with its schedule (or a single maturity date). The principals of the schedule must add up to the principal.</summary>
        [HttpPost]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(LoanDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateLoanCommand command, CancellationToken ct)
        {
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }

        /// <summary>Records that an installment's principal was repaid on a date (default today).</summary>
        [HttpPost("{id:guid}/installments/{installmentId:guid}/repay")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(LoanDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Repay(Guid id, Guid installmentId, [FromBody] RecordLoanRepaymentCommand command, CancellationToken ct)
        {
            command.LoanId = id;
            command.InstallmentId = installmentId;
            return Ok(await _mediator.Send(command, ct));
        }

        /// <summary>
        /// Accrues the interest of every posted loan up to the date (Dr interest expense / Cr interest payable). Running it again for the same date posts nothing new;
        /// the repayment of an installment clears the accrual.
        /// </summary>
        [HttpPost("accrue-interest")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(LoanInterestAccrualDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> AccrueInterest([FromQuery] DateTime asOfDate, CancellationToken ct) =>
            Ok(await _mediator.Send(new AccrueLoanInterestCommand { AsOfDate = asOfDate }, ct));

        /// <summary>Posts the drawdown of a loan that was entered without it (Dr Bank / Cr Loan payable); later repayments are then posted too. Refused once anything was repaid.</summary>
        [HttpPost("{id:guid}/post-drawdown")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(LoanDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> PostDrawdown(Guid id, [FromBody] PostLoanDrawdownCommand? command, CancellationToken ct)
        {
            command ??= new PostLoanDrawdownCommand();
            command.LoanId = id;
            return Ok(await _mediator.Send(command, ct));
        }

        /// <summary>Cancels a loan entered by mistake (reason required); refused once anything has been repaid.</summary>
        [HttpPost("{id:guid}/cancel")]
        [AuthorizePermission(PermissionConstants.Accounting_BankManage)]
        [ProducesResponseType(typeof(LoanDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelLoanCommand command, CancellationToken ct)
        {
            command.LoanId = id;
            return Ok(await _mediator.Send(command, ct));
        }
    }
}
