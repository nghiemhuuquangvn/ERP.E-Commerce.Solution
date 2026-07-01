using ERP.MST.Application.Commands;
using ERP.Shared.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.MST.Application.EventHandlers;

/// <summary>
/// Integration event contract for UserCreated consumed from Identity Service.
/// </summary>
public class UserRegisteredIntegrationEvent : IntegrationEventBase
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEmployee { get; set; }
    public Guid? DefaultDepartmentId { get; set; }
}

/// <summary>
/// Handles UserRegistered integration events from the Identity Service.
/// When a user is registered with IsEmployee=true, automatically creates an employee record.
/// </summary>
public class UserCreatedEventHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<UserCreatedEventHandler> _logger;

    public UserCreatedEventHandler(IMediator mediator, ILogger<UserCreatedEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Received UserRegistered event for user {UserId} ({Email}), IsEmployee={IsEmployee}",
            @event.UserId, @event.Email, @event.IsEmployee);

        if (!@event.IsEmployee)
        {
            _logger.LogInformation(
                "User {UserId} is not an employee — skipping automatic employee record creation.",
                @event.UserId);
            return;
        }

        if (@event.DefaultDepartmentId == null || @event.DefaultDepartmentId == Guid.Empty)
        {
            _logger.LogWarning(
                "User {UserId} is flagged as employee but no DefaultDepartmentId provided — skipping.",
                @event.UserId);
            return;
        }

        // Parse first/last name from full name
        var parts = @event.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.Length > 0 ? parts[0] : "Unknown";
        var lastName = parts.Length > 1 ? parts[1] : string.Empty;

        var command = new HireEmployeeCommand(
            FirstName: firstName,
            LastName: lastName,
            Email: @event.Email,
            Phone: null,
            DepartmentId: @event.DefaultDepartmentId.Value,
            DateOfHire: DateTime.UtcNow,
            EmploymentType: 0 // FullTime by default
        );

        try
        {
            var employeeDto = await _mediator.Send(command, cancellationToken);
            _logger.LogInformation(
                "Employee record created for user {UserId}: {EmployeeId} ({EmployeeNumber})",
                @event.UserId, employeeDto.Id, employeeDto.EmployeeNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create employee record for user {UserId}",
                @event.UserId);
        }
    }
}
