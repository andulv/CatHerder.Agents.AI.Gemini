namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// Exception thrown when a serialized Gemini Interactions request exceeds the configured
/// inline size limit (<see cref="GeminiInteractionsChatClientOptions.MaxInlineRequestBytes" />).
/// The request is rejected before any HTTP call is made.
/// </summary>
/// <remarks>
/// The Gemini Interactions API accepts at most 20 MB of inline content per request
/// (https://ai.google.dev/gemini-api/docs/audio). Raising this failure before the HTTP call
/// turns an opaque provider error into a readable one; consumers can surface the message
/// or disable the check by setting <see cref="GeminiInteractionsChatClientOptions.MaxInlineRequestBytes" />
/// to <see langword="null" />.
/// </remarks>
public sealed class GeminiRequestSizeExceededException : ArgumentException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiRequestSizeExceededException" /> class.
    /// </summary>
    /// <param name="message">A human-readable description of the failure, including the actual size and the limit.</param>
    /// <param name="requestedBytes">The size in bytes of the serialized request body.</param>
    /// <param name="maxInlineRequestBytes">The configured limit in bytes that was exceeded.</param>
    public GeminiRequestSizeExceededException(string message, long requestedBytes, long maxInlineRequestBytes)
        : base(message, "messages")
    {
        RequestedBytes = requestedBytes;
        MaxInlineRequestBytes = maxInlineRequestBytes;
    }

    /// <summary>
    /// Gets the size in bytes of the serialized request body.
    /// </summary>
    public long RequestedBytes { get; }

    /// <summary>
    /// Gets the configured limit in bytes that the request exceeded.
    /// </summary>
    public long MaxInlineRequestBytes { get; }
}
