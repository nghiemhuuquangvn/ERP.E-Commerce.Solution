using System.Text;
using System.Text.Json;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Streaming;

internal static class StreamingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// Streams <see cref="ImportLine"/>s as NDJSON (one JSON object per line), flushing after every line so the client sees each row as soon as
/// it is processed. The first line is awaited <b>before</b> the status line is written: an exception raised while the file header is read
/// (a wrong header, a limit) is still a normal 400 from the exception middleware. After the stream has started a failure can no longer
/// change the status, so it becomes a final <c>{"type":"error"}</c> line.
/// </summary>
public sealed class NdjsonResult : IActionResult
{
    private readonly IAsyncEnumerable<ImportLine> _lines;

    public NdjsonResult(IAsyncEnumerable<ImportLine> lines) => _lines = lines;

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext;
        var ct = http.RequestAborted;
        await using var enumerator = _lines.GetAsyncEnumerator(ct);

        var any = await enumerator.MoveNextAsync();   // may throw: nothing was written yet

        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "application/x-ndjson; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";   // nginx: do not buffer this response
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await http.Response.StartAsync(ct);

        try
        {
            while (any)
            {
                await WriteLineAsync(http.Response, enumerator.Current, ct);
                any = await enumerator.MoveNextAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // the client went away: nobody is listening
        }
        catch (Exception ex)
        {
            var message = ex is BusinessRuleException ? ex.Message : "The import stopped because of a server error.";
            if (ex is not BusinessRuleException)
                http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("ERP.Shared.Streaming.NdjsonResult")
                    .LogError(ex, "A streamed import failed after the response had started");
            try
            {
                await WriteLineAsync(http.Response, new ImportErrorLine(message), CancellationToken.None);
            }
            catch
            {
                // the connection is gone
            }
        }
    }

    private static async Task WriteLineAsync(HttpResponse response, ImportLine line, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(line, line.GetType(), StreamingJson.Options);
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(json + "\n"), ct);
        await response.Body.FlushAsync(ct);
    }
}

/// <summary>
/// Streams progress snapshots as Server-Sent Events: <c>event: progress|completed|failed</c> + <c>data: {json}</c>. A comment line is sent first
/// (so the client knows the stream is open) and again every <see cref="Heartbeat"/> while nothing happens (so proxies keep the connection).
/// The stream ends after a <c>completed</c> or <c>failed</c> event.
/// </summary>
public sealed class SseResult : IActionResult
{
    public static readonly TimeSpan DefaultHeartbeat = TimeSpan.FromSeconds(15);

    private readonly IAsyncEnumerable<ImportProgress> _events;

    public SseResult(IAsyncEnumerable<ImportProgress> events, TimeSpan? heartbeat = null)
    {
        _events = events;
        Heartbeat = heartbeat ?? DefaultHeartbeat;
    }

    public TimeSpan Heartbeat { get; }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext;
        var ct = http.RequestAborted;
        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "text/event-stream; charset=utf-8";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await http.Response.StartAsync(ct);

        try
        {
            await WriteRawAsync(http.Response, ": connected\nretry: 3000\n\n", ct);
            await using var enumerator = _events.GetAsyncEnumerator(ct);
            var next = enumerator.MoveNextAsync().AsTask();
            while (true)
            {
                var winner = await Task.WhenAny(next, Task.Delay(Heartbeat, ct));
                if (winner != next)
                {
                    await WriteRawAsync(http.Response, ": ping\n\n", ct);
                    continue;
                }
                if (!await next) break;

                var progress = enumerator.Current;
                await WriteRawAsync(http.Response, $"event: {progress.State.Replace(ImportStates.Running, "progress")}\ndata: {JsonSerializer.Serialize(progress, StreamingJson.Options)}\n\n", ct);
                if (progress.State != ImportStates.Running) break;
                next = enumerator.MoveNextAsync().AsTask();
            }
        }
        catch (OperationCanceledException)
        {
            // the client closed the stream
        }
    }

    private static async Task WriteRawAsync(HttpResponse response, string text, CancellationToken ct)
    {
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), ct);
        await response.Body.FlushAsync(ct);
    }
}
