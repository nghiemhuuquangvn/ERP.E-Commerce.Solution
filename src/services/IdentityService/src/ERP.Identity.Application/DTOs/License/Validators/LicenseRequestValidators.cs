using ERP.Identity.Application.Interfaces.Repositories;
using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Application.DTOs.License.Validators;

public class UpsertModuleRequestValidator : AbstractValidator<UpsertModuleRequest>
{
    public UpsertModuleRequestValidator()
    {
        RuleFor(x => x.ModuleName)
            .NotEmpty().WithMessage("Module name is required.")
            .MaximumLength(200).WithMessage("Module name must not exceed 200 characters.");
    }
}

/// <summary>
/// Shared by every validator below — module codes are validated against the
/// ModuleDefinitions catalog (a real, admin-managed DB table via
/// ModuleDefinitionsController/LicenseService.UpsertModuleAsync), not a hardcoded
/// compile-time list. This is what makes IdentityService's licensing model reusable
/// by a project other than this ERP: a different deployment just seeds its own
/// ModuleDefinitions rows, no code change needed here.
/// </summary>
internal static class ModuleCodeValidation
{
    public static async Task<bool> AllExistAsync(IUnitOfWork unitOfWork, IEnumerable<string>? codes, CancellationToken ct)
    {
        if (codes == null)
            return true;

        foreach (var code in codes)
        {
            if (await unitOfWork.ModuleDefinitions.GetByCodeAsync(code.ToUpperInvariant(), ct) == null)
                return false;
        }

        return true;
    }
}

public class UpsertLicensePlanRequestValidator : AbstractValidator<UpsertLicensePlanRequest>
{
    public UpsertLicensePlanRequestValidator(IUnitOfWork unitOfWork)
    {
        RuleFor(x => x.PlanName)
            .NotEmpty().WithMessage("Plan name is required.")
            .MaximumLength(200).WithMessage("Plan name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.MaxUsers)
            .GreaterThan(0).WithMessage("Max users must be greater than 0 when specified.")
            .When(x => x.MaxUsers.HasValue);

        RuleFor(x => x.ModuleCodes)
            .NotNull().WithMessage("Module codes are required.")
            .MustAsync((codes, ct) => ModuleCodeValidation.AllExistAsync(unitOfWork, codes, ct))
            .WithMessage("One or more module codes are not registered in the module catalog — create them first via the modules endpoint.");
    }
}

public class IssueLicenseRequestValidator : AbstractValidator<IssueLicenseRequest>
{
    public IssueLicenseRequestValidator(IUnitOfWork unitOfWork)
    {
        RuleFor(x => x.MaxUsers)
            .GreaterThan(0).WithMessage("Max users must be greater than 0 when specified.")
            .When(x => x.MaxUsers.HasValue);

        RuleFor(x => x.ModuleCodes)
            .MustAsync((codes, ct) => ModuleCodeValidation.AllExistAsync(unitOfWork, codes, ct))
            .WithMessage("One or more module codes are not registered in the module catalog — create them first via the modules endpoint.");

        RuleFor(x => x)
            .Must(x => x.ExpiresAt == null || x.StartsAt == null || x.ExpiresAt > x.StartsAt)
            .WithMessage("ExpiresAt must be after StartsAt.")
            .OverridePropertyName(nameof(IssueLicenseRequest.ExpiresAt));
    }
}

public class RenewLicenseRequestValidator : AbstractValidator<RenewLicenseRequest>
{
    public RenewLicenseRequestValidator(IUnitOfWork unitOfWork)
    {
        RuleFor(x => x.MaxUsers)
            .GreaterThan(0).WithMessage("Max users must be greater than 0 when specified.")
            .When(x => x.MaxUsers.HasValue);

        RuleFor(x => x.ModuleCodes)
            .MustAsync((codes, ct) => ModuleCodeValidation.AllExistAsync(unitOfWork, codes, ct))
            .WithMessage("One or more module codes are not registered in the module catalog — create them first via the modules endpoint.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("ExpiresAt must be in the future.")
            .When(x => x.ExpiresAt.HasValue);
    }
}
