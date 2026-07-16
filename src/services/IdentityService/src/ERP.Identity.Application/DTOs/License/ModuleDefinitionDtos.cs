namespace ERP.Identity.Application.DTOs.License
{
    public record ModuleDefinitionDto(string ModuleCode, string ModuleName, bool IsActive);

    /// <summary>
    /// ModuleCode deliberately not included here — it comes from the route
    /// ({moduleCode}), same as UpsertSystemFeatureFlagRequest's Key, so
    /// AddFluentValidationAutoValidation() never has to validate a body field that's
    /// redundant with (and could otherwise conflict with) the URL segment.
    /// </summary>
    public record UpsertModuleRequest(string ModuleName, bool IsActive);
}
