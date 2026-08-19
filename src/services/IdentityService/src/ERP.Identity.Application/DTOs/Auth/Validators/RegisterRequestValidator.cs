using ERP.Shared.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;
using static ERP.Identity.Application.DTOs.Auth.RegisterDto;

namespace ERP.Identity.Application.DTOs.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(IStringLocalizer<SharedResource> localizer)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Email"].Value)
            .EmailAddress().WithMessage(_ => localizer["Validation_InvalidFormat", "Email"].Value)
            .MaximumLength(256).WithMessage(_ => localizer["Validation_MaxLength", "Email", 256].Value);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Password"].Value)
            .MaximumLength(128).WithMessage(_ => localizer["Validation_MaxLength", "Password", 128].Value);

        RuleFor(x => x.TenantCode)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Tenant code"].Value);

        RuleFor(x => x.UserName)
            .MaximumLength(100).WithMessage(_ => localizer["Validation_MaxLength", "Username", 100].Value)
            .When(x => !string.IsNullOrEmpty(x.UserName));
    }
}
