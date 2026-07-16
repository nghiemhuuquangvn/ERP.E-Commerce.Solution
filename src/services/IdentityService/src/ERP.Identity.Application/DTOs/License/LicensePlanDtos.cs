using System;

namespace ERP.Identity.Application.DTOs.License
{
    public record LicensePlanDto(
        Guid Id,
        string PlanCode,
        string PlanName,
        string? Description,
        int? MaxUsers,
        bool IsActive,
        string[] ModuleCodes);

    /// <summary>PlanCode deliberately not included — see UpsertModuleRequest's note.</summary>
    public record UpsertLicensePlanRequest(
        string PlanName,
        string? Description,
        int? MaxUsers,
        bool IsActive,
        string[] ModuleCodes);
}
