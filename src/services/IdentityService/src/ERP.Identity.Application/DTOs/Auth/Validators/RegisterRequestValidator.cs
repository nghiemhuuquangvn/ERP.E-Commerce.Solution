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

        // Anonymous self-registration may only create Customers (AuthService also forces
        // this) — Staff/Admin accounts are created via the admin-gated POST /users/invite.
        RuleFor(x => x.UserType)
            .Equal(UserType.Customer).WithMessage("Self-registration is only available for customer accounts.");

        // No TenantCode rule: AuthController overwrites it with the tenant of the request
        // host (set by the Gateway) — a body value is never trusted.

        RuleFor(x => x.UserName)
            .MaximumLength(100).WithMessage(_ => localizer["Validation_MaxLength", "Username", 100].Value)
            .When(x => !string.IsNullOrEmpty(x.UserName));
    }
}
