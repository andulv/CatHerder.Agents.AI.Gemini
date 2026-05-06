# Public API Sketch

**Decision timestamp:** 2026-05-06T17:29:58+02:00

## Public Types

- `GeminiInteractionsChatClient : IChatClient`
- `GeminiInteractionsChatClientOptions`
- `GeminiBuiltInToolKind`
- `GeminiApiException : HttpRequestException`
- `GeminiInteractionsChatClientExtensions`

## Constructors

```csharp
public GeminiInteractionsChatClient(
    HttpClient httpClient,
    string modelId,
    GeminiInteractionsChatClientOptions? options = null,
    ILogger? logger = null)
```

The client does not own the supplied `HttpClient`. This matches the current CatHerder behavior and keeps host applications in control of handlers, headers, auth, retries, and telemetry.

## Factory Extensions

```csharp
public static IChatClient AsGeminiInteractionsChatClient(
    this HttpClient httpClient,
    string modelId,
    GeminiInteractionsChatClientOptions? options = null,
    ILogger? logger = null)

public static IChatClient CreateGeminiInteractionsChatClient(
    string apiKey,
    string modelId,
    GeminiInteractionsChatClientOptions? options = null,
    Uri? endpoint = null,
    ILogger? logger = null)
```

`CreateGeminiInteractionsChatClient` creates an `HttpClient` with the Google AI Studio API key header and default endpoint `https://generativelanguage.googleapis.com/v1beta/`.

## Agent Extensions

```csharp
public static ChatClientAgent AsAIAgent(
    this IChatClient client,
    string? instructions = null,
    string? name = null,
    string? description = null,
    IList<AITool>? tools = null,
    Func<IChatClient, IChatClient>? clientFactory = null,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? services = null)

public static ChatClientAgent AsAIAgent(
    this IChatClient client,
    ChatClientAgentOptions options,
    Func<IChatClient, IChatClient>? clientFactory = null,
    ILoggerFactory? loggerFactory = null,
    IServiceProvider? services = null)
```

These overloads mirror the convenience shape used by Microsoft OpenAI and Anthropic provider packages while staying generic over the package's own raw-HTTP client.

## Options

`GeminiInteractionsChatClientOptions` currently exposes:

- `IReadOnlyList<GeminiBuiltInToolKind>? BuiltInTools`

Potential future options are intentionally deferred: Vertex AI auth, retry policy, response DTO parsing policy, and streaming fallback policy.

## Intentionally Unsupported In First Package

- Vertex AI authentication and regional endpoints.
- Old Gemini Generative API SDK compatibility.
- Stateful provider-managed conversation storage beyond `ChatOptions.ConversationId` mapped to `previous_interaction_id`.
- Public low-level request/response DTOs.
