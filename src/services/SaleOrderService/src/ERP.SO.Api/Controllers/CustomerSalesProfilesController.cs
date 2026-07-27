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

        [HttpGet("{customerId:guid}")]
        public async Task<IActionResult> Get([FromRoute] Guid customerId, CancellationToken ct)
        {
            if (!_userContext.HasPermission("sales_order:read"))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Insufficient permissions: sales_order:read required" });

            var profile = await _profiles.GetByCustomerIdAsync(customerId, ct);
            if (profile == null)
                return NotFound(new { message = "No credit profile configured for this customer — treated as no limit." });

            return Ok(new CustomerSalesProfileDto(profile.CustomerId, profile.CreditLimit, profile.CurrentBalance, profile.UpdatedAt));
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

            return Ok(new CustomerSalesProfileDto(profile.CustomerId, profile.CreditLimit, profile.CurrentBalance, profile.UpdatedAt));
        }
    }

    public record CustomerSalesProfileDto(Guid CustomerId, decimal CreditLimit, decimal CurrentBalance, DateTime UpdatedAt);
    public record SetCreditLimitRequest(decimal CreditLimit);
}
