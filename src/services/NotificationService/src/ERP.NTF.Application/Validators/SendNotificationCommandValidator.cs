using ERP.NTF.Application.Commands;
using ERP.NTF.Domain.Enums;
using FluentValidation;

namespace ERP.NTF.Application.Validators;

public class SendNotificationCommandValidator : AbstractValidator<SendNotificationCommand>
{
    public SendNotificationCommandValidator()
    {
        RuleFor(x => x.RecipientId).NotEmpty();
        RuleFor(x => x.RecipientEmail).NotEmpty().EmailAddress()
            .When(x => x.Channel == NotificationChannel.Email);
        RuleFor(x => x.RecipientPhone).NotEmpty()
            .When(x => x.Channel == NotificationChannel.Sms);
        RuleFor(x => x.RecipientDeviceToken).NotEmpty()
            .When(x => x.Channel == NotificationChannel.Push);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500)
            .When(x => string.IsNullOrEmpty(x.TemplateCode));
        RuleFor(x => x.Body).NotEmpty()
            .When(x => string.IsNullOrEmpty(x.TemplateCode));
    }
}

public class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Body).NotEmpty();
    }
}
