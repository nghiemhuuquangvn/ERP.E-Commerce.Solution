using FluentValidation;

namespace ERP.Identity.Application.DTOs.Auth.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MaximumLength(128).WithMessage("New password must not exceed 128 characters.");
    }
}
