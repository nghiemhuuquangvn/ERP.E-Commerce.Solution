using FluentValidation;
using static ERP.Identity.Application.DTOs.Auth.LoginDto;

namespace ERP.Identity.Application.DTOs.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");

        RuleFor(x => x.TenantCode)
            .NotEmpty().WithMessage("Tenant code is required.");
    }
}
