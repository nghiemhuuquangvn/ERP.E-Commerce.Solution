using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Streaming;

/// <summary>
/// The bookkeeping every streaming import shares: the counters, the row lines, the progress snapshots for the SSE channel and the summary.
/// A handler reads rows, decides each one's outcome and asks this class for the line to yield.
/// </summary>
public sealed class ImportRun
{
    private readonly ImportSession _session;
    private readonly ImportBytesCounter _bytes;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public ImportRun(ImportSession session, ImportBytesCounter bytes, bool dryRun)
    {
        _session = session;
        _bytes = bytes;
        DryRun = dryRun;
    }

    public bool DryRun { get; }
    public int Processed { get; private set; }
    public int Created { get; private set; }
    public int Skipped { get; private set; }
    public int Failed { get; private set; }

    public ImportStartedLine Started() => new(_session.ImportId.ToString(), DryRun);

    /// <summary>The row is valid: created (or, in a dry run, would be created).</summary>
    public ImportRowLine Ok(int line, string? key)
    {
        Processed++;
        Created++;
        return new ImportRowLine(line, key, DryRun ? ImportRowStatuses.WouldCreate : ImportRowStatuses.Created, null);
    }

    /// <summary>The row is valid but not imported (it already exists, it repeats an earlier row ...).</summary>
    public ImportRowLine Skip(int line, string? key, string reason)
    {
        Processed++;
        Skipped++;
        return new ImportRowLine(line, key, ImportRowStatuses.Skipped, reason);
    }

    /// <summary>The row is invalid.</summary>
    public ImportRowLine Fail(int line, string? key, string reason)
    {
        Processed++;
        Failed++;
        return new ImportRowLine(line, key, ImportRowStatuses.Error, reason);
    }

    /// <summary>A rows that were counted as created could not be saved (the batch was rolled back): move them from created to failed.</summary>
    public void Revert(int rows)
    {
        Created -= rows;
        Failed += rows;
    }

    public void PublishProgress() => _session.Publish(Snapshot(ImportStates.Running));

    public ImportSummaryLine Complete()
    {
        _session.Finish(Snapshot(ImportStates.Completed));
        return new ImportSummaryLine(Processed, Created, Skipped, Failed, DryRun, _clock.ElapsedMilliseconds);
    }

    public void Fail(string message) => _session.Finish(Snapshot(ImportStates.Failed, message));

    private ImportProgress Snapshot(string state, string? message = null)
    {
        var total = _bytes.TotalBytes;
        double? percent = total is > 0 ? Math.Min(100, Math.Round(_bytes.BytesRead * 100.0 / total.Value, 1)) : null;
        if (state == ImportStates.Completed) percent = 100;
        return new ImportProgress(state, Processed, Created, Skipped, Failed, _bytes.BytesRead, total, percent, _clock.ElapsedMilliseconds, message);
    }
}

public static class ImportBatcher
{
    /// <summary>Groups an asynchronous sequence into lists of at most <paramref name="size"/> items (the last list may be shorter).</summary>
    public static async IAsyncEnumerable<List<T>> BatchAsync<T>(
        this IAsyncEnumerable<T> source, int size,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var batch = new List<T>(size);
        await foreach (var item in source.WithCancellation(ct))
        {
            batch.Add(item);
            if (batch.Count < size) continue;
            yield return batch;
            batch = new List<T>(size);
        }
        if (batch.Count > 0) yield return batch;
    }
}

public static class StreamingServiceCollectionExtensions
{
    /// <summary>Registers the progress registry and binds <see cref="ImportLimits"/> from the <c>Import</c> configuration section.</summary>
    public static IServiceCollection AddStreamingImport(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<ImportLimits>(configuration.GetSection(ImportLimits.SectionName));
        services.AddSingleton<IImportProgressRegistry, ImportProgressRegistry>();
        return services;
    }
}
