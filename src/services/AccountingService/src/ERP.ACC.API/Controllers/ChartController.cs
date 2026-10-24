using Asp.Versioning;
using ERP.ACC.Application.Handlers;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers
{
    /// <summary>Ready-made charts of accounts (ROADMAP P1-2): the 14-account chart and the Vietnamese chart (Circular 99/2025).</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/accounting/chart")]
    public class ChartController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ChartController(IMediator mediator) => _mediator = mediator;

        [HttpGet("templates")]
        [AuthorizePermission("accounting:accounts:view")]
        public async Task<IActionResult> Templates(CancellationToken ct) =>
            Ok(await _mediator.Send(new ListChartTemplatesQuery(), ct));

        /// <summary>Adds the template's accounts and points the posting profile at them. Refused once the tenant has journal entries.</summary>
        [HttpPost("apply-template")]
        [AuthorizePermission("accounting:accounts:manage")]
        public async Task<IActionResult> Apply([FromBody] ApplyChartTemplateCommand command, CancellationToken ct) =>
            Ok(await _mediator.Send(command, ct));
    }
}
