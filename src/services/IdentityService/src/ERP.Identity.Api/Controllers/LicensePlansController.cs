using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.Identity.Application.DTOs.License;
using ERP.Identity.Application.Interfaces.Services;
using ERP.Identity.Application.Authorization;
using ERP.Identity.Api.Authorization;

namespace ERP.Identity.Api.Controllers
{
    /// <summary>
    /// SuperAdmin-managed catalog of reusable license plans (e.g. Starter/Professional/
    /// Enterprise). A TenantLicense issued from a plan copies its module set + MaxUsers as
    /// a starting snapshot — editing a plan afterwards does not retroactively change
    /// already-issued tenant licenses.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/license/plans")]
    [Authorize]
    public class LicensePlansController : ControllerBase
    {
        private readonly ILicenseService _licenseService;

        public LicensePlansController(ILicenseService licenseService)
        {
            _licenseService = licenseService;
        }

        [HttpGet]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LicensePermissions.PlansView)]
        public async Task<IActionResult> GetPlans(CancellationToken ct = default)
        {
            var result = await _licenseService.GetPlansAsync(ct);
            return Ok(result);
        }

        [HttpPut("{planCode}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LicensePermissions.PlansManage)]
        public async Task<IActionResult> UpsertPlan([FromRoute] string planCode, [FromBody] UpsertLicensePlanRequest request, CancellationToken ct = default)
        {
            var result = await _licenseService.UpsertPlanAsync(planCode, request, ct);
            return Ok(result);
        }
    }
}
