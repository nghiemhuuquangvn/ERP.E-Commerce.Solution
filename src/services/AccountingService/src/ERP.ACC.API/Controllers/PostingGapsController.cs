using Asp.Versioning;
using ERP.ACC.Application.Posting;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    /// <summary>Documents whose journal entry is missing (no open fiscal period, incomplete posting profile) and the action that posts them (ROADMAP P1-3b).</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/posting-gaps")]
    public class PostingGapsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PostingGapsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [AuthorizePermission(PermissionConstants.Accounting_JournalsView)]
        [ProducesResponseType(typeof(List<PostingGapDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new GetPostingGapsQuery(), ct));

        [HttpPost("repost")]
        [AuthorizePermission(PermissionConstants.Accounting_JournalsManage)]
        [ProducesResponseType(typeof(List<PostingGapDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Repost(CancellationToken ct) => Ok(await _mediator.Send(new RepostPostingGapsCommand(), ct));
    }
}
