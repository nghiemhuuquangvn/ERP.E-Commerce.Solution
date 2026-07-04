using ERP.HRM.Application.Commands;
using ERP.Shared.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.HRM.Application.EventHandlers;

// Local copies of SharedMasterDataService's Employee lifecycle event shapes — HRM
// deserializes "mst-topic" messages into these rather than referencing ERP.MST.Domain
// directly (same pattern already used for UserRegisteredIntegrationEvent/Identity).

public class EmployeeHiredIntegrationEvent : IntegrationEventBase
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid DepartmentId { get; set; }
}

public class EmployeeTerminatedIntegrationEvent : IntegrationEventBase
{
    public Guid EmployeeId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class EmployeeTransferredIntegrationEvent : IntegrationEventBase
{
    public Guid EmployeeId { get; set; }
    public Guid OldDepartmentId { get; set; }
    public Guid NewDepartmentId { get; set; }
}

/// <summary>
/// Keeps HRM's local EmployeeSnapshot projection in sync with SharedMasterDataService's
/// Employee aggregate. Invoked by MasterDataEventConsumer (see Infrastructure/BackgroundJobs)
/// for every message read from "mst-topic".
/// </summary>
public class EmployeeMasterDataEventHandlers
{
    private readonly IMediator _mediator;
    private readonly ILogger<EmployeeMasterDataEventHandlers> _logger;

    public EmployeeMasterDataEventHandlers(IMediator mediator, ILogger<EmployeeMasterDataEventHandlers> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(EmployeeHiredIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received EmployeeHired for {EmployeeId}", @event.EmployeeId);
        await _mediator.Send(
            new UpsertEmployeeSnapshotCommand(
                @event.EmployeeId, @event.FullName, EmploymentStatus: 0, @event.DepartmentId,
                @event.EmployeeNumber, @event.Email),
            cancellationToken);
    }

    public async Task HandleAsync(EmployeeTerminatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received EmployeeTerminated for {EmployeeId}", @event.EmployeeId);
        await _mediator.Send(new MarkEmployeeSnapshotTerminatedCommand(@event.EmployeeId), cancellationToken);
    }

    public async Task HandleAsync(EmployeeTransferredIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received EmployeeTransferred for {EmployeeId}", @event.EmployeeId);
        await _mediator.Send(
            new UpdateEmployeeSnapshotDepartmentCommand(@event.EmployeeId, @event.NewDepartmentId),
            cancellationToken);
    }
}
