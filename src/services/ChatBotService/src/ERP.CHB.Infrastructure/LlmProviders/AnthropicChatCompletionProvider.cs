using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ERP.CHB.Application.Interfaces;
using ERP.CHB.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.CHB.Infrastructure.LlmProviders;

/// <summary>
/// Raw HTTP client for Anthropic's Messages API (https://api.anthropic.com/v1/messages),
/// not a vendor SDK package — the wire format is small and stable enough (and there is no
/// first-party .NET SDK already used elsewhere in this repo) that a direct HttpClient +
/// System.Text.Json implementation is simpler than adding an unverified dependency.
/// </summary>
public class AnthropicChatCompletionProvider : IChatCompletionProvider
{
    private const string ApiVersion = "2023-06-01";
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicChatCompletionProvider> _logger;
    private readonly string _apiKey;
    private readonly int _maxTokens;

    public AiProvider Provider => AiProvider.Claude;

    public AnthropicChatCompletionProvider(HttpClient httpClient, IConfiguration configuration, ILogger<AnthropicChatCompletionProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic:ApiKey is not configured.");
        _maxTokens = configuration.GetValue("Anthropic:MaxTokens", 1024);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var body = new
        {
            model = request.Model,
            max_tokens = _maxTokens,
            system = request.SystemPrompt,
            messages = BuildMessages(request.History),
            tools = request.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = JsonSerializer.Deserialize<JsonElement>(t.InputSchemaJson)
            }),
            stream = true
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", ApiVersion);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Anthropic API error {StatusCode}: {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"Anthropic API returned {response.StatusCode}.");
        }

        // index -> (blockType, toolCallId, toolName, partialJsonBuilder)
        var toolBlocks = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        var stopReason = "end_turn";

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await foreach (var payload in ServerSentEventReader.ReadDataLinesAsync(stream, ct))
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventType = root.GetProperty("type").GetString();

            switch (eventType)
            {
                case "content_block_start":
                {
                    var index = root.GetProperty("index").GetInt32();
                    var block = root.GetProperty("content_block");
                    if (block.GetProperty("type").GetString() == "tool_use")
                    {
                        var id = block.GetProperty("id").GetString()!;
                        var name = block.GetProperty("name").GetString()!;
                        toolBlocks[index] = (id, name, new StringBuilder());
                    }
                    break;
                }
                case "content_block_delta":
                {
                    var index = root.GetProperty("index").GetInt32();
                    var delta = root.GetProperty("delta");
                    var deltaType = delta.GetProperty("type").GetString();
                    if (deltaType == "text_delta")
                    {
                        yield return new TextDeltaEvent(delta.GetProperty("text").GetString() ?? string.Empty);
                    }
                    else if (deltaType == "input_json_delta" && toolBlocks.TryGetValue(index, out var pending))
                    {
                        pending.Args.Append(delta.GetProperty("partial_json").GetString());
                    }
                    break;
                }
                case "content_block_stop":
                {
                    var index = root.GetProperty("index").GetInt32();
                    if (toolBlocks.TryGetValue(index, out var pending))
                    {
                        var argsJson = pending.Args.Length > 0 ? pending.Args.ToString() : "{}";
                        yield return new ToolUseRequestedEvent(pending.Id, pending.Name, argsJson);
                    }
                    break;
                }
                case "message_delta":
                {
                    if (root.TryGetProperty("delta", out var delta) && delta.TryGetProperty("stop_reason", out var sr) && sr.ValueKind == JsonValueKind.String)
                        stopReason = sr.GetString() == "tool_use" ? "tool_use" : "end_turn";
                    break;
                }
            }
        }

        yield return new TurnCompletedEvent(stopReason);
    }

    /// <summary>
    /// Anthropic requires strict user/assistant alternation with tool_use blocks inside
    /// the assistant turn and matching tool_result blocks inside the following user turn —
    /// our provider-agnostic history (see IChatCompletionProvider) records each tool call as
    /// its own pair of entries, so consecutive entries of the same "turn type" are merged
    /// back into a single Anthropic message here.
    /// </summary>
    private static List<object> BuildMessages(IReadOnlyList<ProviderMessage> history)
    {
        var result = new List<object>();
        List<object>? assistantContent = null;
        List<object>? toolResultContent = null;

        void FlushAssistant()
        {
            if (assistantContent is { Count: > 0 })
                result.Add(new { role = "assistant", content = assistantContent });
            assistantContent = null;
        }

        void FlushToolResults()
        {
            if (toolResultContent is { Count: > 0 })
                result.Add(new { role = "user", content = toolResultContent });
            toolResultContent = null;
        }

        foreach (var message in history)
        {
            switch (message)
            {
                case ProviderUserMessage user:
                    FlushAssistant();
                    FlushToolResults();
                    result.Add(new { role = "user", content = new List<object> { new { type = "text", text = user.Content } } });
                    break;

                case ProviderAssistantMessage assistant:
                    FlushToolResults();
                    assistantContent ??= new List<object>();
                    if (!string.IsNullOrEmpty(assistant.Content))
                        assistantContent.Add(new { type = "text", text = assistant.Content });
                    if (assistant.ToolUses is not null)
                    {
                        foreach (var toolUse in assistant.ToolUses)
                        {
                            assistantContent.Add(new
                            {
                                type = "tool_use",
                                id = toolUse.ToolCallId,
                                name = toolUse.ToolName,
                                input = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(toolUse.ArgumentsJson) ? "{}" : toolUse.ArgumentsJson)
                            });
                        }
                    }
                    break;

                case ProviderToolResultMessage toolResult:
                    FlushAssistant();
                    toolResultContent ??= new List<object>();
                    toolResultContent.Add(new { type = "tool_result", tool_use_id = toolResult.ToolCallId, content = toolResult.Content });
                    break;
            }
        }

        FlushAssistant();
        FlushToolResults();
        return result;
    }
}
