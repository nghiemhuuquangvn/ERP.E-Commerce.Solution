namespace ERP.CHB.Infrastructure.LlmProviders;

/// <summary>
/// Minimal SSE line reader shared by both LLM providers — both Anthropic's Messages API
/// and OpenAI's Chat Completions API stream "data: {json}\n\n" frames when stream:true,
/// they just disagree on the JSON payload shape inside each frame (parsed separately by
/// each provider). OpenAI additionally sends a literal "data: [DONE]" sentinel, filtered
/// out here so callers only ever see JSON payloads.
/// </summary>
internal static class ServerSentEventReader
{
    public static async IAsyncEnumerable<string> ReadDataLinesAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        while (true)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                yield break;

            if (line.Length == 0 || !line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var payload = line["data:".Length..].Trim();
            if (payload.Length == 0 || payload == "[DONE]")
                continue;

            yield return payload;
        }
    }
}
