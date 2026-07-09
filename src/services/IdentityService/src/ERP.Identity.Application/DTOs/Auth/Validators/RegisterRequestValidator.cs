using FluentValidation;
using static ERP.Identity.Application.DTOs.Auth.RegisterDto;

namespace ERP.Identity.Application.DTOs.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters.");

        RuleFor(x => x.TenantCode)
            .NotEmpty().WithMessage("Tenant code is required.");

        RuleFor(x => x.UserName)
            .MaximumLength(100).WithMessage("Username must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.UserName));
    }
}
