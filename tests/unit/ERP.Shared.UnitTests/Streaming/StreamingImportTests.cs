using System.Text;
using System.Text.Json;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Streaming;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace ERP.Shared.UnitTests.Streaming;

public class StreamingImportTests
{
    private static readonly string[] Required = { "sku", "name" };

    private static MemoryStream Csv(string text, bool bom = false)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new MemoryStream(bom ? Encoding.UTF8.GetPreamble().Concat(bytes).ToArray() : bytes);
    }

    private static async Task<List<CsvImportRow>> Read(Stream body, ImportLimits? limits = null, ImportBytesCounter? counter = null)
    {
        var rows = new List<CsvImportRow>();
        await foreach (var row in CsvImportReader.ReadAsync(body, Required, limits ?? new ImportLimits(), counter ?? new ImportBytesCounter()))
            rows.Add(row);
        return rows;
    }

    // ── reader ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Rows_are_read_with_their_file_line_and_headers_match_without_case_space_or_underscore()
    {
        var rows = await Read(Csv("SKU,Name,Category Code\nA-1,First,CAT1\nA-2,Second,\n"));

        rows.Select(r => r.Line).Should().Equal(2, 3);
        rows[0].Get("sku").Should().Be("A-1");
        rows[0].Get("categorycode").Should().Be("CAT1");
        rows[1].Get("categorycode").Should().BeEmpty();
    }

    [Fact]
    public async Task A_bom_a_semicolon_separator_and_a_quoted_cell_with_a_comma_and_a_new_line_are_read()
    {
        var rows = await Read(Csv("sku;name\nA-1;\"Cable, 2 m\nblack\"\n", bom: true));

        rows.Should().ContainSingle();
        rows[0].Get("sku").Should().Be("A-1");
        rows[0].Get("name").Should().Be("Cable, 2 m\nblack");
    }

    [Fact]
    public async Task Blank_lines_are_skipped_but_the_line_numbers_still_count_them()
    {
        var rows = await Read(Csv("sku,name\nA-1,One\n,\n\nA-2,Two\n"));

        rows.Select(r => r.Get("sku")).Should().Equal("A-1", "A-2");
        rows[1].Line.Should().BeGreaterThan(rows[0].Line + 1);
    }

    [Fact]
    public async Task A_missing_required_column_an_empty_file_too_many_rows_and_too_many_bytes_are_business_rule_errors()
    {
        var noName = async () => await Read(Csv("sku,price\nA,1\n"));
        (await noName.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("name");

        var empty = async () => await Read(Csv(""));
        await empty.Should().ThrowAsync<BusinessRuleException>();

        var tooMany = async () => await Read(Csv("sku,name\nA,1\nB,2\nC,3\n"), new ImportLimits { MaxRows = 2 });
        (await tooMany.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("Import.TooManyRows");

        var tooBig = async () => await Read(Csv("sku,name\n" + string.Concat(Enumerable.Repeat("A,Name\n", 200))), new ImportLimits { MaxBytes = 100 });
        (await tooBig.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("Import.TooLarge");
    }

    [Fact]
    public async Task The_bytes_read_are_counted_for_the_progress_percentage()
    {
        var text = "sku,name\nA,One\nB,Two\n";
        var counter = new ImportBytesCounter { TotalBytes = Encoding.UTF8.GetByteCount(text) };

        await Read(Csv(text), counter: counter);

        counter.BytesRead.Should().Be(counter.TotalBytes);
    }

    [Fact]
    public async Task A_row_is_available_before_the_rest_of_the_body_has_arrived()
    {
        var pipe = new System.IO.Pipelines.Pipe();
        var enumerator = CsvImportReader.ReadAsync(pipe.Reader.AsStream(), Required, new ImportLimits(), new ImportBytesCounter()).GetAsyncEnumerator();

        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("sku,name\nA-1,First\n"));
        await pipe.Writer.FlushAsync();
        var first = enumerator.MoveNextAsync().AsTask();
        (await Task.WhenAny(first, Task.Delay(2000))).Should().BeSameAs(first, "the first row is parsed while the upload is still open");
        enumerator.Current.Get("sku").Should().Be("A-1");

        await pipe.Writer.CompleteAsync();
        (await enumerator.MoveNextAsync()).Should().BeFalse();
    }

    // ── NDJSON ───────────────────────────────────────────────────────────

    private sealed class FlushCountingStream : MemoryStream
    {
        public int Flushes { get; private set; }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flushes++;
            return base.FlushAsync(cancellationToken);
        }
    }

    private static async IAsyncEnumerable<ImportLine> Lines(params ImportLine[] lines)
    {
        foreach (var line in lines)
        {
            await Task.Yield();
            yield return line;
        }
    }

    private static async IAsyncEnumerable<ImportLine> ThrowAfter(ImportLine first, Exception error)
    {
        yield return first;
        await Task.Yield();
        throw error;
    }

    private static async IAsyncEnumerable<ImportLine> ThrowAtOnce(Exception error)
    {
        await Task.Yield();
        throw error;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static (ActionContext Context, FlushCountingStream Body) Context()
    {
        var http = new DefaultHttpContext();
        var body = new FlushCountingStream();
        http.Response.Body = body;
        http.RequestServices = Moq.Mock.Of<IServiceProvider>();
        return (new ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()), body);
    }

    [Fact]
    public async Task Ndjson_writes_one_json_object_per_line_and_flushes_after_each()
    {
        var (ctx, body) = Context();

        await new NdjsonResult(Lines(
            new ImportStartedLine("abc", false),
            new ImportRowLine(2, "A-1", ImportRowStatuses.Created, null),
            new ImportSummaryLine(1, 1, 0, 0, false, 5))).ExecuteResultAsync(ctx);

        var lines = Encoding.UTF8.GetString(body.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3);
        JsonDocument.Parse(lines[0]).RootElement.GetProperty("type").GetString().Should().Be("started");
        var row = JsonDocument.Parse(lines[1]).RootElement;
        row.GetProperty("type").GetString().Should().Be("row");
        row.GetProperty("status").GetString().Should().Be("created");
        row.GetProperty("line").GetInt32().Should().Be(2);
        JsonDocument.Parse(lines[2]).RootElement.GetProperty("processed").GetInt32().Should().Be(1);
        body.Flushes.Should().BeGreaterOrEqualTo(3);
        ctx.HttpContext.Response.ContentType.Should().StartWith("application/x-ndjson");
        ctx.HttpContext.Response.Headers["X-Accel-Buffering"].ToString().Should().Be("no");
    }

    [Fact]
    public async Task An_error_before_the_first_line_is_thrown_so_the_middleware_can_answer_400()
    {
        var (ctx, body) = Context();

        var act = async () => await new NdjsonResult(ThrowAtOnce(new BusinessRuleException("Import.MissingColumns", "no sku"))).ExecuteResultAsync(ctx);

        await act.Should().ThrowAsync<BusinessRuleException>();
        body.Length.Should().Be(0);
        ctx.HttpContext.Response.HasStarted.Should().BeFalse();
    }

    [Fact]
    public async Task An_error_after_the_stream_started_becomes_a_final_error_line_without_leaking_the_exception_text()
    {
        var (ctx, body) = Context();

        await new NdjsonResult(ThrowAfter(new ImportStartedLine("abc", false), new Exception("secret connection string"))).ExecuteResultAsync(ctx);

        var lines = Encoding.UTF8.GetString(body.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        var last = JsonDocument.Parse(lines[1]).RootElement;
        last.GetProperty("type").GetString().Should().Be("error");
        last.GetProperty("message").GetString().Should().NotContain("secret");
    }

    // ── SSE ──────────────────────────────────────────────────────────────

    private static ImportProgress P(string state, int processed) => new(state, processed, processed, 0, 0, 10, 100, 10, 5);

    [Fact]
    public async Task Sse_writes_named_events_a_first_comment_and_stops_after_the_final_one()
    {
        var (ctx, body) = Context();
        async IAsyncEnumerable<ImportProgress> Events()
        {
            yield return P(ImportStates.Running, 1);
            await Task.Yield();
            yield return P(ImportStates.Completed, 2);
            yield return P(ImportStates.Running, 99);   // never written: the stream ended at completed
        }

        await new SseResult(Events()).ExecuteResultAsync(ctx);

        var text = Encoding.UTF8.GetString(body.ToArray());
        text.Should().StartWith(": connected");
        text.Should().Contain("event: progress\ndata: {");
        text.Should().Contain("event: completed\ndata: {");
        text.Should().NotContain("\"processed\":99");
        ctx.HttpContext.Response.ContentType.Should().StartWith("text/event-stream");
    }

    [Fact]
    public async Task Sse_sends_a_heartbeat_comment_while_nothing_happens()
    {
        var (ctx, body) = Context();
        async IAsyncEnumerable<ImportProgress> Slow()
        {
            await Task.Delay(300);
            yield return P(ImportStates.Completed, 1);
        }

        await new SseResult(Slow(), TimeSpan.FromMilliseconds(50)).ExecuteResultAsync(ctx);

        Encoding.UTF8.GetString(body.ToArray()).Should().Contain(": ping");
    }

    // ── registry ─────────────────────────────────────────────────────────

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ImportProgressRegistry Registry(FakeTime? time = null) =>
        new(Options.Create(new ImportLimits()), time ?? new FakeTime());

    [Fact]
    public void Another_user_or_tenant_never_gets_the_session_of_an_import()
    {
        var registry = Registry();
        var id = Guid.NewGuid();

        var mine = registry.GetOrCreate("t1", "u1", id);

        registry.GetOrCreate("t1", "u1", id).Should().BeSameAs(mine);
        registry.GetOrCreate("t1", "u2", id).Should().NotBeSameAs(mine);
        registry.GetOrCreate("t2", "u1", id).Should().NotBeSameAs(mine);
    }

    [Fact]
    public void A_user_runs_one_import_at_a_time_and_an_import_id_is_used_once()
    {
        var registry = Registry();
        var first = Guid.NewGuid();
        var session = registry.BeginRun("t", "u", first);

        var second = () => registry.BeginRun("t", "u", Guid.NewGuid());
        second.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("Import.Busy");
        var again = () => registry.BeginRun("t", "u", first);
        again.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("Import.AlreadyStarted");
        registry.BeginRun("t", "other-user", Guid.NewGuid()).Should().NotBeNull();

        session.Finish(P(ImportStates.Completed, 1));
        registry.BeginRun("t", "u", Guid.NewGuid()).Should().NotBeNull();
    }

    [Fact]
    public void At_most_five_sessions_may_wait_open_per_user()
    {
        var registry = Registry();
        for (var i = 0; i < ImportProgressRegistry.MaxPendingSessionsPerUser; i++) registry.GetOrCreate("t", "u", Guid.NewGuid());

        var sixth = () => registry.GetOrCreate("t", "u", Guid.NewGuid());

        sixth.Should().Throw<BusinessRuleException>().Which.RuleName.Should().Be("Import.TooManySessions");
    }

    [Fact]
    public async Task A_subscriber_gets_the_latest_snapshot_first_then_the_next_ones_and_the_stream_ends_after_the_final()
    {
        var session = Registry().GetOrCreate("t", "u", Guid.NewGuid());
        session.Publish(P(ImportStates.Running, 1));

        var received = new List<ImportProgress>();
        var reading = Task.Run(async () =>
        {
            await foreach (var p in session.SubscribeAsync(CancellationToken.None)) received.Add(p);
        });
        await Task.Delay(100);
        session.Publish(P(ImportStates.Running, 2));
        session.Finish(P(ImportStates.Completed, 3));
        await reading.WaitAsync(TimeSpan.FromSeconds(5));

        received.Select(p => p.Processed).Should().Equal(1, 2, 3);
        received.Last().State.Should().Be(ImportStates.Completed);
    }

    [Fact]
    public async Task A_subscriber_that_comes_after_the_end_still_gets_the_final_snapshot()
    {
        var session = Registry().GetOrCreate("t", "u", Guid.NewGuid());
        session.Finish(P(ImportStates.Completed, 7));

        var received = new List<ImportProgress>();
        await foreach (var p in session.SubscribeAsync(CancellationToken.None)) received.Add(p);

        received.Should().ContainSingle().Which.Processed.Should().Be(7);
    }

    [Fact]
    public void Finished_sessions_are_dropped_after_the_keep_time_and_unused_ones_after_the_maximum()
    {
        var time = new FakeTime();
        var registry = Registry(time);
        var finishedId = Guid.NewGuid();
        var finished = registry.GetOrCreate("t", "u", finishedId);
        finished.Finish(P(ImportStates.Completed, 1));
        var unusedId = Guid.NewGuid();
        var unused = registry.GetOrCreate("t", "u", unusedId);

        time.Now = time.Now.AddMinutes(11);
        registry.GetOrCreate("t", "u", finishedId).Should().NotBeSameAs(finished);
        registry.GetOrCreate("t", "u", unusedId).Should().BeSameAs(unused);   // 11 minutes < 60

        time.Now = time.Now.AddMinutes(60);
        registry.GetOrCreate("t", "u", unusedId).Should().NotBeSameAs(unused);
    }

    // ── run bookkeeping and batching ─────────────────────────────────────

    [Fact]
    public void The_run_counts_rows_reports_a_dry_run_as_would_create_and_publishes_progress_with_a_percentage()
    {
        var session = Registry().BeginRun("t", "u", Guid.NewGuid());
        var counter = new ImportBytesCounter { TotalBytes = 200 };
        var run = new ImportRun(session, counter, dryRun: true);

        run.Ok(2, "A").Status.Should().Be(ImportRowStatuses.WouldCreate);
        run.Skip(3, "B", "exists").Status.Should().Be(ImportRowStatuses.Skipped);
        run.Fail(4, "C", "bad").Status.Should().Be(ImportRowStatuses.Error);
        counter.GetType().GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(counter, new object[] { 50 });
        run.PublishProgress();

        var summary = run.Complete();
        summary.Should().Be(summary with { ElapsedMs = summary.ElapsedMs });
        (summary.Processed, summary.Created, summary.Skipped, summary.Failed, summary.DryRun).Should().Be((3, 1, 1, 1, true));
        session.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task Batching_groups_a_sequence_into_lists_of_the_given_size()
    {
        async IAsyncEnumerable<int> Numbers()
        {
            for (var i = 1; i <= 5; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }

        var batches = new List<List<int>>();
        await foreach (var batch in Numbers().BatchAsync(2)) batches.Add(batch);

        batches.Select(b => string.Join(",", b)).Should().Equal("1,2", "3,4", "5");
    }
}
