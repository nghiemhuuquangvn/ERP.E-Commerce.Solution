using Asp.Versioning;
using ERP.Shared.Authorization;
using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SO.Api.Controllers
{
    /// <summary>
    /// Minimal admin CRUD for CommissionPlan — no other service claims Commission, so this
    /// service must own the plan configuration itself. No approval workflow: a plan is
    /// either active or not.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/commission-plans")]
    public class CommissionPlansController : ControllerBase
    {
        private readonly ICommissionPlanRepository _plans;
        private readonly UserContext _userContext;

        public CommissionPlansController(ICommissionPlanRepository plans, UserContext userContext)
        {
            _plans = plans;
            _userContext = userContext;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            if (!_userContext.HasPermission("sales_order:read"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });

            var plans = await _plans.GetAllAsync(ct);
            return Ok(plans.Select(p => new CommissionPlanDto(p.Id, p.Name, p.RateType.ToString(), p.RateValue, p.IsActive)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCommissionPlanRequest request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("sales_order:approve"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:approve required" });

            var plan = CommissionPlan.Create(request.Name, request.RateValue);
            await _plans.AddAsync(plan, ct);
            return Ok(new CommissionPlanDto(plan.Id, plan.Name, plan.RateType.ToString(), plan.RateValue, plan.IsActive));
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate([FromRoute] Guid id, CancellationToken ct)
        {
            if (!_userContext.HasPermission("sales_order:approve"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:approve required" });

            var plan = await _plans.GetByIdAsync(id, ct);
            if (plan == null)
                return NotFound();

            plan.Deactivate();
            await _plans.UpdateAsync(plan, ct);
            return NoContent();
        }
    }

    /// <summary>Read-only query surface matching the doc's GET /sales/commissions?salesRepId=&amp;period= shape (real path: no "/sales/" prefix, see CLAUDE.md-vs-doc reconciliation).</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/commissions")]
    public class CommissionsController : ControllerBase
    {
        private readonly ICommissionRecordRepository _records;
        private readonly UserContext _userContext;

        public CommissionsController(ICommissionRecordRepository records, UserContext userContext)
        {
            _records = records;
            _userContext = userContext;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] Guid? salesRepId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        {
            if (!_userContext.HasPermission("sales_order:read"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });

            var records = await _records.ListAsync(salesRepId, from, to, ct);
            return Ok(records.Select(r => new CommissionRecordDto(r.Id, r.PlanId, r.SalesRepId, r.OrderId, r.Amount, r.Status.ToString(), r.CalculatedAt)));
        }
    }

    public record CommissionPlanDto(Guid Id, string Name, string RateType, decimal RateValue, bool IsActive);
    public record CreateCommissionPlanRequest(string Name, decimal RateValue);
    public record CommissionRecordDto(Guid Id, Guid PlanId, Guid SalesRepId, Guid OrderId, decimal Amount, string Status, DateTime CalculatedAt);
}
