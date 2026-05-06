namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// Options for <see cref="GeminiInteractionsChatClient" />.
/// </summary>
public sealed class GeminiInteractionsChatClientOptions
{
    /// <summary>
    /// Gets an empty options instance.
    /// </summary>
    public static GeminiInteractionsChatClientOptions Empty { get; } = new();

    /// <summary>
    /// Gets the Gemini server-side built-in tools to request for each interaction.
    /// </summary>
    public IReadOnlyList<GeminiBuiltInToolKind>? BuiltInTools { get; init; }
}

/// <summary>
/// Gemini Interactions server-side built-in tool kinds.
/// </summary>
public enum GeminiBuiltInToolKind
{
    /// <summary>
    /// Enables Gemini server-side code execution.
    /// </summary>
    CodeExecution,

    /// <summary>
    /// Enables Gemini URL context retrieval.
    /// </summary>
    UrlContext,

    /// <summary>
    /// Enables Gemini Google Search grounding.
    /// </summary>
    GoogleSearch,

    /// <summary>
    /// Enables Gemini Google Maps grounding.
    /// </summary>
    GoogleMaps,
}
