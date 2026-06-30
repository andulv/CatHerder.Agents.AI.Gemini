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
                CaptureInteractionMetadata(RequireObject(RequirePayload(payload, eventType), "interaction", eventType, "$.interaction"));
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

            case "error":
                HandleErrorEvent(payload, updates);
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
        var interactionId = OptionalString(interaction, "id", "interaction.created", "$.interaction.id");
        if (!string.IsNullOrWhiteSpace(interactionId))
        {
            _responseId = interactionId;
            _conversationId = interactionId;
            _messageId ??= interactionId;
        }

        var modelId = OptionalString(interaction, "model", "interaction.created", "$.interaction.model");
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            _modelId = modelId;
        }

    }

    private void HandleStatusUpdate(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        payload = RequirePayload(payload, "interaction.status_update");
        var status = RequireString(payload, "status", "interaction.status_update", "$.status");
        if (!string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var error = payload?["error"] as JsonObject ?? payload;
        var message = OptionalString(error, "message", "interaction.status_update", "$.error.message") ?? "Gemini streaming error.";
        var code = OptionalString(error, "code", "interaction.status_update", "$.error.code")
            ?? OptionalString(error, "status", "interaction.status_update", "$.error.status");
        _logger?.LogWarning("Gemini streaming error status received: {Message}", message);
        updates.Add(CreateContentsUpdate([
            new ErrorContent(message)
            {
                ErrorCode = code,
                Details = error?.ToJsonString(),
            },
        ]));
    }

    private void HandleErrorEvent(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        payload = RequirePayload(payload, "error");
        var error = payload["error"] as JsonObject ?? payload;
        var message = OptionalString(error, "message", "error", "$.error.message") ?? "Gemini streaming error.";
        var code = OptionalString(error, "code", "error", "$.error.code")
            ?? OptionalString(error, "status", "error", "$.error.status");
        _logger?.LogWarning("Gemini streaming error event received: {Message}", message);
        updates.Add(CreateContentsUpdate([
            new ErrorContent(message)
            {
                ErrorCode = code,
                Details = error?.ToJsonString(),
            },
        ]));
    }

    private void HandleStepStart(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        payload = RequirePayload(payload, "step.start");
        var index = RequireIndex(payload, "step.start");

        var step = RequireObject(payload, "step", "step.start", "$.step");
        var type = RequireString(step, "type", "step.start", "$.step.type");
        if (!IsSupportedStepType(type))
        {
            _logger?.LogDebug("Ignoring unsupported Gemini SSE step.start type {StepType}.", type);
            return;
        }

        var content = new InFlightContent(type)
        {
            Id = OptionalString(step, "id", "step.start", "$.step.id"),
            Name = OptionalString(step, "name", "step.start", "$.step.name"),
            CallId = OptionalString(step, "call_id", "step.start", "$.step.call_id"),
            Arguments = step["arguments"]?.DeepClone(),
            Payload = (JsonObject)step.DeepClone(),
        };
        _contentByIndex[index] = content;

        if (type == "model_output")
        {
            EmitModelOutputContent(step?["content"] as JsonArray, updates);
        }
        else if (type == "thought")
        {
            EmitThoughtSummary(step, updates);
        }

        if (type != "function_call" || !IsEmptyObject(content.Arguments))
        {
            EmitFunctionCallIfReady(content, updates, requireComplete: false);
        }

        EmitBuiltInToolCallIfReady(content, updates);
        EmitBuiltInToolResultIfReady(content, updates);
    }

    private void HandleStepDelta(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        payload = RequirePayload(payload, "step.delta");
        var index = RequireIndex(payload, "step.delta");

        var delta = RequireObject(payload, "delta", "step.delta", "$.delta");

        var deltaType = RequireString(delta, "type", "step.delta", "$.delta.type");

        switch (deltaType)
        {
            case "text":
                var content = GetOrCreateContent(index, deltaType);
                MergePayload(content, delta);
                var text = RequireString(delta, "text", "step.delta", "$.delta.text");
                if (!string.IsNullOrEmpty(text))
                {
                    updates.Add(CreateTextUpdate(text));
                }

                break;

            case "arguments":
                content = GetOrCreateContent(index, deltaType);
                MergePayload(content, delta);
                content.Type = "function_call";
                AppendArgumentsDelta(content, delta, "step.delta");

                break;

            case "arguments_delta":
                content = GetOrCreateContent(index, deltaType);
                MergePayload(content, delta);
                content.Type = "function_call";
                AppendArgumentsDelta(content, delta, "step.delta");

                break;

            case "thought_signature":
                content = GetOrCreateContent(index, deltaType);
                MergePayload(content, delta);
                content.Type = "thought";
                var signature = OptionalString(delta, "signature", "step.delta", "$.delta.signature");
                if (!string.IsNullOrWhiteSpace(signature))
                {
                    content.Signature = signature;
                }
                break;

            case "thought_summary":
                content = GetOrCreateContent(index, deltaType);
                MergePayload(content, delta);
                content.Type = "thought";
                var summary = OptionalString(delta["content"] as JsonObject, "text", "step.delta", "$.delta.content.text")
                    ?? OptionalString(delta, "text", "step.delta", "$.delta.text");
                if (summary is null)
                {
                    throw Protocol("Gemini SSE thought_summary delta must contain summary text.", "step.delta", "$.delta.text");
                }

                if (!string.IsNullOrEmpty(summary))
                {
                    updates.Add(CreateContentsUpdate([new TextReasoningContent(summary)]));
                }

                break;

            case "function_call":
                content = GetOrCreateContent(index, deltaType);
                MergePayload(content, delta);
                content.Type = "function_call";
                content.Id = OptionalString(delta, "id", "step.delta", "$.delta.id") ?? content.Id;
                content.Name = OptionalString(delta, "name", "step.delta", "$.delta.name") ?? content.Name;
                if (delta["arguments"] is not null)
                {
                    content.Arguments = delta["arguments"]?.DeepClone();
                }

                EmitFunctionCallIfReady(content, updates, requireComplete: false);
                break;

            default:
                if (GeminiBuiltInToolBridge.IsBuiltInToolCallType(deltaType))
                {
                    content = GetOrCreateContent(index, deltaType);
                    MergePayload(content, delta);
                    content.Type = deltaType;
                    content.Id = OptionalString(delta, "id", "step.delta", "$.delta.id") ?? content.Id;
                    EmitBuiltInToolCallIfReady(content, updates);
                    break;
                }

                if (GeminiBuiltInToolBridge.IsBuiltInToolResultType(deltaType))
                {
                    content = GetOrCreateContent(index, deltaType);
                    MergePayload(content, delta);
                    content.Type = deltaType;
                    content.CallId = OptionalString(delta, "call_id", "step.delta", "$.delta.call_id") ?? content.CallId;
                    EmitBuiltInToolResultIfReady(content, updates);
                    break;
                }

                _logger?.LogDebug("Ignoring unsupported Gemini SSE step delta type {DeltaType}.", deltaType);
                break;
        }
    }

    private void HandleStepStop(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        payload = RequirePayload(payload, "step.stop");
        var index = RequireIndex(payload, "step.stop");

        if (_contentByIndex.TryGetValue(index, out var content))
        {
            EmitFunctionCallIfReady(content, updates, requireComplete: true);
            EmitBuiltInToolCallIfReady(content, updates);
            EmitBuiltInToolResultIfReady(content, updates);
        }
    }

    private void HandleInteractionCompleted(JsonObject? payload, List<ChatResponseUpdate> updates)
    {
        payload = RequirePayload(payload, "interaction.completed");
        var interaction = RequireObject(payload, "interaction", "interaction.completed", "$.interaction");
        CaptureInteractionMetadata(interaction);

        foreach (var content in _contentByIndex.Values)
        {
            EmitFunctionCallIfReady(content, updates, requireComplete: true);
            EmitBuiltInToolCallIfReady(content, updates);
            EmitBuiltInToolResultIfReady(content, updates);
        }

        var usage = GeminiUsageMapper.Map(interaction["usage"]);
        var status = OptionalString(interaction, "status", "interaction.completed", "$.interaction.status");
        var finishReason = GeminiInteractionsChatClient.MapFinishReason(status);

        if (string.Equals(status, "incomplete", StringComparison.OrdinalIgnoreCase))
        {
            updates.Add(CreateContentsUpdate([
                new TextContent("[Response truncated — max output tokens reached]"),
            ]));
        }

        var contents = usage is null
            ? []
            : new List<AIContent> { new UsageContent(usage) };
        var update = CreateContentsUpdate(contents);
        update.FinishReason = finishReason;
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

    private void EmitFunctionCallIfReady(InFlightContent content, List<ChatResponseUpdate> updates, bool requireComplete)
    {
        if (content.FunctionCallEmitted || content.Type != "function_call")
        {
            return;
        }

        var arguments = GetFunctionArguments(content, requireComplete);
        if (string.IsNullOrWhiteSpace(content.Id) || string.IsNullOrWhiteSpace(content.Name) || arguments is null)
        {
            if (requireComplete)
            {
                var missing = string.IsNullOrWhiteSpace(content.Id)
                    ? "id"
                    : string.IsNullOrWhiteSpace(content.Name)
                        ? "name"
                        : "arguments";
                throw Protocol($"Gemini streamed function_call ended without required '{missing}'.", "step.stop", $"$.delta.{missing}");
            }

            return;
        }

        if (arguments is not JsonObject)
        {
            throw Protocol("Gemini streamed function_call arguments must be a JSON object.", "step.stop", "$.delta.arguments");
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

    private JsonNode? GetFunctionArguments(InFlightContent content, bool requireComplete)
    {
        if (content.ArgumentsJsonBuilder is not { Length: > 0 } && content.Arguments is not null)
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
            if (requireComplete)
            {
                throw Protocol("Gemini streamed function-call arguments were not valid JSON.", "step.stop", "$.delta.arguments", ex);
            }

            _logger?.LogDebug(ex, "Ignoring incomplete Gemini streamed function-call arguments.");
            return null;
        }
    }

    private static void AppendArgumentsDelta(InFlightContent content, JsonObject delta, string eventType)
    {
        var argumentsDelta = TryGetString(delta, "partial_arguments")
            ?? TryGetString(delta, "arguments_delta")
            ?? TryGetString(delta, "arguments");
        if (string.IsNullOrEmpty(argumentsDelta))
        {
            throw Protocol("Gemini SSE arguments delta must contain argument text.", eventType, "$.delta.partial_arguments");
        }

        content.Arguments = null;
        content.ArgumentsJsonBuilder ??= new StringBuilder();
        content.ArgumentsJsonBuilder.Append(argumentsDelta);
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

    private static int RequireIndex(JsonObject payload, string eventType)
    {
        var node = payload["index"];
        if (node is null)
        {
            throw Protocol("Gemini SSE event is missing required index.", eventType, "$.index");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw Protocol("Gemini SSE event index must be an integer.", eventType, "$.index", ex);
        }
    }

    private static bool IsEmptyObject(JsonNode? node)
        => node is JsonObject jsonObject && jsonObject.Count == 0;

    private static string? TryGetString(JsonObject jsonObject, string propertyName)
    {
        try
        {
            return jsonObject[propertyName]?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static JsonObject RequirePayload(JsonObject? payload, string eventType)
        => payload ?? throw Protocol("Gemini SSE event must contain a JSON payload.", eventType, "$");

    private static JsonObject RequireObject(JsonObject payload, string propertyName, string eventType, string jsonPath)
    {
        if (payload[propertyName] is JsonObject value)
        {
            return value;
        }

        throw Protocol($"Gemini SSE event field '{propertyName}' must be a JSON object.", eventType, jsonPath);
    }

    private static string RequireString(JsonObject payload, string propertyName, string eventType, string jsonPath)
    {
        var value = OptionalString(payload, propertyName, eventType, jsonPath);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw Protocol($"Gemini SSE event field '{propertyName}' must be a non-empty string.", eventType, jsonPath);
    }

    private static string? OptionalString(JsonObject? payload, string propertyName, string eventType, string jsonPath)
    {
        if (payload is null || !payload.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.GetValue<string>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw Protocol($"Gemini SSE event field '{propertyName}' must be a string.", eventType, jsonPath, ex);
        }
    }

    private static bool IsSupportedStepType(string type)
        => type is "model_output" or "thought" or "function_call"
            || GeminiBuiltInToolBridge.IsBuiltInToolCallType(type)
            || GeminiBuiltInToolBridge.IsBuiltInToolResultType(type);

    private static GeminiProtocolException Protocol(string message, string eventType, string jsonPath, Exception? innerException = null)
        => new(
            message,
            operationName: nameof(GeminiInteractionsChatClient.GetStreamingResponseAsync),
            sseEventType: eventType,
            jsonPath: jsonPath,
            responseId: null,
            modelId: null,
            innerException: innerException);

    private static void MergePayload(InFlightContent content, JsonObject delta)
    {
        content.Payload ??= [];

        foreach (var property in delta)
        {
            content.Payload[property.Key] = property.Value?.DeepClone();
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

        public StringBuilder? ArgumentsJsonBuilder { get; set; }

        public JsonObject? Payload { get; set; }

        public string? Signature { get; set; }

        public bool FunctionCallEmitted { get; set; }

        public bool FunctionResultEmitted { get; set; }
    }
}
