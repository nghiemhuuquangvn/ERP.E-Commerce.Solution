using ERP.CRM.Application.Commands;
using ERP.Shared.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.CRM.Application.EventHandlers;

/// <summary>
/// Integration event consumed from IdentityService.
/// When a new user registers, we create a CRM Contact automatically.
/// </summary>
public record UserRegisteredIntegrationEvent(Guid UserId, string Email, string Name);

public class UserCreatedEventHandler : IEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(IMediator mediator, ILogger<UserCreatedEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received UserRegisteredIntegrationEvent for UserId={UserId}, Email={Email}",
            @event.UserId, @event.Email);

        var command = new CreateContactCommand(
            Name: @event.Name,
            Email: @event.Email,
            Phone: null,
            Company: null,
            UserId: @event.UserId,
            ContactSource: 1 // UserRegistration
        );

        var contact = await _mediator.Send(command, cancellationToken);

        _logger.LogInformation(
            "CRM Contact {ContactId} created from user registration UserId={UserId}",
            contact.Id, @event.UserId);
    }
}
