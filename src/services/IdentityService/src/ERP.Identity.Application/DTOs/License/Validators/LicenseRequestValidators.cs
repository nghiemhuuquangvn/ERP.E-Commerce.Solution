using ERP.Shared.Authorization;
using FluentValidation;
using System.Linq;

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

public class UpsertLicensePlanRequestValidator : AbstractValidator<UpsertLicensePlanRequest>
{
    public UpsertLicensePlanRequestValidator()
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
            .Must(codes => codes.All(c => ModuleCodes.All.Contains(c)))
            .WithMessage($"Module codes must be one of: {string.Join(", ", ModuleCodes.All)}.");
    }
}

public class IssueLicenseRequestValidator : AbstractValidator<IssueLicenseRequest>
{
    public IssueLicenseRequestValidator()
    {
        RuleFor(x => x.MaxUsers)
            .GreaterThan(0).WithMessage("Max users must be greater than 0 when specified.")
            .When(x => x.MaxUsers.HasValue);

        RuleFor(x => x.ModuleCodes)
            .Must(codes => codes == null || codes.All(c => ModuleCodes.All.Contains(c)))
            .WithMessage($"Module codes must be one of: {string.Join(", ", ModuleCodes.All)}.");

        RuleFor(x => x)
            .Must(x => x.ExpiresAt == null || x.StartsAt == null || x.ExpiresAt > x.StartsAt)
            .WithMessage("ExpiresAt must be after StartsAt.")
            .OverridePropertyName(nameof(IssueLicenseRequest.ExpiresAt));
    }
}

public class RenewLicenseRequestValidator : AbstractValidator<RenewLicenseRequest>
{
    public RenewLicenseRequestValidator()
    {
        RuleFor(x => x.MaxUsers)
            .GreaterThan(0).WithMessage("Max users must be greater than 0 when specified.")
            .When(x => x.MaxUsers.HasValue);

        RuleFor(x => x.ModuleCodes)
            .Must(codes => codes == null || codes.All(c => ModuleCodes.All.Contains(c)))
            .WithMessage($"Module codes must be one of: {string.Join(", ", ModuleCodes.All)}.");
    }
}
