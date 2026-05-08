using System.Text.Json;

namespace CatHerder.Agents.AI.Gemini;

internal sealed record GeminiInteractionRequest
{
    public required string Model { get; init; }

    public required object Input { get; init; }

    public string? SystemInstruction { get; init; }

    public GeminiInteractionGenerationConfig? GenerationConfig { get; init; }

    public GeminiInteractionResponseFormat? ResponseFormat { get; init; }

    public IReadOnlyList<GeminiInteractionTool>? Tools { get; init; }

    public string? PreviousInteractionId { get; init; }

    public bool? Stream { get; init; }
}

internal sealed record GeminiInteractionInputStep
{
    public required string Type { get; init; }

    public IReadOnlyList<GeminiInteractionContent>? Content { get; init; }

    public string? Id { get; init; }

    public string? Name { get; init; }

    public object? Arguments { get; init; }

    public string? CallId { get; init; }

    public object? Result { get; init; }
}

internal sealed record GeminiInteractionContent
{
    public required string Type { get; init; }

    public string? Text { get; init; }

    public string? Id { get; init; }

    public string? Name { get; init; }

    public object? Arguments { get; init; }

    public string? CallId { get; init; }

    public object? Result { get; init; }

    public string? MimeType { get; init; }

    public string? Data { get; init; }

    public string? Uri { get; init; }
}

internal sealed record GeminiInteractionGenerationConfig
{
    public float? Temperature { get; init; }

    public int? MaxOutputTokens { get; init; }

    public float? TopP { get; init; }

    public int? TopK { get; init; }

    public IReadOnlyList<string>? StopSequences { get; init; }
}

internal sealed record GeminiInteractionResponseFormat
{
    public required string Type { get; init; }

    public string? MimeType { get; init; }

    public JsonElement? Schema { get; init; }
}

internal sealed record GeminiInteractionTool
{
    public required string Type { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public JsonElement? Parameters { get; init; }
}