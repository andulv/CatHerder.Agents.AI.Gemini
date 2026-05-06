using System.Net;

namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// Provider-specific exception for Gemini API failures with parsed error metadata.
/// </summary>
public sealed class GeminiApiException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiApiException" /> class.
    /// </summary>
    public GeminiApiException(
        string message,
        HttpStatusCode statusCode,
        string? providerCode,
        string? providerStatus,
        string? responseBody,
        Exception? innerException = null)
        : base(message, innerException, statusCode)
    {
        ProviderCode = providerCode;
        ProviderStatus = providerStatus;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Gets the provider error code when one was present in the Gemini error payload.
    /// </summary>
    public string? ProviderCode { get; }

    /// <summary>
    /// Gets the provider error status when one was present in the Gemini error payload.
    /// </summary>
    public string? ProviderStatus { get; }

    /// <summary>
    /// Gets the raw response body returned by the Gemini API.
    /// </summary>
    public string? ResponseBody { get; }
}
