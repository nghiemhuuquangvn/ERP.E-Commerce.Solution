using ERP.Shared.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;
using static ERP.Identity.Application.DTOs.Auth.LoginDto;

namespace ERP.Identity.Application.DTOs.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator(IStringLocalizer<SharedResource> localizer)
    {
        // .WithMessage(x => ...) — a lambda, not a plain string — so the localized text is
        // resolved at *validation* time (reading CultureInfo.CurrentUICulture as set by
        // RequestLocalizationMiddleware for the current request), not once when this
        // validator is constructed/cached.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Email"].Value)
            .EmailAddress().WithMessage(_ => localizer["Validation_InvalidFormat", "Email"].Value);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_ => localizer["Validation_Required", "Password"].Value);
    }
}
