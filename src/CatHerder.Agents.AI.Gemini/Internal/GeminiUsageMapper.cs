using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini;

internal static class GeminiUsageMapper
{
    private static readonly HashSet<string> CanonicalFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "cached_content_token_count",
        "cached_input_tokens",
        "cachedContentTokenCount",
        "cache_read_input_tokens",
        "candidates_token_count",
        "input_tokens",
        "output_tokens",
        "prompt_token_count",
        "reasoning_tokens",
        "thinking_tokens",
        "thought_tokens",
        "thoughts_token_count",
        "thoughts_tokens",
        "total_input_tokens",
        "total_output_tokens",
        "total_token_count",
        "total_tokens",
    };

    public static UsageDetails? Map(JsonNode? usageNode)
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
            CachedInputTokenCount = GetInt(usageNode, "cached_input_tokens") ?? GetInt(usageNode, "cache_read_input_tokens") ?? GetInt(usageNode, "cached_content_token_count") ?? GetInt(usageNode, "cachedContentTokenCount"),
            ReasoningTokenCount = GetInt(usageNode, "reasoning_tokens") ?? GetInt(usageNode, "thoughts_token_count") ?? GetInt(usageNode, "thoughts_tokens") ?? GetInt(usageNode, "thought_tokens") ?? GetInt(usageNode, "thinking_tokens"),
        };

        if (usageNode is JsonObject usageObject)
        {
            foreach (var property in usageObject)
            {
                if (CanonicalFields.Contains(property.Key) || !TryGetInt(property.Value, out var count))
                {
                    continue;
                }

                usage.AdditionalCounts ??= [];
                usage.AdditionalCounts[property.Key] = count;
            }
        }

        return usage.InputTokenCount is null
            && usage.OutputTokenCount is null
            && usage.TotalTokenCount is null
            && usage.CachedInputTokenCount is null
            && usage.ReasoningTokenCount is null
            && (usage.AdditionalCounts is null || usage.AdditionalCounts.Count == 0)
                ? null
                : usage;
    }

    private static int? GetInt(JsonNode node, string property)
    {
        var value = node[property];
        return TryGetInt(value, out var parsed) ? parsed : null;
    }

    private static bool TryGetInt(JsonNode? value, out int parsed)
    {
        parsed = default;
        if (value is null)
        {
            return false;
        }

        try
        {
            parsed = value.GetValue<int>();
            return true;
        }
        catch
        {
            return false;
        }
    }
}