using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini;

internal static class GeminiUsageMapper
{
    public static UsageDetails? Map(JsonNode? usageNode)
    {
        if (usageNode is null)
        {
            return null;
        }

        if (usageNode is not JsonObject usageObject)
        {
            throw new GeminiProtocolException(
                "Gemini interaction usage must be a JSON object.",
                operationName: "UsageMapping",
                jsonPath: "$.usage");
        }

        var usage = new UsageDetails
        {
            InputTokenCount = GetCanonicalTokenCount(usageObject, "total_input_tokens"),
            OutputTokenCount = GetCanonicalTokenCount(usageObject, "total_output_tokens"),
            TotalTokenCount = GetCanonicalTokenCount(usageObject, "total_tokens"),
            CachedInputTokenCount = GetCanonicalTokenCount(usageObject, "total_cached_tokens"),
            ReasoningTokenCount = GetCanonicalTokenCount(usageObject, "total_thought_tokens"),
        };

        AddAdditionalCount(usage, usageObject, "total_tool_use_tokens");

        return usage.InputTokenCount is null
            && usage.OutputTokenCount is null
            && usage.TotalTokenCount is null
            && usage.CachedInputTokenCount is null
            && usage.ReasoningTokenCount is null
            && (usage.AdditionalCounts is null || usage.AdditionalCounts.Count == 0)
                ? null
                : usage;
    }

    private static void AddAdditionalCount(UsageDetails usage, JsonObject usageObject, string property)
    {
        var count = GetCanonicalTokenCount(usageObject, property);
        if (count is null)
        {
            return;
        }

        usage.AdditionalCounts ??= [];
        usage.AdditionalCounts[property] = count.Value;
    }

    private static int? GetCanonicalTokenCount(JsonObject usageObject, string property)
    {
        if (!usageObject.TryGetPropertyValue(property, out var value) || value is null)
        {
            return null;
        }

        try
        {
            return value.GetValue<int>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new GeminiProtocolException(
                $"Gemini interaction usage field '{property}' must be an integer.",
                operationName: "UsageMapping",
                jsonPath: $"$.usage.{property}",
                innerException: ex);
        }
    }
}
