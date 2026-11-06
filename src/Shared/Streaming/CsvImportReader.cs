using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.Shared.Streaming;

/// <summary>One data row of the file: its line number in the file (the header is line 1) and the cells by normalised header name.</summary>
public sealed class CsvImportRow
{
    public CsvImportRow(int line, IReadOnlyDictionary<string, string> cells, string? parseError = null)
    {
        Line = line;
        Cells = cells;
        ParseError = parseError;
    }

    public int Line { get; }
    public IReadOnlyDictionary<string, string> Cells { get; }
    /// <summary>The row could not be read (a broken quote, ...); the reader went on with the next one.</summary>
    public string? ParseError { get; }

    public string Get(string column) => Cells.TryGetValue(column, out var value) ? value.Trim() : string.Empty;
}

/// <summary>Counts the bytes of the request body that were read so far (the numerator of the progress percentage).</summary>
public sealed class ImportBytesCounter
{
    private long _bytes;
    public long BytesRead => Interlocked.Read(ref _bytes);
    public long? TotalBytes { get; init; }
    internal void Add(int count) => Interlocked.Add(ref _bytes, count);
}

/// <summary>
/// Reads a CSV upload <b>while it arrives</b>: one row at a time from the request stream, never the whole file in memory. UTF-8 (a BOM is
/// accepted), comma, semicolon (Excel in Vietnamese locale) or tab separated, headers matched without case, spaces and underscores
/// ("Category Code", "category_code" and "categorycode" are the same column). A wrong header, too many rows or too many bytes is a
/// <see cref="BusinessRuleException"/> raised before the first row, so the caller can still answer a normal 400.
/// </summary>
public static class CsvImportReader
{
    public static string NormaliseHeader(string? header) =>
        (header ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

    public static async IAsyncEnumerable<CsvImportRow> ReadAsync(
        Stream body,
        IReadOnlyCollection<string> requiredColumns,
        ImportLimits limits,
        ImportBytesCounter counter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var counting = new CountingStream(body, counter, limits.MaxBytes);
        using var text = new StreamReader(counting, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectDelimiter = true,
            DetectDelimiterValues = new[] { ",", ";", "\t" },
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
            PrepareHeaderForMatch = args => NormaliseHeader(args.Header),
        };
        using var csv = new CsvReader(text, config);

        if (!await csv.ReadAsync())
            throw new BusinessRuleException("Import.EmptyFile", "The file is empty: the first line must be the header.");
        csv.ReadHeader();
        var headers = (csv.HeaderRecord ?? Array.Empty<string>()).Select(NormaliseHeader).ToArray();
        var missing = requiredColumns.Where(c => !headers.Contains(c)).ToList();
        if (missing.Count > 0)
            throw new BusinessRuleException("Import.MissingColumns", $"The header lacks the required column(s): {string.Join(", ", missing)}.");

        var rows = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            CsvImportRow? row = null;
            bool more;
            try
            {
                more = await csv.ReadAsync();
                if (more)
                {
                    var cells = new Dictionary<string, string>(headers.Length, StringComparer.Ordinal);
                    for (var i = 0; i < headers.Length; i++)
                    {
                        if (headers[i].Length == 0) continue;
                        cells[headers[i]] = csv.TryGetField<string>(i, out var value) ? value ?? string.Empty : string.Empty;
                    }
                    row = cells.Values.All(string.IsNullOrWhiteSpace) ? null : new CsvImportRow(csv.Parser.Row, cells);
                }
            }
            catch (CsvHelperException ex)
            {
                more = true;
                row = new CsvImportRow(csv.Parser.Row, new Dictionary<string, string>(), $"The row could not be read: {ex.Message.Split('\n')[0].Trim()}");
            }

            if (!more) yield break;
            if (row == null) continue;   // a blank line
            if (++rows > limits.MaxRows)
                throw new BusinessRuleException("Import.TooManyRows", $"The file has more than {limits.MaxRows} rows; split it into smaller files.");
            yield return row;
        }
    }

    /// <summary>Wraps the request body: counts what was read and refuses a body larger than the limit.</summary>
    private sealed class CountingStream : Stream
    {
        private readonly Stream _inner;
        private readonly ImportBytesCounter _counter;
        private readonly long _max;

        public CountingStream(Stream inner, ImportBytesCounter counter, long max)
        {
            _inner = inner;
            _counter = counter;
            _max = max;
        }

        private int Track(int read)
        {
            if (read > 0)
            {
                _counter.Add(read);
                if (_counter.BytesRead > _max)
                    throw new BusinessRuleException("Import.TooLarge", $"The file is larger than {_max / (1024 * 1024)} MB.");
            }
            return read;
        }

        public override int Read(byte[] buffer, int offset, int count) => Track(_inner.Read(buffer, offset, count));
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Track(await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Track(await _inner.ReadAsync(buffer, cancellationToken));

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
