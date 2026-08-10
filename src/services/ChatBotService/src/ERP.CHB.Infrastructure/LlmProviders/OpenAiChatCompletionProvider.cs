using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ERP.CHB.Application.Interfaces;
using ERP.CHB.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.CHB.Infrastructure.LlmProviders;

/// <summary>
/// Raw HTTP client for OpenAI's Chat Completions API (https://api.openai.com/v1/chat/completions)
/// — the free-tier provider (see docs/business/Identity/License.md and
/// ModuleCodes.ChatBotPremium: tenants without the premium license flag are routed here
/// instead of Anthropic). Same rationale as AnthropicChatCompletionProvider for calling the
/// REST API directly rather than via a vendor SDK package.
/// </summary>
public class OpenAiChatCompletionProvider : IChatCompletionProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiChatCompletionProvider> _logger;
    private readonly string _apiKey;
    private readonly int _maxTokens;

    public AiProvider Provider => AiProvider.ChatGpt;

    public OpenAiChatCompletionProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiChatCompletionProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["OpenAi:ApiKey"]
            ?? throw new InvalidOperationException("OpenAi:ApiKey is not configured.");
        _maxTokens = configuration.GetValue("OpenAi:MaxTokens", 1024);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var messages = new List<object> { new { role = "system", content = request.SystemPrompt } };
        messages.AddRange(BuildMessages(request.History));

        var body = new
        {
            model = request.Model,
            max_tokens = _maxTokens,
            messages,
            tools = request.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = JsonSerializer.Deserialize<JsonElement>(t.InputSchemaJson)
                }
            }),
            stream = true
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenAI API error {StatusCode}: {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"OpenAI API returned {response.StatusCode}.");
        }

        // OpenAI keys tool-call deltas by their position in the turn's tool_calls array
        // ("index"), not by id — the id/name only arrive on the first chunk for that index,
        // subsequent chunks carry only incremental "arguments" text to concatenate.
        var toolCalls = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        var stopReason = "stop";

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await foreach (var payload in ServerSentEventReader.ReadDataLinesAsync(stream, ct))
        {
            using var doc = JsonDocument.Parse(payload);
            var choice = doc.RootElement.GetProperty("choices")[0];
            var delta = choice.GetProperty("delta");

            if (delta.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
            {
                var text = contentEl.GetString();
                if (!string.IsNullOrEmpty(text))
                    yield return new TextDeltaEvent(text);
            }

            if (delta.TryGetProperty("tool_calls", out var toolCallsEl) && toolCallsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in toolCallsEl.EnumerateArray())
                {
                    var index = tc.GetProperty("index").GetInt32();
                    if (!toolCalls.TryGetValue(index, out var existing))
                        existing = (string.Empty, string.Empty, new StringBuilder());

                    if (tc.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                        existing.Id = idEl.GetString()!;

                    if (tc.TryGetProperty("function", out var fn))
                    {
                        if (fn.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                            existing.Name = nameEl.GetString()!;
                        if (fn.TryGetProperty("arguments", out var argsEl) && argsEl.ValueKind == JsonValueKind.String)
                            existing.Args.Append(argsEl.GetString());
                    }

                    toolCalls[index] = existing;
                }
            }

            if (choice.TryGetProperty("finish_reason", out var finishEl) && finishEl.ValueKind == JsonValueKind.String)
            {
                var finishReason = finishEl.GetString();
                if (finishReason == "tool_calls")
                    stopReason = "tool_use";
                else if (!string.IsNullOrEmpty(finishReason))
                    stopReason = finishReason;
            }
        }

        foreach (var (_, call) in toolCalls.OrderBy(kv => kv.Key))
        {
            var argsJson = call.Args.Length > 0 ? call.Args.ToString() : "{}";
            yield return new ToolUseRequestedEvent(call.Id, call.Name, argsJson);
        }

        yield return new TurnCompletedEvent(stopReason);
    }

    /// <summary>
    /// OpenAI expects one assistant message per turn carrying ALL of that turn's tool_calls
    /// together, followed by one role:"tool" message per call — our provider-agnostic history
    /// (see IChatCompletionProvider) instead records one assistant-with-single-tool-use entry
    /// per call, so consecutive assistant entries are merged back into one message here (tool
    /// result entries are already 1:1 with OpenAI's expected shape, no merge needed).
    /// </summary>
    private static List<object> BuildMessages(IReadOnlyList<ProviderMessage> history)
    {
        var result = new List<object>();
        string? assistantContent = null;
        List<object>? assistantToolCalls = null;

        void FlushAssistant()
        {
            if (assistantContent is not null || assistantToolCalls is { Count: > 0 })
            {
                result.Add(new
                {
                    role = "assistant",
                    content = (object?)assistantContent,
                    tool_calls = assistantToolCalls
                });
            }
            assistantContent = null;
            assistantToolCalls = null;
        }

        foreach (var message in history)
        {
            switch (message)
            {
                case ProviderUserMessage user:
                    FlushAssistant();
                    result.Add(new { role = "user", content = user.Content });
                    break;

                case ProviderAssistantMessage assistant:
                    if (!string.IsNullOrEmpty(assistant.Content))
                        assistantContent = (assistantContent ?? string.Empty) + assistant.Content;
                    if (assistant.ToolUses is not null)
                    {
                        assistantToolCalls ??= new List<object>();
                        foreach (var toolUse in assistant.ToolUses)
                        {
                            assistantToolCalls.Add(new
                            {
                                id = toolUse.ToolCallId,
                                type = "function",
                                function = new { name = toolUse.ToolName, arguments = toolUse.ArgumentsJson }
                            });
                        }
                    }
                    break;

                case ProviderToolResultMessage toolResult:
                    FlushAssistant();
                    result.Add(new { role = "tool", tool_call_id = toolResult.ToolCallId, content = toolResult.Content });
                    break;
            }
        }

        FlushAssistant();
        return result;
    }
}
