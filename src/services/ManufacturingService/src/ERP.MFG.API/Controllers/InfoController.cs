using Asp.Versioning;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

/// <summary>Who the service is - a signed-in check that the route, license and permission plumbing work end to end.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/info")]
public class InfoController : ControllerBase
{
    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public IActionResult Get() => Ok(new { service = "ManufacturingService", module = ModuleCodes.Manufacturing, version = "0.1" });
}
