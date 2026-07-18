using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini;

internal static class GeminiImageContentMapper
{
    public static DataContent Map(
        JsonObject payload,
        string operationName,
        string jsonPath,
        string? sseEventType = null,
        string? responseId = null,
        string? modelId = null)
    {
        var mediaType = GetRequiredString(payload, "mime_type", operationName, $"{jsonPath}.mime_type", sseEventType, responseId, modelId);
        var base64 = GetRequiredString(payload, "data", operationName, $"{jsonPath}.data", sseEventType, responseId, modelId);

        try
        {
            return new DataContent(Convert.FromBase64String(base64), mediaType);
        }
        catch (FormatException ex)
        {
            throw new GeminiProtocolException(
                "Gemini image data must be valid base64.",
                operationName: operationName,
                sseEventType: sseEventType,
                jsonPath: $"{jsonPath}.data",
                responseId: responseId,
                modelId: modelId,
                innerException: ex);
        }
    }

    private static string GetRequiredString(
        JsonObject payload,
        string propertyName,
        string operationName,
        string jsonPath,
        string? sseEventType,
        string? responseId,
        string? modelId)
    {
        if (!payload.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            throw new GeminiProtocolException(
                $"Gemini image is missing required '{propertyName}'.",
                operationName: operationName,
                sseEventType: sseEventType,
                jsonPath: jsonPath,
                responseId: responseId,
                modelId: modelId);
        }

        try
        {
            var value = node.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new GeminiProtocolException(
                $"Gemini image field '{propertyName}' must be a string.",
                operationName: operationName,
                sseEventType: sseEventType,
                jsonPath: jsonPath,
                responseId: responseId,
                modelId: modelId,
                innerException: ex);
        }

        throw new GeminiProtocolException(
            $"Gemini image field '{propertyName}' must not be empty.",
            operationName: operationName,
            sseEventType: sseEventType,
            jsonPath: jsonPath,
            responseId: responseId,
            modelId: modelId);
    }
}
