namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// Options for <see cref="GeminiInteractionsChatClient" />.
/// </summary>
/// <remarks>
/// Server-side tools are requested per call through <c>ChatOptions.Tools</c>, like the
/// official MEAI adapters: <c>HostedWebSearchTool</c>, <c>HostedCodeInterpreterTool</c>,
/// and <see cref="GeminiBuiltInTool"/> for Gemini-only tools.
/// </remarks>
public sealed class GeminiInteractionsChatClientOptions
{
    /// <summary>
    /// Gets an empty options instance.
    /// </summary>
    public static GeminiInteractionsChatClientOptions Empty { get; } = new();
}
