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

Server-side tools are requested per call through `ChatOptions.Tools`, the same way as with the
official MEAI adapters (OpenAI, Anthropic, Google.GenAI):

| Tool | Gemini tool type |
|---|---|
| `HostedWebSearchTool` | `google_search` |
| `HostedCodeInterpreterTool` | `code_execution` |
| `GeminiBuiltInTool.UrlContext` | `url_context` |
| `GeminiBuiltInTool.GoogleMaps` | `google_maps` |

```csharp
var response = await client.GetResponseAsync(
    [new ChatMessage(ChatRole.User, "Search the web and compute the answer in Python.")],
    new ChatOptions { Tools = [new HostedWebSearchTool(), new HostedCodeInterpreterTool(), GeminiBuiltInTool.UrlContext] });
```

Other `AITool` types that are not `AIFunctionDeclaration` throw `NotSupportedException`.
Built-in tool calls and results are represented as informational `FunctionCallContent` / `FunctionResultContent` values in response content.

## Multimodal Input

User messages can include image, PDF, and audio content as `DataContent` (inline bytes) or
`UriContent` (a URI). These map to the corresponding Interactions content parts:

| MEAI content | Media types | Interactions part |
|---|---|---|
| `DataContent` / `UriContent` | `image/*` | `image` (inline `data` or `uri`) |
| `DataContent` / `UriContent` | `application/pdf` | `document` (inline `data` or `uri`) |
| `DataContent` / `UriContent` | `audio/*` | `audio` (inline `data` or `uri`) |

```csharp
var response = await client.GetResponseAsync([
    new ChatMessage(ChatRole.User, [
        new TextContent("Transcribe or describe this audio."),
        new DataContent(File.ReadAllBytes("clip.wav"), "audio/wav"),
    ]),
]);
```

The media type is passed through as-is (e.g. `audio/wav`, `audio/mp3`); which formats the
API accepts is documented by Google (wav, mp3, aiff, aac, ogg, flac on the Interactions
API audio page). Audio is input-only; model audio output is not mapped.

The same mapping applies to media in assistant turns (sent as `model_output`).
`function_result` carries only text and image parts, so PDF and audio returned by a function
are sent as a `user_input` step right after the function results, and the result text points
to it.

Nothing is dropped silently: other media types (e.g. `video/*`), `HostedFileContent`, and
content a message role cannot carry throw `NotSupportedException` before any request is sent.

### Inline request size

The client does not limit the request size; the API decides. Google documents 100 MB per
inline request (https://ai.google.dev/gemini-api/docs/file-input-methods); a live check on
2026-10-04 accepted a 266 MB request with inline MP3 audio.

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
- Audio is input-only; model audio output is not mapped.
- Vertex AI auth is not implemented yet.
- Public low-level request/response DTOs are intentionally internal.
- Streaming event handling is based on observed Interactions SSE shapes and may evolve with the API.
