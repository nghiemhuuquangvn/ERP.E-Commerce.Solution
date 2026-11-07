using System.Collections.Concurrent;
using System.Threading.Channels;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Options;

namespace ERP.Shared.Streaming;

/// <summary>
/// One import as seen by the people watching it: the importer (the streaming POST) publishes snapshots, any number of SSE subscribers read them.
/// A subscriber that arrives late (the SSE is opened before or after the POST starts) first receives the latest snapshot, and one that arrives
/// after the end still gets the final one. Slow readers never slow the import: each subscriber has a small bounded queue that drops its oldest entry.
/// </summary>
public sealed class ImportSession
{
    private readonly object _gate = new();
    private readonly List<Channel<ImportProgress>> _subscribers = new();
    private readonly TimeProvider _clock;
    private ImportProgress? _last;

    internal ImportSession(string tenantId, string userId, Guid importId, TimeProvider clock)
    {
        TenantId = tenantId;
        UserId = userId;
        ImportId = importId;
        _clock = clock;
        CreatedAt = clock.GetUtcNow();
    }

    public string TenantId { get; }
    public string UserId { get; }
    public Guid ImportId { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public bool Started { get; private set; }
    public bool IsRunning => Started && FinishedAt == null;
    public bool IsFinished => FinishedAt != null;

    internal void MarkStarted()
    {
        lock (_gate) Started = true;
    }

    /// <summary>A snapshot while the import runs.</summary>
    public void Publish(ImportProgress progress)
    {
        lock (_gate)
        {
            if (FinishedAt != null) return;
            _last = progress;
            foreach (var subscriber in _subscribers) subscriber.Writer.TryWrite(progress);
        }
    }

    /// <summary>The final snapshot (state completed or failed); every subscriber's stream ends after it.</summary>
    public void Finish(ImportProgress final)
    {
        lock (_gate)
        {
            if (FinishedAt != null) return;
            FinishedAt = _clock.GetUtcNow();
            _last = final;
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(final);
                subscriber.Writer.TryComplete();
            }
            _subscribers.Clear();
        }
    }

    /// <summary>The snapshots from now on (the latest one first when there is one); ends after the final snapshot.</summary>
    public async IAsyncEnumerable<ImportProgress> SubscribeAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<ImportProgress>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        lock (_gate)
        {
            if (_last != null) channel.Writer.TryWrite(_last);
            if (FinishedAt != null) channel.Writer.TryComplete();
            else _subscribers.Add(channel);
        }

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct)) yield return item;
        }
        finally
        {
            lock (_gate) _subscribers.Remove(channel);
        }
    }
}

public interface IImportProgressRegistry
{
    /// <summary>The session of this import, created on first use (by the SSE subscriber or by the importer, whichever comes first).</summary>
    ImportSession GetOrCreate(string tenantId, string userId, Guid importId);

    /// <summary>The importer claims the session: refused when the user already has another import running.</summary>
    ImportSession BeginRun(string tenantId, string userId, Guid importId);
}

/// <summary>
/// In-memory registry of the imports in progress, keyed by (tenant, user, importId): another user or another tenant never finds the session.
/// It lives in one process, so the SSE request and the POST of one import must reach the same instance (one replica today, recorded as a
/// limit). Old sessions are swept lazily.
/// </summary>
public sealed class ImportProgressRegistry : IImportProgressRegistry
{
    public const int MaxPendingSessionsPerUser = 5;

    private readonly ConcurrentDictionary<(string Tenant, string User, Guid Id), ImportSession> _sessions = new();
    private readonly TimeProvider _clock;
    private readonly ImportLimits _limits;

    public ImportProgressRegistry(IOptions<ImportLimits> limits, TimeProvider? clock = null)
    {
        _limits = limits.Value;
        _clock = clock ?? TimeProvider.System;
    }

    public ImportSession GetOrCreate(string tenantId, string userId, Guid importId)
    {
        Sweep();
        var key = (tenantId, userId, importId);
        if (_sessions.TryGetValue(key, out var existing)) return existing;

        var pending = _sessions.Values.Count(s => s.TenantId == tenantId && s.UserId == userId && !s.IsFinished);
        if (pending >= MaxPendingSessionsPerUser)
            throw new BusinessRuleException("Import.TooManySessions", "Too many imports are open for this user; finish or cancel one first.");
        return _sessions.GetOrAdd(key, _ => new ImportSession(tenantId, userId, importId, _clock));
    }

    public ImportSession BeginRun(string tenantId, string userId, Guid importId)
    {
        var session = GetOrCreate(tenantId, userId, importId);
        if (session.Started)
            throw new BusinessRuleException("Import.AlreadyStarted", "This import id was already used; start a new import.");
        var busy = _sessions.Values.Any(s => s.TenantId == tenantId && s.UserId == userId && s.ImportId != importId && s.IsRunning);
        if (busy)
            throw new BusinessRuleException("Import.Busy", "Another import of yours is still running; wait for it to finish.");
        session.MarkStarted();
        return session;
    }

    private void Sweep()
    {
        var now = _clock.GetUtcNow();
        var keep = TimeSpan.FromMinutes(_limits.SessionKeepMinutes);
        var max = TimeSpan.FromMinutes(_limits.SessionMaxMinutes);
        foreach (var (key, session) in _sessions)
        {
            var expired = session.FinishedAt is { } done ? now - done > keep : now - session.CreatedAt > max;
            if (expired) _sessions.TryRemove(key, out _);
        }
    }
}
