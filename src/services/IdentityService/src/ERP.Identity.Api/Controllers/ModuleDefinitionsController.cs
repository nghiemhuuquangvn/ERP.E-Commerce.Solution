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
    /// System catalog of the 14 licensable backend module codes. GET is open to any
    /// authenticated caller (needed to populate module pickers in the Plan/License UI);
    /// only upsert is SuperAdmin-gated.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/license/modules")]
    [Authorize]
    public class ModuleDefinitionsController : ControllerBase
    {
        private readonly ILicenseService _licenseService;

        public ModuleDefinitionsController(ILicenseService licenseService)
        {
            _licenseService = licenseService;
        }

        [HttpGet]
        public async Task<IActionResult> GetModules(CancellationToken ct = default)
        {
            var result = await _licenseService.GetModulesAsync(ct);
            return Ok(result);
        }

        [HttpPut("{moduleCode}")]
        [Authorize(Policy = "Permission")]
        [RequirePermission(LicensePermissions.ModulesManage)]
        public async Task<IActionResult> UpsertModule([FromRoute] string moduleCode, [FromBody] UpsertModuleRequest request, CancellationToken ct = default)
        {
            var result = await _licenseService.UpsertModuleAsync(moduleCode, request, ct);
            return Ok(result);
        }
    }
}
