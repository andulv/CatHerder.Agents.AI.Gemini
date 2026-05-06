using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CatHerder.Agents.AI.Gemini;

internal sealed class GeminiSseEventReducer
{
    private readonly ILogger? _logger;
    private readonly Dictionary<int, InFlightContent> _contentByIndex = new();

    private string? _responseId;
    private string? _messageId;
    private string? _conversationId;
    private string? _modelId;

    public GeminiSseEventReducer(ILogger? logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ChatResponseUpdate> Reduce(string eventType, JsonObject? payload)
    {
        var updates = new List<ChatResponseUpdate>();

        switch (eventType)
        {
            case "interaction.start":
                CaptureInteractionMetadata(payload?["interaction"] as JsonObject);
                break;

            case "interaction.status_update":
                break;

            case "content.start":
                HandleContentStart(payload, updates);
                break;

            case "content.delta":
                HandleContentDelta(payload, updates);
                break;

            case "content.stop":
                HandleContentStop(payload, updates);
                break;

            case "interaction.complete":
                HandleInteractionComplete(payload, updates);
                break;

            case "error":
                HandleError(payload, updates);
                break;

            case "done":
                break;

            default:
                _logger?.LogDebug("Ignoring unsupported Gemini SSE event type {EventType}.", eventType);
                break;
        }

        return updates;
    }

    private void CaptureInteractionMetadata(JsonObject? interaction)
    {
        if (interaction is null)
        {
            return;
        }

        var interactionId = interaction["id"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(interactionId))
        {
            _responseId = interactionId;
            _conversationId = interactionId;
            _messageId ??= interactionId;
        }

        var modelId = interaction["model"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            _modelId = modelId;
        }

    }

    private void HandleContentStart(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        if (!TryGetIndex(payload, out var index))
        {
            return;
        }

        var content = payload?["content"] as JsonObject;
        var type = content?["type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type))
        {
            _logger?.LogDebug("Ignoring Gemini SSE content.start without a content type.");
            return;
        }

        _contentByIndex[index] = new InFlightContent(type)
        {
            Id = content?["id"]?.GetValue<string>(),
            CallId = content?["call_id"]?.GetValue<string>(),
            Payload = content is null ? null : (JsonObject)content.DeepClone(),
        };

        EmitFunctionCallIfReady(_contentByIndex[index], updates);
    }

    private void HandleContentDelta(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        if (!TryGetIndex(payload, out var index))
        {
            return;
        }

        var delta = payload?["delta"] as JsonObject;
        if (delta is null)
        {
            _logger?.LogDebug("Ignoring Gemini SSE content.delta without a delta payload.");
            return;
        }

        var deltaType = delta["type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(deltaType))
        {
            _logger?.LogDebug("Ignoring Gemini SSE content.delta without a delta type.");
            return;
        }

        var content = GetOrCreateContent(index, deltaType);
        MergePayload(content, delta);

        switch (deltaType)
        {
            case "text":
                var text = delta["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    updates.Add(CreateTextUpdate(text));
                }

                break;

            case "thought_signature":
                content.Type = "thought";
                content.Signature = delta["signature"]?.GetValue<string>();
                break;

            case "thought_summary":
                content.Type = "thought";
                var summary = delta["content"]?["text"]?.GetValue<string>()
                    ?? delta["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(summary))
                {
                    updates.Add(CreateContentsUpdate([new TextReasoningContent(summary)]));
                }

                break;

            case "function_call":
                content.Type = "function_call";
                content.Id = delta["id"]?.GetValue<string>() ?? content.Id;
                content.Name = delta["name"]?.GetValue<string>() ?? content.Name;
                if (delta["arguments"] is not null)
                {
                    content.Arguments = delta["arguments"]?.DeepClone();
                }

                EmitFunctionCallIfReady(content, updates);
                break;

            default:
                if (GeminiBuiltInToolBridge.IsBuiltInToolCallType(deltaType))
                {
                    content.Type = deltaType;
                    content.Id = delta["id"]?.GetValue<string>() ?? content.Id;
                    EmitBuiltInToolCallIfReady(content, updates);
                    break;
                }

                if (GeminiBuiltInToolBridge.IsBuiltInToolResultType(deltaType))
                {
                    content.Type = deltaType;
                    content.CallId = delta["call_id"]?.GetValue<string>() ?? content.CallId;
                    EmitBuiltInToolResultIfReady(content, updates);
                    break;
                }

                _logger?.LogDebug("Ignoring unsupported Gemini SSE content delta type {DeltaType}.", deltaType);
                break;
        }
    }

    private void HandleContentStop(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        if (!TryGetIndex(payload, out var index))
        {
            return;
        }

        if (_contentByIndex.TryGetValue(index, out var content))
        {
            EmitFunctionCallIfReady(content, updates);
            EmitBuiltInToolCallIfReady(content, updates);
            EmitBuiltInToolResultIfReady(content, updates);
        }
    }

    private void HandleInteractionComplete(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        CaptureInteractionMetadata(payload?["interaction"] as JsonObject);

        foreach (var content in _contentByIndex.Values)
        {
            EmitFunctionCallIfReady(content, updates);
            EmitBuiltInToolCallIfReady(content, updates);
            EmitBuiltInToolResultIfReady(content, updates);
        }

        var usage = MapUsageDetails((payload?["interaction"] as JsonObject)?["usage"]);
        if (usage is null)
        {
            return;
        }

        var update = CreateContentsUpdate([new UsageContent(usage)]);
        updates.Add(update);
    }

    private void HandleError(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        var error = payload?["error"] as JsonObject ?? payload;
        var message = error?["message"]?.GetValue<string>() ?? "Gemini streaming error.";
        _logger?.LogWarning("Gemini streaming error event received: {Message}", message);
        updates.Add(CreateContentsUpdate([new ErrorContent(message)]));
    }

    private InFlightContent GetOrCreateContent(int index, string type)
    {
        if (_contentByIndex.TryGetValue(index, out var content))
        {
            return content;
        }

        content = new InFlightContent(type);
        _contentByIndex[index] = content;
        return content;
    }

    private void EmitFunctionCallIfReady(InFlightContent content, List<ChatResponseUpdate> updates)
    {
        if (content.FunctionCallEmitted || content.Type != "function_call")
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(content.Id) || string.IsNullOrWhiteSpace(content.Name) || content.Arguments is null)
        {
            return;
        }

        var argumentsJson = content.Arguments.ToJsonString();
        var functionCall = FunctionCallContent.CreateFromParsedArguments(
            argumentsJson,
            content.Id,
            content.Name,
            static json => JsonSerializer.Deserialize<Dictionary<string, object?>>(json));

        updates.Add(CreateContentsUpdate([functionCall]));
        content.FunctionCallEmitted = true;
    }

    private void EmitBuiltInToolCallIfReady(InFlightContent content, List<ChatResponseUpdate> updates)
    {
        if (content.FunctionCallEmitted
            || content.Payload is null
            || !GeminiBuiltInToolBridge.IsBuiltInToolCallType(content.Type)
            || string.IsNullOrWhiteSpace(content.Id))
        {
            return;
        }

        updates.Add(CreateContentsUpdate([GeminiBuiltInToolBridge.CreateToolCall(content.Payload)]));
        content.FunctionCallEmitted = true;
    }

    private void EmitBuiltInToolResultIfReady(InFlightContent content, List<ChatResponseUpdate> updates)
    {
        if (content.FunctionResultEmitted
            || content.Payload is null
            || !GeminiBuiltInToolBridge.IsBuiltInToolResultType(content.Type)
            || string.IsNullOrWhiteSpace(content.CallId)
            || !GeminiBuiltInToolBridge.HasResultValue(content.Payload))
        {
            return;
        }

        updates.Add(CreateContentsUpdate([GeminiBuiltInToolBridge.CreateToolResult(content.Payload)]));
        content.FunctionResultEmitted = true;
    }

    private ChatResponseUpdate CreateTextUpdate(string text)
    {
        return CreateUpdate(new ChatResponseUpdate(ChatRole.Assistant, text));
    }

    private ChatResponseUpdate CreateContentsUpdate(IList<AIContent> contents)
    {
        return CreateUpdate(new ChatResponseUpdate(ChatRole.Assistant, contents));
    }

    private ChatResponseUpdate CreateUpdate(ChatResponseUpdate update)
    {
        update.ResponseId = _responseId;
        update.MessageId = _messageId ?? _responseId;
        update.ConversationId = _conversationId;
        update.ModelId = _modelId;
        return update;
    }

    private static bool TryGetIndex(JsonObject? payload, out int index)
    {
        index = default;
        var node = payload?["index"];
        if (node is null)
        {
            return false;
        }

        try
        {
            index = node.GetValue<int>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MergePayload(InFlightContent content, JsonObject delta)
    {
        content.Payload ??= [];

        foreach (var property in delta)
        {
            content.Payload[property.Key] = property.Value?.DeepClone();
        }
    }

    private static UsageDetails? MapUsageDetails(JsonNode? usageNode)
    {
        if (usageNode is null)
        {
            return null;
        }

        var usage = new UsageDetails
        {
            InputTokenCount = GetInt(usageNode, "total_input_tokens") ?? GetInt(usageNode, "input_tokens") ?? GetInt(usageNode, "prompt_token_count"),
            OutputTokenCount = GetInt(usageNode, "total_output_tokens") ?? GetInt(usageNode, "output_tokens") ?? GetInt(usageNode, "candidates_token_count"),
            TotalTokenCount = GetInt(usageNode, "total_tokens") ?? GetInt(usageNode, "total_token_count"),
        };

        return usage.InputTokenCount is null && usage.OutputTokenCount is null && usage.TotalTokenCount is null
            ? null
            : usage;
    }

    private static int? GetInt(JsonNode node, string property)
    {
        var value = node[property];
        if (value is null)
        {
            return null;
        }

        try
        {
            return value.GetValue<int>();
        }
        catch
        {
            return null;
        }
    }

    private sealed class InFlightContent
    {
        public InFlightContent(string type)
        {
            Type = type;
        }

        public string Type { get; set; }

        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? CallId { get; set; }

        public JsonNode? Arguments { get; set; }

        public JsonObject? Payload { get; set; }

        public string? Signature { get; set; }

        public bool FunctionCallEmitted { get; set; }

        public bool FunctionResultEmitted { get; set; }
    }
}