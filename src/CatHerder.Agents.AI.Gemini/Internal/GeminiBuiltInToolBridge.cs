using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini;

internal static class GeminiBuiltInToolBridge
{
    private const string BuiltInToolMarkerKey = "catherder.agents.ai.gemini.built_in_tool";
    private const string ToolNameKey = "name";
    private const string ToolTypeKey = "tool_type";
    private const string IsErrorKey = "is_error";

    public static bool IsBuiltInToolCallType(string? type)
        => IsBuiltInToolType(type, "_call");

    public static bool IsBuiltInToolResultType(string? type)
        => IsBuiltInToolType(type, "_result");

    public static FunctionCallContent CreateToolCall(JsonObject payload)
    {
        var toolName = GetToolName(GetRequiredString(payload, "type"), "_call");
        var callId = GetRequiredString(payload, "id");
        var argumentsJson = payload["arguments"]?.ToJsonString() ??
            throw new InvalidOperationException($"Gemini built-in tool call '{toolName}' has null arguments.");
        var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);

        return new FunctionCallContent(callId, toolName, arguments)
        {
            InformationalOnly = true,
            AdditionalProperties = CreateAdditionalProperties(toolName),
        };
    }

    public static FunctionResultContent CreateToolResult(JsonObject payload)
    {
        var toolName = GetToolName(GetRequiredString(payload, "type"), "_result");
        var callId = GetRequiredString(payload, "call_id");
        var resultValue = GetResultValue(payload)
            ?? throw new InvalidOperationException($"Gemini built-in tool result '{toolName}' has no result payload.");

        var resultContent = new FunctionResultContent(callId, resultValue)
        {
            AdditionalProperties = CreateAdditionalProperties(toolName),
        };

        if (payload["is_error"] is JsonValue isErrorNode
            && isErrorNode.TryGetValue<bool>(out var isError))
        {
            resultContent.AdditionalProperties![IsErrorKey] = isError;
        }

        return resultContent;
    }

    public static bool HasResultValue(JsonObject payload)
        => GetResultValue(payload) is not null;

    public static bool IsInformationalBuiltInTool(AIContent content)
    {
        return content.AdditionalProperties?.TryGetValue(BuiltInToolMarkerKey, out var marker) == true
            && marker is true;
    }

    public static string GetToolName(AIContent content)
        => GetRequiredAdditionalProperty(content, ToolNameKey);

    public static string GetToolType(AIContent content)
        => GetRequiredAdditionalProperty(content, ToolTypeKey);

    public static bool IsError(FunctionResultContent content)
    {
        if (content.AdditionalProperties?.TryGetValue(IsErrorKey, out var value) != true)
        {
            return false;
        }

        return value is bool isError
            ? isError
            : throw new InvalidOperationException($"Gemini built-in tool result '{content.CallId}' has a non-boolean '{IsErrorKey}' flag.");
    }

    private static AdditionalPropertiesDictionary CreateAdditionalProperties(string toolName)
    {
        return new AdditionalPropertiesDictionary
        {
            [BuiltInToolMarkerKey] = true,
            [ToolNameKey] = toolName,
            [ToolTypeKey] = toolName,
        };
    }

    private static object? GetResultValue(JsonObject payload)
    {
        var clone = (JsonObject)payload.DeepClone();
        clone.Remove("type");
        clone.Remove("call_id");
        clone.Remove("signature");

        if (clone.Count == 0)
        {
            return null;
        }

        if (clone.Count == 1 && clone.TryGetPropertyValue("result", out var resultNode))
        {
            return resultNode?.DeepClone();
        }

        return clone;
    }

    private static string GetToolName(string type, string suffix)
    {
        if (!IsBuiltInToolType(type, suffix))
        {
            throw new InvalidOperationException($"Unsupported Gemini built-in tool type '{type}'.");
        }

        return type[..^suffix.Length];
    }

    private static bool IsBuiltInToolType(string? type, string suffix)
    {
        return !string.IsNullOrWhiteSpace(type)
            && type.EndsWith(suffix, StringComparison.Ordinal)
            && type != $"function{suffix}";
    }

    private static string GetRequiredString(JsonObject payload, string propertyName)
    {
        var value = payload[propertyName]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Gemini built-in tool payload is missing required '{propertyName}'.");
    }

    private static string GetRequiredAdditionalProperty(AIContent content, string propertyName)
    {
        var value = content.AdditionalProperties?.TryGetValue(propertyName, out var propertyValue) == true
            ? propertyValue as string
            : null;

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Gemini built-in tool content is missing required '{propertyName}'.");
    }
}
