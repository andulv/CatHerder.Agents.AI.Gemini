namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// Provider-specific exception for malformed Gemini data after a successful response
/// or established stream.
/// </summary>
/// <remarks>
/// This exception represents Gemini protocol drift or malformed successful provider
/// data. HTTP failures and provider request rejections are represented by
/// <see cref="GeminiApiException" />.
/// </remarks>
public sealed class GeminiProtocolException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiProtocolException" /> class.
    /// </summary>
    public GeminiProtocolException(
        string message,
        string? operationName = null,
        string? sseEventType = null,
        string? jsonPath = null,
        string? responseId = null,
        string? modelId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        OperationName = operationName;
        SseEventType = sseEventType;
        JsonPath = jsonPath;
        ResponseId = responseId;
        ModelId = modelId;
    }

    /// <summary>
    /// Gets the package operation being performed when the protocol violation was observed.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    /// Gets the Gemini SSE event type when the violation came from a streaming event.
    /// </summary>
    public string? SseEventType { get; }

    /// <summary>
    /// Gets the safe JSON field or path associated with the malformed provider data.
    /// </summary>
    public string? JsonPath { get; }

    /// <summary>
    /// Gets the Gemini response or interaction id when available.
    /// </summary>
    public string? ResponseId { get; }

    /// <summary>
    /// Gets the Gemini model id when available.
    /// </summary>
    public string? ModelId { get; }
}
