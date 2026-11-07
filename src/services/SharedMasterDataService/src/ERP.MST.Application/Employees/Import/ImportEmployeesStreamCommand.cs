using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Domain.Events;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Streaming;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.MST.Application.Employees.Import;

/// <summary>
/// Imports employees from a CSV that is still arriving (docs/architecture/streaming-import-pattern.md). The employee list belongs to MST;
/// each created row raises EmployeeHired (source "import"), so HRM's snapshot follows through Kafka. No login is created.
/// </summary>
public class ImportEmployeesStreamCommand : IStreamRequest<ImportLine>
{
    public Stream Body { get; set; } = Stream.Null;
    public long? ContentLength { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Guid ImportId { get; set; }
    public bool DryRun { get; set; }
}

public class ImportEmployeesStreamHandler : IStreamRequestHandler<ImportEmployeesStreamCommand, ImportLine>
{
    /// <summary>Generated numbers look like EMP-2026-00001 (5 digits), which cannot collide with the random EMP-2026-1234 numbers of the hire form.</summary>
    public const int GeneratedDigits = 5;

    private readonly IEmployeeRepository _employees;
    private readonly IDepartmentRepository _departments;
    private readonly IPositionRepository _positions;
    private readonly IImportProgressRegistry _registry;
    private readonly ImportLimits _limits;
    private readonly ILogger<ImportEmployeesStreamHandler> _logger;
    private readonly TimeProvider _clock;

    public ImportEmployeesStreamHandler(
        IEmployeeRepository employees, IDepartmentRepository departments, IPositionRepository positions,
        IImportProgressRegistry registry, IOptions<ImportLimits> limits, ILogger<ImportEmployeesStreamHandler> logger, TimeProvider? clock = null)
    {
        _employees = employees;
        _departments = departments;
        _positions = positions;
        _registry = registry;
        _limits = limits.Value;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    private sealed class RunState
    {
        public readonly HashSet<string> SeenNumbers = new(StringComparer.Ordinal);
        public readonly HashSet<string> SeenEmails = new(StringComparer.Ordinal);
        public readonly Dictionary<int, int> Sequences = new();   // per hire year, loaded when the first row of that year needs a generated number
    }

    public async IAsyncEnumerable<ImportLine> Handle(
        ImportEmployeesStreamCommand request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var session = _registry.BeginRun(request.TenantId, request.UserId, request.ImportId);
        var counter = new ImportBytesCounter { TotalBytes = request.ContentLength };
        var run = new ImportRun(session, counter, request.DryRun);
        var finished = false;

        try
        {
            await using var rows = CsvImportReader.ReadAsync(request.Body, EmployeeImportParser.RequiredColumns, _limits, counter, ct).GetAsyncEnumerator(ct);

            var hasRow = await rows.MoveNextAsync();   // header + first row: a wrong header is still a plain 400
            yield return run.Started();

            var state = new RunState();
            var batch = new List<CsvImportRow>(_limits.BatchSize);
            string? readError = null;

            while (hasRow)
            {
                batch.Add(rows.Current);
                try
                {
                    hasRow = await rows.MoveNextAsync();
                }
                catch (BusinessRuleException ex)
                {
                    readError = ex.Message;
                    hasRow = false;
                }

                if (batch.Count < _limits.BatchSize && hasRow) continue;

                var outcome = await ProcessBatchAsync(batch, state, run, ct);
                foreach (var line in outcome.Lines) yield return line;
                run.PublishProgress();
                batch.Clear();

                if (outcome.Fatal != null)
                {
                    run.Fail(outcome.Fatal);
                    finished = true;
                    yield return new ImportErrorLine(outcome.Fatal);
                    yield break;
                }
            }

            if (readError != null)
            {
                run.Fail(readError);
                finished = true;
                yield return new ImportErrorLine(readError);
                yield break;
            }

            finished = true;
            yield return run.Complete();
        }
        finally
        {
            if (!finished) run.Fail("The import was interrupted.");
        }
    }

    private sealed record BatchOutcome(List<ImportRowLine> Lines, string? Fatal);

    private async Task<BatchOutcome> ProcessBatchAsync(List<CsvImportRow> batch, RunState state, ImportRun run, CancellationToken ct)
    {
        var lines = new List<ImportRowLine>();
        var today = _clock.GetUtcNow().UtcDateTime.Date;
        var candidates = new List<EmployeeImportData>();

        foreach (var row in batch)
        {
            if (row.ParseError != null)
            {
                lines.Add(run.Fail(row.Line, null, row.ParseError));
                continue;
            }

            var parsed = EmployeeImportParser.Parse(row, today);
            if (parsed.Data == null)
            {
                lines.Add(run.Fail(row.Line, parsed.Key, parsed.Error ?? "The row is invalid."));
                continue;
            }

            var data = parsed.Data;
            if (data.EmployeeNumber != null && !state.SeenNumbers.Add(data.EmployeeNumber))
            {
                lines.Add(run.Skip(row.Line, data.Key, "The employee number repeats an earlier row of this file."));
                continue;
            }
            if (data.Email != null && !state.SeenEmails.Add(data.Email))
            {
                lines.Add(run.Skip(row.Line, data.Key, "The e-mail repeats an earlier row of this file."));
                continue;
            }
            candidates.Add(data);
        }

        // One query per kind for the whole batch.
        var existingEmails = await _employees.GetExistingEmailsAsync(candidates.Where(c => c.Email != null).Select(c => c.Email!).ToList(), ct);
        var existingNumbers = await _employees.GetExistingNumbersAsync(candidates.Where(c => c.EmployeeNumber != null).Select(c => c.EmployeeNumber!).ToList(), ct);
        var departments = await _departments.GetIdsByCodesAsync(candidates.Select(c => c.DepartmentCode).Distinct().ToList(), ct);
        var positions = await _positions.GetIdsByCodesAsync(candidates.Where(c => c.PositionCode != null).Select(c => c.PositionCode!).Distinct().ToList(), ct);

        var accepted = new List<(EmployeeImportData Data, Guid DepartmentId, Guid? PositionId)>();
        foreach (var candidate in candidates)
        {
            if (candidate.EmployeeNumber != null && existingNumbers.Contains(candidate.EmployeeNumber))
            {
                lines.Add(run.Skip(candidate.Line, candidate.Key, "An employee with this number already exists."));
                continue;
            }
            if (candidate.Email != null && existingEmails.Contains(candidate.Email))
            {
                lines.Add(run.Skip(candidate.Line, candidate.Key, "An employee with this e-mail already exists."));
                continue;
            }
            if (!departments.TryGetValue(candidate.DepartmentCode, out var departmentId))
            {
                lines.Add(run.Fail(candidate.Line, candidate.Key, $"Unknown department code '{candidate.DepartmentCode}'."));
                continue;
            }
            Guid? positionId = null;
            if (candidate.PositionCode != null)
            {
                if (!positions.TryGetValue(candidate.PositionCode, out var id))
                {
                    lines.Add(run.Fail(candidate.Line, candidate.Key, $"Unknown position code '{candidate.PositionCode}'."));
                    continue;
                }
                positionId = id;
            }
            accepted.Add((candidate, departmentId, positionId));
        }

        if (accepted.Count == 0)
            return new BatchOutcome(lines.OrderBy(l => l.Line).ToList(), null);

        // Employee numbers: the given one, or the next free one of the year's sequence.
        var numbered = new List<(EmployeeImportData Data, Guid DepartmentId, Guid? PositionId, string Number)>();
        foreach (var (data, departmentId, positionId) in accepted)
        {
            var number = data.EmployeeNumber ?? await NextNumberAsync(data, state, ct);
            numbered.Add((data, departmentId, positionId, number));
        }

        if (!run.DryRun)
        {
            try
            {
                foreach (var (data, departmentId, positionId, number) in numbered)
                {
                    var employee = Employee.Create(number, data.FirstName, data.LastName, departmentId, data.DateOfHire, (int)data.EmploymentType, positionId);
                    employee.UpdateProfile(data.Email, data.Phone);
                    employee.RaiseHiredEvent(EmployeeHiredEvent.SourceImport);
                    _employees.Add(employee);
                }
                await _employees.SaveChangesAsync(ct);   // the rows and their outbox events commit together
                _employees.ClearTracking();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An employee import batch of {Count} rows could not be saved", numbered.Count);
                _employees.ClearTracking();
                foreach (var (data, _, _, _) in numbered)
                    lines.Add(run.Fail(data.Line, data.Key, "Not saved: the batch was rolled back."));
                return new BatchOutcome(lines.OrderBy(l => l.Line).ToList(), "The import stopped: a batch could not be saved. Rows reported as created before this point are saved.");
            }
        }

        foreach (var (data, _, _, number) in numbered)
            lines.Add(run.Ok(data.Line, number));

        return new BatchOutcome(lines.OrderBy(l => l.Line).ToList(), null);
    }

    /// <summary>EMP-{year}-{5 digits}: continues above the highest generated number of the tenant and never repeats within the file.</summary>
    private async Task<string> NextNumberAsync(EmployeeImportData data, RunState state, CancellationToken ct)
    {
        var year = data.DateOfHire.Year;
        var prefix = $"EMP-{year}-";
        if (!state.Sequences.TryGetValue(year, out var sequence))
            sequence = await _employees.GetMaxNumberSuffixAsync(prefix, GeneratedDigits, ct);
        string number;
        do
        {
            sequence++;
            number = $"{prefix}{sequence.ToString($"D{GeneratedDigits}")}";
        }
        while (!state.SeenNumbers.Add(number));
        state.Sequences[year] = sequence;
        return number;
    }
}
