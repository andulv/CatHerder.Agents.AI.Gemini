# CatHerder.Agents.AI.Gemini

`CatHerder.Agents.AI.Gemini` provides a Microsoft Agent Framework / `Microsoft.Extensions.AI` `IChatClient` implementation for the Google Gemini Interactions API.

This package is preview-quality. It targets the Interactions API, not the older Gemini Generative API wrapper packages.

## Quick Start

```csharp
using CatHerder.Agents.AI.Gemini;
using Microsoft.Extensions.AI;

IChatClient client = GeminiInteractionsChatClientExtensions.CreateGeminiInteractionsChatClient(
    apiKey: Environment.GetEnvironmentVariable("GOOGLE_API_KEY")!,
    modelId: "gemini-3.1-pro-preview");

var response = await client.GetResponseAsync([
    new ChatMessage(ChatRole.User, "Write one sentence about Bergen.")
]);

Console.WriteLine(response.Text);
```

## ChatClientAgent

```csharp
using CatHerder.Agents.AI.Gemini;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var chatClient = GeminiInteractionsChatClientExtensions.CreateGeminiInteractionsChatClient(
    Environment.GetEnvironmentVariable("GOOGLE_API_KEY")!,
    "gemini-3.1-pro-preview");

var agent = chatClient.AsAIAgent(
    instructions: "You are a concise assistant.",
    name: "GeminiAssistant");
```

## Streaming

```csharp
await foreach (var update in client.GetStreamingResponseAsync([
    new ChatMessage(ChatRole.User, "Stream a short answer.")
]))
{
    Console.Write(update.Text);
}
```

## Built-In Tools

Gemini server-side built-in tools can be requested through `GeminiInteractionsChatClientOptions`:

```csharp
var client = GeminiInteractionsChatClientExtensions.CreateGeminiInteractionsChatClient(
    apiKey,
    "gemini-3.1-pro-preview",
    new GeminiInteractionsChatClientOptions
    {
        BuiltInTools = [GeminiBuiltInToolKind.GoogleSearch]
    });
```

Built-in tool calls and results are represented as informational `FunctionCallContent` / `FunctionResultContent` values in response content.

## Integration Tests

Live tests are skipped unless `GOOGLE_API_KEY` and `GEMINI_INTERACTIONS_MODEL` are set.

```bash
GOOGLE_API_KEY=... GEMINI_INTERACTIONS_MODEL=gemini-3.1-flash-lite-preview \
  dotnet test tests/CatHerder.Agents.AI.Gemini.IntegrationTests
```

Optional:

- `GEMINI_INTERACTIONS_BASE_URL` overrides the default endpoint.
- `GEMINI_INTERACTIONS_ENABLE_BUILTIN_TOOLS=true` enables built-in tool smoke tests.

## Current Limitations

- Google AI Studio API-key authentication only.
- Vertex AI auth is not implemented yet.
- Public low-level request/response DTOs are intentionally internal.
- Streaming event handling is based on observed Interactions SSE shapes and may evolve with the API.
