using Asp.Versioning;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    /// <summary>
    /// Which account each posting key (receivables, payables, revenue, cash ...) uses when the system writes journal entries.
    /// A tenant gets a default profile the first time something is posted; edit it here when the tenant has its own chart.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/posting-profile")]
    public class PostingProfileController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PostingProfileController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [AuthorizePermission("accounting:accounts:view")]
        public async Task<IActionResult> Get(CancellationToken ct) =>
            Ok(await _mediator.Send(new GetPostingProfileQuery(), ct));

        [HttpPut]
        [AuthorizePermission("accounting:accounts:manage")]
        public async Task<IActionResult> Update([FromBody] UpdatePostingProfileCommand command, CancellationToken ct) =>
            Ok(await _mediator.Send(command, ct));
    }
}
