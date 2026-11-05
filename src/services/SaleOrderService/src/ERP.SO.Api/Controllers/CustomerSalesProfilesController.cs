using Asp.Versioning;
using ERP.Shared.Authorization;
using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SO.Api.Controllers
{
    /// <summary>
    /// Sales/Finance-managed credit limit per customer — this service is the source of
    /// truth for CreditLimit (no other service in this system has one to sync from), so
    /// staff set it here directly rather than it arriving via an event.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/customer-sales-profiles")]
    public class CustomerSalesProfilesController : ControllerBase
    {
        private readonly ICustomerSalesProfileRepository _profiles;
        private readonly UserContext _userContext;

        public CustomerSalesProfilesController(ICustomerSalesProfileRepository profiles, UserContext userContext)
        {
            _profiles = profiles;
            _userContext = userContext;
        }

        /// <summary>The customers that have a credit limit, highest open balance first (Admin web / mobile app). Requires sales_order:read.</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            if (!_userContext.HasPermission("sales_order:read"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });

            page = Math.Max(1, page);
            pageSize = Math.Min(100, Math.Max(1, pageSize));
            var (rows, total) = await _profiles.ListAsync(CreditProfileSearch.Parse(search), (page - 1) * pageSize, pageSize, ct);
            return Ok(new CreditProfilePage(rows.Select(CustomerSalesProfileDto.From).ToList(), total, page, pageSize));
        }

        [HttpGet("{customerId:guid}")]
        public async Task<IActionResult> Get([FromRoute] Guid customerId, CancellationToken ct)
        {
            if (!_userContext.HasPermission("sales_order:read"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });

            var profile = await _profiles.GetByCustomerIdAsync(customerId, ct);
            if (profile == null)
                return NotFound(new { message = "No credit profile configured for this customer — treated as no limit." });

            var name = await _profiles.GetCustomerNameAsync(customerId, ct);
            return Ok(CustomerSalesProfileDto.From(new CreditProfileRow(profile.CustomerId, name, profile.CreditLimit, profile.CurrentBalance, profile.UpdatedAt)));
        }

        [HttpPut("{customerId:guid}")]
        public async Task<IActionResult> Set([FromRoute] Guid customerId, [FromBody] SetCreditLimitRequest request, CancellationToken ct)
        {
            if (!_userContext.HasPermission("sales_order:approve"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:approve required" });

            var profile = await _profiles.GetByCustomerIdAsync(customerId, ct);
            if (profile == null)
            {
                profile = CustomerSalesProfile.Create(customerId, request.CreditLimit);
                await _profiles.AddAsync(profile, ct);
            }
            else
            {
                profile.SetCreditLimit(request.CreditLimit);
                await _profiles.UpdateAsync(profile, ct);
            }

            var customerName = await _profiles.GetCustomerNameAsync(customerId, ct);
            return Ok(CustomerSalesProfileDto.From(new CreditProfileRow(profile.CustomerId, customerName, profile.CreditLimit, profile.CurrentBalance, profile.UpdatedAt)));
        }
    }

    public record CustomerSalesProfileDto(Guid CustomerId, string CustomerName, decimal CreditLimit, decimal CurrentBalance, decimal AvailableCredit, DateTime UpdatedAt)
    {
        public static CustomerSalesProfileDto From(CreditProfileRow r) => new(r.CustomerId, r.CustomerName, r.CreditLimit, r.CurrentBalance, r.AvailableCredit, r.UpdatedAt);
    }

    public record CreditProfilePage(IReadOnlyList<CustomerSalesProfileDto> Items, int TotalCount, int PageNumber, int PageSize);
    public record SetCreditLimitRequest(decimal CreditLimit);
}
