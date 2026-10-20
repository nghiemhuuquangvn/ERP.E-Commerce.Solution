using ERP.Identity.Application.Security;
using FluentValidation;

namespace ERP.Identity.Application.DTOs.Webhook.Validators;

public class CreateWebhookEndpointRequestValidator : AbstractValidator<CreateWebhookEndpointRequest>
{
    public CreateWebhookEndpointRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.")
            .Must(WebhookUrlRules.IsAllowedUrl)
            .WithMessage("URL must be an absolute https URL to a public host (internal, private-network and IP-literal destinations are not allowed).")
            .MaximumLength(2000).WithMessage("URL must not exceed 2000 characters.");

        RuleFor(x => x.Secret)
            .MaximumLength(200).WithMessage("Secret must not exceed 200 characters.");
    }
}

public class UpdateWebhookEndpointRequestValidator : AbstractValidator<UpdateWebhookEndpointRequest>
{
    public UpdateWebhookEndpointRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.")
            .Must(WebhookUrlRules.IsAllowedUrl)
            .WithMessage("URL must be an absolute https URL to a public host (internal, private-network and IP-literal destinations are not allowed).")
            .MaximumLength(2000).WithMessage("URL must not exceed 2000 characters.");

        RuleFor(x => x.Secret)
            .MaximumLength(200).WithMessage("Secret must not exceed 200 characters.");
    }
}
