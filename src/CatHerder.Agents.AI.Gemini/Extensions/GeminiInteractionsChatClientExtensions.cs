using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// Extension methods for creating Gemini Interactions chat clients and Agent Framework agents.
/// </summary>
public static class GeminiInteractionsChatClientExtensions
{
    /// <summary>
    /// Creates an <see cref="IChatClient" /> over the Gemini Interactions API from an existing <see cref="HttpClient" />.
    /// </summary>
    public static IChatClient AsGeminiInteractionsChatClient(
        this HttpClient httpClient,
        string modelId,
        GeminiInteractionsChatClientOptions? options = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        return new GeminiInteractionsChatClient(httpClient, modelId, options, logger);
    }

    /// <summary>
    /// Creates an <see cref="IChatClient" /> over the Gemini Interactions API using Google AI Studio API-key authentication.
    /// </summary>
    public static IChatClient CreateGeminiInteractionsChatClient(
        string apiKey,
        string modelId,
        GeminiInteractionsChatClientOptions? options = null,
        Uri? endpoint = null,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        var httpClient = new HttpClient
        {
            BaseAddress = endpoint ?? new Uri("https://generativelanguage.googleapis.com/v1beta/"),
        };
        httpClient.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);

        return new GeminiInteractionsChatClient(httpClient, modelId, options, logger, disposeHttpClient: true);
    }

    /// <summary>
    /// Creates a <see cref="ChatClientAgent" /> backed by this chat client.
    /// </summary>
    public static ChatClientAgent AsAIAgent(
        this IChatClient client,
        string? instructions = null,
        string? name = null,
        string? description = null,
        IList<AITool>? tools = null,
        Func<IChatClient, IChatClient>? clientFactory = null,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = name,
                Description = description,
                ChatOptions = tools is null && string.IsNullOrWhiteSpace(instructions)
                    ? null
                    : new ChatOptions
                    {
                        Instructions = instructions,
                        Tools = tools,
                    },
            },
            clientFactory,
            loggerFactory,
            services);
    }

    /// <summary>
    /// Creates a <see cref="ChatClientAgent" /> backed by this chat client.
    /// </summary>
    public static ChatClientAgent AsAIAgent(
        this IChatClient client,
        ChatClientAgentOptions options,
        Func<IChatClient, IChatClient>? clientFactory = null,
        ILoggerFactory? loggerFactory = null,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        var chatClient = clientFactory is null ? client : clientFactory(client);
        return new ChatClientAgent(chatClient, options, loggerFactory, services);
    }
}