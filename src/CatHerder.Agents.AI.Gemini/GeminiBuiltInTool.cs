using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// A Gemini server-side built-in tool that has no portable Microsoft.Extensions.AI equivalent.
/// Add it to <see cref="ChatOptions.Tools"/> like any other tool.
/// </summary>
/// <remarks>
/// Use the portable MEAI tools where they exist: <see cref="HostedWebSearchTool"/> maps to
/// <c>google_search</c> and <see cref="HostedCodeInterpreterTool"/> maps to <c>code_execution</c>.
/// This mirrors how the OpenAI and Anthropic MEAI adapters expose provider-specific tools.
/// </remarks>
public sealed class GeminiBuiltInTool : AITool
{
    /// <summary>Gemini URL context retrieval (<c>url_context</c>).</summary>
    public static GeminiBuiltInTool UrlContext { get; } = new(GeminiBuiltInToolKind.UrlContext);

    /// <summary>Gemini Google Maps grounding (<c>google_maps</c>).</summary>
    public static GeminiBuiltInTool GoogleMaps { get; } = new(GeminiBuiltInToolKind.GoogleMaps);

    /// <summary>Creates a built-in tool of the given kind.</summary>
    public GeminiBuiltInTool(GeminiBuiltInToolKind kind)
    {
        Kind = kind;
        ToolType = kind switch
        {
            GeminiBuiltInToolKind.UrlContext => "url_context",
            GeminiBuiltInToolKind.GoogleMaps => "google_maps",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Gemini built-in tool."),
        };
    }

    /// <summary>The built-in tool kind.</summary>
    public GeminiBuiltInToolKind Kind { get; }

    /// <summary>The Gemini Interactions tool type sent on the wire.</summary>
    public string ToolType { get; }

    /// <inheritdoc />
    public override string Name => ToolType;
}

/// <summary>
/// Gemini Interactions server-side built-in tools without a portable MEAI tool type.
/// </summary>
public enum GeminiBuiltInToolKind
{
    /// <summary>Gemini URL context retrieval.</summary>
    UrlContext,

    /// <summary>Gemini Google Maps grounding.</summary>
    GoogleMaps,
}
