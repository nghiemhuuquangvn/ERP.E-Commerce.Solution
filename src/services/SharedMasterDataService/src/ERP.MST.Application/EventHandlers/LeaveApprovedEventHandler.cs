using ERP.MST.Application.Commands;
using ERP.Shared.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.MST.Application.EventHandlers;

/// <summary>
/// Local copy of HRMService's LeaveApprovedEvent shape — deserialized from "hrm-topic"
/// without referencing ERP.HRM.Domain directly (same pattern as every other
/// cross-service integration event contract in this codebase).
/// </summary>
public class LeaveApprovedIntegrationEvent : IntegrationEventBase
{
    public Guid LeaveRequestId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Closes the loop with HRM: HRM approves leave locally and can only update its own
/// EmployeeSnapshot cache, since it no longer owns Employee. This handler sets the
/// canonical Employee's status here, in the service that actually owns it.
/// </summary>
public class LeaveApprovedEventHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<LeaveApprovedEventHandler> _logger;

    public LeaveApprovedEventHandler(IMediator mediator, ILogger<LeaveApprovedEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(LeaveApprovedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Received LeaveApproved for employee {EmployeeId}, leave request {LeaveRequestId}",
            @event.EmployeeId, @event.LeaveRequestId);

        if (@event.StartDate.Date > DateTime.UtcNow.Date)
        {
            _logger.LogInformation(
                "Leave for employee {EmployeeId} starts in the future ({StartDate}) — not setting OnLeave yet.",
                @event.EmployeeId, @event.StartDate);
            return;
        }

        await _mediator.Send(new SetEmployeeOnLeaveCommand(@event.EmployeeId), cancellationToken);
    }
}
