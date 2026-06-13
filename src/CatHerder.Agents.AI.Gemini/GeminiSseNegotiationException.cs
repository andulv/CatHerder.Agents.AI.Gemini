using System.Net;

namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// Provider-specific exception for failures before a Gemini SSE stream is established.
/// </summary>
public sealed class GeminiSseNegotiationException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiSseNegotiationException" /> class.
    /// </summary>
    public GeminiSseNegotiationException(
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException, statusCode)
    {
    }
}
