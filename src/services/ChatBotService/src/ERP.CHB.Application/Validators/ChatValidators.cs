using ERP.CHB.Application.Commands;
using FluentValidation;

namespace ERP.CHB.Application.Validators;

public class StartConversationCommandValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationCommandValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.GuestSessionId).MaximumLength(200);
    }
}

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
    }
}
