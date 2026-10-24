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
    [Route("api/v{version:apiVersion}/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly UserContext _userContext;

        public AccountsController(IMediator mediator, UserContext userContext)
        {
            _mediator = mediator;
            _userContext = userContext;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:view"))
                return Forbid();

            var result = await _mediator.Send(new ListAccountsQuery(), ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:view"))
                return Forbid();

            var result = await _mediator.Send(new GetAccountQuery { AccountId = id }, ct);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpGet("tree")]
        public async Task<IActionResult> Tree(CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:view"))
                return Forbid();

            return Ok(await _mediator.Send(new GetAccountTreeQuery(), ct));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:manage"))
                return Forbid();

            command.AccountId = id;
            return Ok(await _mediator.Send(command, ct));
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:manage"))
                return Forbid();

            return Ok(await _mediator.Send(new SetAccountActiveCommand { AccountId = id, Active = false }, ct));
        }

        [HttpPost("{id:guid}/activate")]
        public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:manage"))
                return Forbid();

            return Ok(await _mediator.Send(new SetAccountActiveCommand { AccountId = id, Active = true }, ct));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAccountCommand command, CancellationToken ct)
        {
            if (!_userContext.HasPermission("accounting:accounts:manage"))
                return Forbid();

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
    }
}
