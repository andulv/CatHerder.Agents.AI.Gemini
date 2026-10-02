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

    /// <summary>
    /// Gets or sets the maximum size in bytes of the serialized Interactions request body
    /// that is sent inline, or <see langword="null" /> to disable the pre-flight size check.
    /// The default is <c>20_000_000</c> bytes, the documented 20 MB inline request limit for
    /// the Gemini Interactions API (the limit covers the whole request: prompts and all inline
    /// files, https://ai.google.dev/gemini-api/docs/audio). Requests that exceed the limit
    /// throw <see cref="GeminiRequestSizeExceededException" /> before any HTTP call is made.
    /// </summary>
    public long? MaxInlineRequestBytes { get; set; } = 20_000_000;
}
