using ERP.HRM.Application.Commands;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.HRM.Application.Handlers;

// Keeps the local EmployeeSnapshot projection in sync with SharedMasterDataService's
// Employee aggregate, driven by MasterDataEventConsumer (see Infrastructure/BackgroundJobs).

public class UpsertEmployeeSnapshotCommandHandler : IRequestHandler<UpsertEmployeeSnapshotCommand, Unit>
{
    private readonly IEmployeeSnapshotRepository _repository;
    private readonly ILogger<UpsertEmployeeSnapshotCommandHandler> _logger;

    public UpsertEmployeeSnapshotCommandHandler(
        IEmployeeSnapshotRepository repository,
        ILogger<UpsertEmployeeSnapshotCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpsertEmployeeSnapshotCommand request, CancellationToken cancellationToken)
    {
        var snapshot = EmployeeSnapshot.Create(
            request.EmployeeId, request.FullName, request.EmploymentStatus, request.DepartmentId,
            request.EmployeeNumber, request.Email);
        _repository.Upsert(snapshot);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee snapshot upserted: {EmployeeId}", request.EmployeeId);
        return Unit.Value;
    }
}

public class MarkEmployeeSnapshotTerminatedCommandHandler : IRequestHandler<MarkEmployeeSnapshotTerminatedCommand, Unit>
{
    private readonly IEmployeeSnapshotRepository _repository;
    private readonly ILogger<MarkEmployeeSnapshotTerminatedCommandHandler> _logger;

    public MarkEmployeeSnapshotTerminatedCommandHandler(
        IEmployeeSnapshotRepository repository,
        ILogger<MarkEmployeeSnapshotTerminatedCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Unit> Handle(MarkEmployeeSnapshotTerminatedCommand request, CancellationToken cancellationToken)
    {
        var snapshot = await _repository.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (snapshot == null)
        {
            _logger.LogWarning(
                "Received EmployeeTerminated for {EmployeeId} but no local snapshot exists yet — ignoring.",
                request.EmployeeId);
            return Unit.Value;
        }

        snapshot.EmploymentStatus = (int)Domain.Enums.EmploymentStatus.Terminated;
        snapshot.UpdatedAt = DateTime.UtcNow;
        _repository.Upsert(snapshot);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee snapshot marked terminated: {EmployeeId}", request.EmployeeId);
        return Unit.Value;
    }
}

public class UpdateEmployeeSnapshotDepartmentCommandHandler : IRequestHandler<UpdateEmployeeSnapshotDepartmentCommand, Unit>
{
    private readonly IEmployeeSnapshotRepository _repository;
    private readonly ILogger<UpdateEmployeeSnapshotDepartmentCommandHandler> _logger;

    public UpdateEmployeeSnapshotDepartmentCommandHandler(
        IEmployeeSnapshotRepository repository,
        ILogger<UpdateEmployeeSnapshotDepartmentCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateEmployeeSnapshotDepartmentCommand request, CancellationToken cancellationToken)
    {
        var snapshot = await _repository.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (snapshot == null)
        {
            _logger.LogWarning(
                "Received EmployeeTransferred for {EmployeeId} but no local snapshot exists yet — ignoring.",
                request.EmployeeId);
            return Unit.Value;
        }

        snapshot.DepartmentId = request.NewDepartmentId;
        snapshot.UpdatedAt = DateTime.UtcNow;
        _repository.Upsert(snapshot);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee snapshot department updated: {EmployeeId}", request.EmployeeId);
        return Unit.Value;
    }
}
