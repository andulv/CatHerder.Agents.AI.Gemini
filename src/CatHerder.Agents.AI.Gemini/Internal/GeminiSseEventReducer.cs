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
            case "interaction.created":
                CaptureInteractionMetadata(payload?["interaction"] as JsonObject);
                break;

            case "interaction.status_update":
                HandleStatusUpdate(payload, updates);
                break;

            case "step.start":
                HandleStepStart(payload, updates);
                break;

            case "step.delta":
                HandleStepDelta(payload, updates);
                break;

            case "step.stop":
                HandleStepStop(payload, updates);
                break;

            case "interaction.completed":
                HandleInteractionCompleted(payload, updates);
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

    private void HandleStatusUpdate(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        var status = payload?["status"]?.GetValue<string>();
        if (!string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var error = payload?["error"] as JsonObject ?? payload;
        var message = error?["message"]?.GetValue<string>() ?? "Gemini streaming error.";
        _logger?.LogWarning("Gemini streaming error status received: {Message}", message);
        updates.Add(CreateContentsUpdate([new ErrorContent(message)]));
    }

    private void HandleStepStart(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        if (!TryGetIndex(payload, out var index))
        {
            return;
        }

        var step = payload?["step"] as JsonObject;
        var type = step?["type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type))
        {
            _logger?.LogDebug("Ignoring Gemini SSE step.start without a step type.");
            return;
        }

        _contentByIndex[index] = new InFlightContent(type)
        {
            Id = step?["id"]?.GetValue<string>(),
            Name = step?["name"]?.GetValue<string>(),
            CallId = step?["call_id"]?.GetValue<string>(),
            Arguments = step?["arguments"]?.DeepClone(),
            Payload = step is null ? null : (JsonObject)step.DeepClone(),
        };

        if (type == "model_output")
        {
            EmitModelOutputContent(step?["content"] as JsonArray, updates);
        }
        else if (type == "thought")
        {
            EmitThoughtSummary(step, updates);
        }

        EmitFunctionCallIfReady(_contentByIndex[index], updates);
        EmitBuiltInToolCallIfReady(_contentByIndex[index], updates);
        EmitBuiltInToolResultIfReady(_contentByIndex[index], updates);
    }

    private void HandleStepDelta(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        if (!TryGetIndex(payload, out var index))
        {
            return;
        }

        var delta = payload?["delta"] as JsonObject;
        if (delta is null)
        {
            _logger?.LogDebug("Ignoring Gemini SSE step.delta without a delta payload.");
            return;
        }

        var deltaType = delta["type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(deltaType))
        {
            _logger?.LogDebug("Ignoring Gemini SSE step.delta without a delta type.");
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

            case "arguments":
                content.Type = "function_call";
                var argumentsDelta = delta["partial_arguments"]?.GetValue<string>()
                    ?? delta["arguments_delta"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(argumentsDelta))
                {
                    content.ArgumentsJsonBuilder ??= new StringBuilder();
                    content.ArgumentsJsonBuilder.Append(argumentsDelta);
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

                _logger?.LogDebug("Ignoring unsupported Gemini SSE step delta type {DeltaType}.", deltaType);
                break;
        }
    }

    private void HandleStepStop(JsonObject? payload, List<ChatResponseUpdate> updates)
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

    private void HandleInteractionCompleted(JsonObject? payload, List<ChatResponseUpdate> updates)
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

        var arguments = GetFunctionArguments(content);
        if (string.IsNullOrWhiteSpace(content.Id) || string.IsNullOrWhiteSpace(content.Name) || arguments is null)
        {
            return;
        }

        var argumentsJson = arguments.ToJsonString();
        var functionCall = FunctionCallContent.CreateFromParsedArguments(
            argumentsJson,
            content.Id,
            content.Name,
            static json => JsonSerializer.Deserialize<Dictionary<string, object?>>(json));

        updates.Add(CreateContentsUpdate([functionCall]));
        content.FunctionCallEmitted = true;
    }

    private JsonNode? GetFunctionArguments(InFlightContent content)
    {
        if (content.Arguments is not null)
        {
            return content.Arguments;
        }

        if (content.ArgumentsJsonBuilder is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            content.Arguments = JsonNode.Parse(content.ArgumentsJsonBuilder.ToString());
            return content.Arguments;
        }
        catch (JsonException ex)
        {
            _logger?.LogDebug(ex, "Ignoring incomplete Gemini streamed function-call arguments.");
            return null;
        }
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

    private void EmitModelOutputContent(JsonArray? contentBlocks, List<ChatResponseUpdate> updates)
    {
        if (contentBlocks is null)
        {
            return;
        }

        foreach (var contentBlock in contentBlocks.OfType<JsonObject>())
        {
            var type = contentBlock["type"]?.GetValue<string>();
            if (type == "text")
            {
                var text = contentBlock["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    updates.Add(CreateTextUpdate(text));
                }

                continue;
            }

            if (type == "thought")
            {
                EmitThoughtSummary(contentBlock, updates);
            }
        }
    }

    private void EmitThoughtSummary(JsonObject? thought, List<ChatResponseUpdate> updates)
    {
        if (thought?["summary"] is JsonArray summaryBlocks)
        {
            foreach (var summaryBlock in summaryBlocks.OfType<JsonObject>())
            {
                var text = summaryBlock["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    updates.Add(CreateContentsUpdate([new TextReasoningContent(text)]));
                }
            }

            return;
        }

        var summary = thought?["summary"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(summary))
        {
            updates.Add(CreateContentsUpdate([new TextReasoningContent(summary)]));
        }
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
        => GeminiUsageMapper.Map(usageNode);

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

        public StringBuilder? ArgumentsJsonBuilder { get; set; }

        public JsonObject? Payload { get; set; }

        public string? Signature { get; set; }

        public bool FunctionCallEmitted { get; set; }

        public bool FunctionResultEmitted { get; set; }
    }
}