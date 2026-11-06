namespace ERP.Shared.Streaming;

// The wire contract of a streaming import (docs/architecture/streaming-import-pattern.md):
//  * the POST answers application/x-ndjson - one ImportLine per line, written and flushed as the rows are processed;
//  * the progress channel (SSE, a separate GET) carries ImportProgress snapshots.

/// <summary>Row outcomes as they appear on the wire.</summary>
public static class ImportRowStatuses
{
    public const string Created = "created";
    public const string WouldCreate = "would-create";   // dry run: the row is valid and would be created
    public const string Skipped = "skipped";            // valid file row that is not imported (already exists, duplicate in the file ...)
    public const string Error = "error";                // the row is invalid
}

/// <summary>One NDJSON line. <see cref="Type"/> is the discriminator: started, row, summary, error.</summary>
public abstract record ImportLine(string Type);

public sealed record ImportStartedLine(string ImportId, bool DryRun) : ImportLine("started");

public sealed record ImportRowLine(int Line, string? Key, string Status, string? Message) : ImportLine("row");

public sealed record ImportSummaryLine(int Processed, int Created, int Skipped, int Failed, bool DryRun, long ElapsedMs) : ImportLine("summary");

/// <summary>The import stopped (a database failure, a limit). Rows reported before it were processed as stated.</summary>
public sealed record ImportErrorLine(string Message) : ImportLine("error");

public static class ImportStates
{
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// A progress snapshot (SSE data). The number of rows is not known while the file is still arriving, so the percentage is the share of the
/// request body already read (<see cref="BytesRead"/> / <see cref="TotalBytes"/>) and is null for a chunked upload without a length.
/// </summary>
public sealed record ImportProgress(
    string State, int Processed, int Created, int Skipped, int Failed,
    long BytesRead, long? TotalBytes, double? Percent, long ElapsedMs, string? Message = null);

/// <summary>Limits of one import (configuration section <c>Import</c>).</summary>
public sealed class ImportLimits
{
    public const string SectionName = "Import";
    public int MaxRows { get; set; } = 20_000;
    public long MaxBytes { get; set; } = 25 * 1024 * 1024;
    public int BatchSize { get; set; } = 200;
    /// <summary>How long a finished import stays readable for a late SSE subscriber.</summary>
    public int SessionKeepMinutes { get; set; } = 10;
    /// <summary>An import session that never started (an SSE opened for nothing) is dropped after this.</summary>
    public int SessionMaxMinutes { get; set; } = 60;
}
