using ERP.Shared.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;
using static ERP.Identity.Application.DTOs.Auth.TenantSignupDto;

namespace ERP.Identity.Application.DTOs.Auth.Validators;

public class TenantSignupRequestValidator : AbstractValidator<TenantSignupRequest>
{
    public TenantSignupRequestValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.TenantCode)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Tenant code"].Value)
            .MaximumLength(256).WithMessage(_ => localizer["Validation_MaxLength", "Tenant code", 256].Value)
            .Matches("^[A-Za-z0-9_-]+$").WithMessage(_ => localizer["Validation_InvalidFormat", "Tenant code"].Value);

        RuleFor(x => x.TenantName)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Tenant name"].Value)
            .MaximumLength(256).WithMessage(_ => localizer["Validation_MaxLength", "Tenant name", 256].Value);

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Admin email"].Value)
            .EmailAddress().WithMessage(_ => localizer["Validation_InvalidFormat", "Admin email"].Value)
            .MaximumLength(256).WithMessage(_ => localizer["Validation_MaxLength", "Admin email", 256].Value);

        RuleFor(x => x.AdminPassword)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Admin password"].Value)
            .MaximumLength(128).WithMessage(_ => localizer["Validation_MaxLength", "Admin password", 128].Value);

        RuleFor(x => x.AdminFullName)
            .MaximumLength(100).WithMessage(_ => localizer["Validation_MaxLength", "Admin full name", 100].Value)
            .When(x => !string.IsNullOrEmpty(x.AdminFullName));
    }
}
