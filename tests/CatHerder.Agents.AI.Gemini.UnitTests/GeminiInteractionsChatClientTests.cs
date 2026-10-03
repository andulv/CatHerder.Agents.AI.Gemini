using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CatHerder.Agents.AI.Gemini;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini.UnitTests;

public sealed class GeminiInteractionsChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_SendsMay2026ApiRevisionHeader()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

        Assert.Equal("2026-05-20", handler.LastApiRevision);
    }

    [Fact]
    public async Task GetResponseAsync_OmitsTools_WhenNoToolsRequested()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

        var payload = ParseCapturedPayload(handler);
        Assert.Null(payload["tools"]);
    }

    [Fact]
    public async Task GetResponseAsync_MapsHostedAndGeminiBuiltInTools_InOrder()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Summarize https://www.example.com")],
            new ChatOptions
            {
                Tools = [GeminiBuiltInTool.UrlContext, GeminiBuiltInTool.GoogleMaps, new HostedCodeInterpreterTool()],
            });

        var payload = ParseCapturedPayload(handler);
        var tools = Assert.IsType<JsonArray>(payload["tools"]);

        Assert.Collection(
            tools,
            item => Assert.Equal("url_context", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("google_maps", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("code_execution", item!["type"]!.GetValue<string>()));
    }

    [Fact]
    public async Task GetResponseAsync_DeduplicatesBuiltInTools()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Use the provided URL and run code")],
            new ChatOptions
            {
                ConversationId = "interaction-123",
                Tools =
                [
                    GeminiBuiltInTool.UrlContext,
                    new GeminiBuiltInTool(GeminiBuiltInToolKind.UrlContext),
                    new HostedCodeInterpreterTool(),
                    new HostedCodeInterpreterTool(),
                ],
            });

        var payload = ParseCapturedPayload(handler);
        var tools = Assert.IsType<JsonArray>(payload["tools"]);

        Assert.Equal("interaction-123", payload["previous_interaction_id"]?.GetValue<string>());
        Assert.Collection(
            tools,
            item => Assert.Equal("url_context", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("code_execution", item!["type"]!.GetValue<string>()));
    }

    [Fact]
    public async Task GetResponseAsync_RejectsUnsupportedToolType()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hello")],
            new ChatOptions { Tools = [new HostedFileSearchTool()] }));
    }

    [Fact]
    public async Task GetResponseAsync_MapsHostedWebSearchTool_ToGoogleSearch()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "What is in the news today?")],
            new ChatOptions { Tools = [new HostedWebSearchTool()] });

        var payload = ParseCapturedPayload(handler);
        var tools = Assert.IsType<JsonArray>(payload["tools"]);

        Assert.Collection(
            tools,
            item => Assert.Equal("google_search", item!["type"]!.GetValue<string>()));
    }

    [Fact]
    public async Task GetResponseAsync_DeduplicatesHostedWebSearchTool_AndKeepsFunctionTools()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var function = AIFunctionFactory.Create(
            (string query) => query,
            name: "echo",
            description: "Echoes the query back.");

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Search and echo")],
            new ChatOptions { Tools = [GeminiBuiltInTool.UrlContext, new HostedWebSearchTool(), new HostedWebSearchTool(), function] });

        var payload = ParseCapturedPayload(handler);
        var tools = Assert.IsType<JsonArray>(payload["tools"]);

        // google_search is deduplicated from two
        // HostedWebSearchTool instances; the function tool is mapped last.
        Assert.Collection(
            tools,
            item => Assert.Equal("url_context", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("google_search", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("function", item!["type"]!.GetValue<string>()));
    }

    [Fact]
    public async Task GetResponseAsync_UsesChatOptionsInstructionsAsSystemInstruction()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hello")],
            new ChatOptions
            {
                Instructions = "Base identity instructions.",
                ConversationId = "interaction-123",
            });

        var payload = ParseCapturedPayload(handler);

        Assert.Equal("Base identity instructions.", payload["system_instruction"]?.GetValue<string>());
        Assert.Equal("interaction-123", payload["previous_interaction_id"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetResponseAsync_MergesChatOptionsInstructionsWithSystemMessages()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, "Be terse."),
                new ChatMessage(ChatRole.User, "Hello"),
            ],
            new ChatOptions
            {
                Instructions = "Base identity instructions.",
            });

        var payload = ParseCapturedPayload(handler);

        Assert.Equal("Base identity instructions.\nBe terse.", payload["system_instruction"]?.GetValue<string>());
        Assert.Equal("Hello", payload["input"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetResponseAsync_IncludesLocalFunctionToolsFromChatOptions()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var weatherTool = AIFunctionFactory.Create(
            (string location) => $"Sunny in {location}",
            name: "get_weather",
            description: "Gets weather for a city.");

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "What is the weather in Oslo?")],
            new ChatOptions { Tools = [weatherTool] });

        var payload = ParseCapturedPayload(handler);
        var tools = Assert.IsType<JsonArray>(payload["tools"]);

        var functionTool = Assert.Single(tools);
        Assert.Equal("function", functionTool!["type"]!.GetValue<string>());
        Assert.Equal("get_weather", functionTool["name"]!.GetValue<string>());
        Assert.Equal("Gets weather for a city.", functionTool["description"]!.GetValue<string>());
        Assert.Equal("object", functionTool["parameters"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetResponseAsync_SerializesUserImageContent()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User,
            [
                new TextContent("Describe this image."),
                new DataContent(new byte[] { 0x89, 0x50, 0x4e, 0x47 }, "image/png"),
            ]),
        ]);

        var input = Assert.IsType<JsonArray>(ParseCapturedPayload(handler)["input"]);
        var image = Assert.IsType<JsonObject>(input[1]);

        Assert.Equal("image", image["type"]!.GetValue<string>());
        Assert.Equal("image/png", image["mime_type"]!.GetValue<string>());
        Assert.Equal("iVBORw==", image["data"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetResponseAsync_SerializesUserPdfContentAsDocument()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User,
            [
                new TextContent("Summarize this PDF."),
                new DataContent("%PDF-1.7\n"u8.ToArray(), "application/pdf"),
            ]),
        ]);

        var input = Assert.IsType<JsonArray>(ParseCapturedPayload(handler)["input"]);
        var document = Assert.IsType<JsonObject>(input[1]);

        Assert.Equal("document", document["type"]!.GetValue<string>());
        Assert.Equal("application/pdf", document["mime_type"]!.GetValue<string>());
        Assert.Equal("JVBERi0xLjcK", document["data"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetResponseAsync_SerializesUserAudioContent_AsAudioPart(bool inline)
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        AIContent audio = inline
            ? new DataContent(new byte[] { 0x01, 0x02, 0x03 }, "audio/wav")
            : new UriContent(new Uri("https://storage.example.com/clips/demo.wav"), "audio/wav");

        await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, "Hello"),
            new ChatMessage(ChatRole.Assistant, "Hi there."),
            new ChatMessage(ChatRole.User, [new TextContent("Describe this audio."), audio]),
        ]);

        var input = Assert.IsType<JsonArray>(ParseCapturedPayload(handler)["input"]);
        var userStep = Assert.IsType<JsonObject>(input[2]);
        var content = Assert.IsType<JsonArray>(userStep["content"]);
        var audioPart = Assert.IsType<JsonObject>(content[1]);

        Assert.Equal("audio", audioPart["type"]!.GetValue<string>());
        Assert.Equal("audio/wav", audioPart["mime_type"]!.GetValue<string>());
        if (inline)
        {
            Assert.Equal("AQID", audioPart["data"]!.GetValue<string>());
            Assert.Null(audioPart["uri"]);
        }
        else
        {
            Assert.Equal("https://storage.example.com/clips/demo.wav", audioPart["uri"]!.GetValue<string>());
            Assert.Null(audioPart["data"]);
        }
    }

    [Fact]
    public async Task GetResponseAsync_Throws_WhenUserContentHasUnsupportedMediaType()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var ex = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, [new TextContent("Watch this video."), new DataContent(new byte[] { 1, 2, 3 }, "video/mp4")])]));

        Assert.Contains("video/mp4", ex.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetResponseAsync_DropsUnsupportedMediaInAssistantTurns()
    {
        // Documented behaviour: only user turns reject unmappable media. Assistant turns are
        // replayed history; the API has no audio/video parts in model_output steps, so
        // unmappable binary content there is dropped instead of throwing.
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.Assistant, [new TextContent("Here is a clip."), new DataContent(new byte[] { 1, 2, 3 }, "video/mp4")]),
            new ChatMessage(ChatRole.User, "Tell me more."),
        ]);

        var input = Assert.IsType<JsonArray>(ParseCapturedPayload(handler)["input"]);
        var assistantStep = Assert.IsType<JsonObject>(input[0]);
        var content = Assert.IsType<JsonArray>(assistantStep["content"]);
        Assert.Single(content);
        Assert.Equal("text", content[0]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetResponseAsync_Throws_WhenRequestExceedsMaxInlineRequestBytes()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(
            httpClient,
            "gemini-3-flash-preview",
            new GeminiInteractionsChatClientOptions { MaxInlineRequestBytes = 10 });

        var ex = await Assert.ThrowsAsync<GeminiRequestSizeExceededException>(async () =>
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

        Assert.Contains(ex.RequestedBytes.ToString(), ex.Message);
        Assert.Contains("10", ex.Message);
        Assert.Contains("20 MB", ex.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_Throws_WhenRequestExceedsMaxInlineRequestBytes()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(
            httpClient,
            "gemini-3-flash-preview",
            new GeminiInteractionsChatClientOptions { MaxInlineRequestBytes = 10 });

        var updates = client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);
        var enumerator = updates.GetAsyncEnumerator();

        try
        {
            await Assert.ThrowsAsync<GeminiRequestSizeExceededException>(async () =>
                await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetResponseAsync_SendsOversizeRequest_WhenMaxInlineRequestBytesIsNull()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(
            httpClient,
            "gemini-3-flash-preview",
            new GeminiInteractionsChatClientOptions { MaxInlineRequestBytes = null });

        // ~20 MB of base64 payload: over the documented inline limit, but null disables the check.
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, [new DataContent(new byte[15_000_001], "image/png")])]);

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetResponseAsync_SerializesFunctionResultMessageAsFunctionResultContent()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var messages = new ChatMessage[]
        {
            new(ChatRole.User, "Use the tool"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_weather", new Dictionary<string, object?> { ["location"] = "Oslo" })]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "tool-result")]),
        };

        await client.GetResponseAsync(messages, new ChatOptions { ConversationId = "interaction-123" });

        var payload = ParseCapturedPayload(handler);
        var input = Assert.IsType<JsonArray>(payload["input"]);
        Assert.Equal(3, input.Count);

        var functionResult = Assert.IsType<JsonObject>(input[2]);

        Assert.Equal("function_result", functionResult["type"]!.GetValue<string>());
        Assert.Equal("get_weather", functionResult["name"]!.GetValue<string>());
        Assert.Equal("call-1", functionResult["call_id"]!.GetValue<string>());
        Assert.Equal("tool-result", functionResult["result"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetResponseAsync_SerializesStructuredFunctionResultAsText()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var messages = new ChatMessage[]
        {
            new(ChatRole.User, "List the files"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "list_files", new Dictionary<string, object?>())]),
            // A structured result: its "type" property must not become a typed part on the wire.
            new(ChatRole.Tool, [new FunctionResultContent("call-1", new[] { new { name = "a.md", type = "file" } })]),
        };

        await client.GetResponseAsync(messages);

        var input = Assert.IsType<JsonArray>(ParseCapturedPayload(handler)["input"]);
        var functionResult = Assert.IsType<JsonObject>(input[2]);
        var result = functionResult["result"]!.GetValue<string>();

        Assert.Contains("a.md", result);
        Assert.DoesNotContain("\"type\":\"function_result\"", result);
    }

    [Fact]
    public async Task GetResponseAsync_SerializesDirectImageToolResultAsMultimodalFunctionResult()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync(
        [
            new ChatMessage(ChatRole.User, "Inspect the image."),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "read_image", new Dictionary<string, object?> { ["path"] = "cat.png" })]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-1", new DataContent(new byte[] { 0x89, 0x50, 0x4e, 0x47 }, "image/png"))]),
        ]);

        var input = Assert.IsType<JsonArray>(ParseCapturedPayload(handler)["input"]);
        var functionResult = Assert.IsType<JsonObject>(input[2]);
        var result = Assert.IsType<JsonArray>(functionResult["result"]);
        var image = Assert.IsType<JsonObject>(Assert.Single(result));

        Assert.Equal("function_result", functionResult["type"]!.GetValue<string>());
        Assert.Equal("read_image", functionResult["name"]!.GetValue<string>());
        Assert.Equal("image", image["type"]!.GetValue<string>());
        Assert.Equal("image/png", image["mime_type"]!.GetValue<string>());
        Assert.Equal("iVBORw==", image["data"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetResponseAsync_Throws_WhenFunctionResultNameCannotBeResolved()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var messages = new ChatMessage[]
        {
            new(ChatRole.User, "Use the tool"),
            new(ChatRole.Tool, [new FunctionResultContent("missing-call-id", "tool-result")]),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.GetResponseAsync(messages, new ChatOptions { ConversationId = "interaction-123" }));

        Assert.Contains("Cannot serialize function_result", ex.Message);
        Assert.Contains("missing-call-id", ex.Message);
        Assert.Contains("AgentSession.StateBag", ex.Message);
        Assert.Contains("transcript function_call content", ex.Message);
        Assert.Contains("AdditionalProperties", ex.Message);
    }

    [Fact]
    public async Task GetResponseAsync_DoesNotPersist_FunctionNameAcrossClientInstances()
    {
        const string functionCallResponse = """
            {
              "id": "interaction-1",
              "model": "gemini-3-flash-preview",
              "steps": [
                {
                  "type": "function_call",
                  "id": "k9323oby",
                  "name": "run_bash_command",
                  "arguments": {
                    "command": "ls -R"
                  }
                }
              ]
            }
            """;

        var handler = new RecordingHandler(responses: [functionCallResponse]);
        using var httpClient = CreateHttpClient(handler);

        using (var firstClient = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview"))
        {
            var firstResponse = await firstClient.GetResponseAsync([new ChatMessage(ChatRole.User, "list files")]);
            Assert.Single(firstResponse.Messages.Single().Contents.OfType<FunctionCallContent>());
        }

        using var secondClient = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await secondClient.GetResponseAsync(
                [new ChatMessage(ChatRole.Tool, [new FunctionResultContent("k9323oby", "file list")])],
                new ChatOptions { ConversationId = "interaction-1" }));

        Assert.Contains("k9323oby", ex.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task AgentRun_DoesNotRecord_CarriedRequestCalls()
    {
        // Carried history (any provider) must not grow the map: the result and its call are
        // in the same request, so the transcript lookup resolves the name. Pinned on the
        // failure path — a failed request never prunes, so a recorded carried call would
        // survive here (this is the pin for the dominant growth source, plan065 outcome 1).
        var handler = new SequentialHandler(
            (HttpStatusCode.InternalServerError, """{"error":{"code":500,"status":"INTERNAL"}}""", "application/json"));
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");
        var agent = client.AsAIAgent();
        var session = await agent.CreateSessionAsync();

        var messages = new ChatMessage[]
        {
            new(ChatRole.User, "Use the tool"),
            new(ChatRole.Assistant, [new FunctionCallContent("carried-call-1", "read_image", new Dictionary<string, object?> { ["path"] = "cat.png" })]),
            new(ChatRole.Tool, [new FunctionResultContent("carried-call-1", "an image of a cat")]),
        };

        await Assert.ThrowsAnyAsync<Exception>(async () => await agent.RunAsync(messages, session));

        // The result resolved its name from the transcript, not the map:
        Assert.Contains("read_image", handler.LastRequestBody);
        var map = GetFunctionNameMap(session);
        Assert.True(map is null || map.Count == 0, "request-carried calls must not be recorded");
    }

    [Fact]
    public async Task AgentRun_Prunes_AfterSuccessfulToolLoop()
    {
        // The function loop records the call (response 1), sends the result (request 2,
        // resolved from the map), and prunes the entry once that request completed.
        const string functionCallResponse = """
            {
              "id": "interaction-1",
              "model": "gemini-3-flash-preview",
              "steps": [
                {
                  "type": "function_call",
                  "id": "k9323oby",
                  "name": "run_bash_command",
                  "arguments": {
                    "command": "ls -R"
                  }
                }
              ]
            }
            """;

        var handler = new SequentialHandler(
            (HttpStatusCode.OK, functionCallResponse, "application/json"),
            (HttpStatusCode.OK, DefaultOkResponseJson, "application/json"));
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");
        var tools = new List<AITool> { AIFunctionFactory.Create(() => "file list", name: "run_bash_command") };
        var agent = client.AsAIAgent(tools: tools);
        var session = await agent.CreateSessionAsync();

        var response = await agent.RunAsync("list files", session);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.Equal(2, handler.RequestCount); // tool loop: call, then result follow-up
        Assert.Contains("run_bash_command", handler.LastRequestBody); // name resolved from the map
        Assert.True(GetFunctionNameMap(session) is not { } map || !map.ContainsKey("k9323oby"),
            "the entry must be pruned once its result left successfully");
    }

    [Fact]
    public async Task AgentRun_KeepsEntries_WhenFollowUpFails()
    {
        const string functionCallResponse = """
            {
              "id": "interaction-1",
              "model": "gemini-3-flash-preview",
              "steps": [
                {
                  "type": "function_call",
                  "id": "k9323oby",
                  "name": "run_bash_command",
                  "arguments": {
                    "command": "ls -R"
                  }
                }
              ]
            }
            """;

        var handler = new SequentialHandler(
            (HttpStatusCode.OK, functionCallResponse, "application/json"),
            (HttpStatusCode.ServiceUnavailable, """{"error":{"code":503,"status":"UNAVAILABLE"}}""", "application/json"));
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");
        var tools = new List<AITool> { AIFunctionFactory.Create(() => "file list", name: "run_bash_command") };
        var agent = client.AsAIAgent(tools: tools);
        var session = await agent.CreateSessionAsync();

        await Assert.ThrowsAnyAsync<Exception>(async () => await agent.RunAsync("list files", session));

        // The follow-up failed, so the entry survives for a retry (plan065 outcome 2).
        Assert.NotNull(GetFunctionNameMap(session)?["k9323oby"]);
    }

    [Fact]
    public async Task AgentStreamingRun_Prunes_AfterStreamCompletes()
    {
        // Streaming runs call the client streaming end-to-end, so the tool call itself
        // arrives over SSE (recorded per streaming update) and the result follow-up is a
        // second SSE stream that completes without error.
        const string sseFunctionCall = SseFunctionCallPayload;
        const string sseCompleted = """
            event: interaction.completed
            data: {"interaction":{"id":"interaction-stream-1","status":"completed","usage":{"total_tokens":5}},"event_type":"interaction.completed"}

            """;

        var handler = new SequentialHandler(
            (HttpStatusCode.OK, sseFunctionCall, "text/event-stream"),
            (HttpStatusCode.OK, sseCompleted, "text/event-stream"));
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");
        var tools = new List<AITool> { AIFunctionFactory.Create(() => "file list", name: "run_bash_command") };
        var agent = client.AsAIAgent(tools: tools);
        var session = await agent.CreateSessionAsync();

        var updates = 0;
        await foreach (var update in agent.RunStreamingAsync("list files", session))
        {
            updates++;
        }

        Assert.True(updates > 0);
        Assert.Equal(2, handler.RequestCount); // tool loop: call, then streaming result follow-up
        Assert.True(GetFunctionNameMap(session) is not { } map || !map.ContainsKey("k9323oby"),
            "a completed stream accepted the results, so the entry must be pruned");
    }

    [Fact]
    public async Task AgentStreamingRun_KeepsEntries_WhenNegotiationFails()
    {
        // Stream 1 delivers the tool call over SSE (entry recorded); stream 2 (carrying the
        // result) fails negotiation before any update, so the entry must survive.
        var handler = new SequentialHandler(
            (HttpStatusCode.OK, SseFunctionCallPayload, "text/event-stream"),
            (HttpStatusCode.InternalServerError, "boom", "application/json"));
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");
        var tools = new List<AITool> { AIFunctionFactory.Create(() => "file list", name: "run_bash_command") };
        var agent = client.AsAIAgent(tools: tools);
        var session = await agent.CreateSessionAsync();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var update in agent.RunStreamingAsync("list files", session))
            {
            }
        });

        // An established stream that fails keeps its entries: the host may retry the same
        // results, whose calls exist only in Gemini's server-side history (plan065 outcome 2).
        Assert.NotNull(GetFunctionNameMap(session)?["k9323oby"]);
    }

    [Fact]
    public async Task GetResponseAsync_MapsFunctionCallStepToFunctionCallContent()
    {
        const string responseJson = """
            {
              "id": "interaction-2",
              "model": "gemini-3-flash-preview",
              "steps": [
                {
                  "type": "function_call",
                  "id": "call-123",
                  "name": "get_weather",
                  "arguments": {
                    "location": "Oslo"
                  }
                }
              ]
            }
            """;

        var handler = new RecordingHandler(HttpStatusCode.OK, responseJson);
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Weather?")]);
        var message = Assert.Single(response.Messages);

        var functionCall = Assert.Single(message.Contents.OfType<FunctionCallContent>());
        Assert.Equal("call-123", functionCall.CallId);
        Assert.Equal("get_weather", functionCall.Name);
        Assert.NotNull(functionCall.Arguments);
        Assert.True(functionCall.Arguments!.ContainsKey("location"));
    }

    [Fact]
    public async Task GetResponseAsync_Throws_WhenLegacyOutputsOnlyResponseIsReturned()
    {
        const string responseJson = """
            {
              "id": "interaction-legacy",
              "model": "gemini-3-flash-preview",
              "outputs": [
                {
                  "type": "text",
                  "text": "legacy"
                }
              ]
            }
            """;

        var handler = new RecordingHandler(HttpStatusCode.OK, responseJson);
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var ex = await Assert.ThrowsAsync<GeminiProtocolException>(async () =>
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

        Assert.Contains("steps", ex.Message);
        Assert.Contains("2026-05-20", ex.Message);
    }

    [Fact]
    public async Task GetResponseAsync_MapsJsonResponseFormat_ToPolymorphicResponseFormat()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");
        using var schemaDocument = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "summary": { "type": "string" }
              },
              "required": ["summary"]
            }
            """);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Summarize this.")],
            new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaDocument.RootElement.Clone()),
            });

        var payload = ParseCapturedPayload(handler);
        var responseFormat = Assert.IsType<JsonObject>(payload["response_format"]);
        Assert.Equal("text", responseFormat["type"]!.GetValue<string>());
        Assert.Equal("application/json", responseFormat["mime_type"]!.GetValue<string>());
        Assert.Equal("object", responseFormat["schema"]!["type"]!.GetValue<string>());
        Assert.Null(payload["response_mime_type"]);
    }

    [Fact]
    public async Task ResponseFormat_UnsupportedVariant_ThrowsNotSupportedException()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");
        var unsupportedFormat = (ChatResponseFormat)RuntimeHelpers.GetUninitializedObject(typeof(ChatResponseFormat));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Hello")],
                new ChatOptions { ResponseFormat = unsupportedFormat }));

        Assert.Contains("response format", exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetResponseAsync_MapsGeminiBuiltInToolSteps_AsInformationalMeaiContent()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            id = "interaction-3",
            model = "gemini-3-flash-preview",
            steps = new object[]
            {
                new
                {
                    type = "google_search_call",
                    id = "search-123",
                    arguments = new
                    {
                        queries = new[] { "weather oslo" },
                    },
                },
                new
                {
                    type = "google_search_result",
                    call_id = "search-123",
                    result = new[]
                    {
                        new
                        {
                            url = "https://example.com/weather",
                            title = "Example Weather",
                        },
                    },
                    rendered_content = "<div>chips</div>",
                },
                new
                {
                    type = "model_output",
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "Oslo is cloudy today.",
                        },
                    },
                },
            },
        });

        var handler = new RecordingHandler(HttpStatusCode.OK, responseJson);
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Weather?")]);
        var message = Assert.Single(response.Messages);

        Assert.Equal("Oslo is cloudy today.", message.Text);

        var functionCall = Assert.Single(message.Contents.OfType<FunctionCallContent>());
        Assert.True(functionCall.InformationalOnly);
        Assert.Equal("search-123", functionCall.CallId);
        Assert.Equal("google_search", functionCall.Name);
        Assert.Contains("weather oslo", functionCall.Arguments!["queries"]?.ToString());

        var functionResult = Assert.Single(message.Contents.OfType<FunctionResultContent>());
        Assert.Equal("search-123", functionResult.CallId);
        Assert.Contains("rendered_content", functionResult.Result?.ToString());
        Assert.Contains("Example Weather", functionResult.Result?.ToString());
    }

    [Fact]
    public async Task GetResponseAsync_MapsBuiltInToolCall_WithoutArguments()
    {
        // The live Gemini Interactions API returns google_search_call with no arguments field.
        // The mapper must tolerate that instead of throwing.
        var responseJson = JsonSerializer.Serialize(new
        {
            id = "interaction-noargs",
            model = "gemini-3-flash-preview",
            steps = new object[]
            {
                new
                {
                    type = "google_search_call",
                    id = "search-456",
                },
                new
                {
                    type = "google_search_result",
                    call_id = "search-456",
                    result = new[]
                    {
                        new { url = "https://example.com", title = "Example" },
                    },
                },
                new
                {
                    type = "model_output",
                    content = new[]
                    {
                        new { type = "text", text = "Grounded answer." },
                    },
                },
            },
        });

        var handler = new RecordingHandler(HttpStatusCode.OK, responseJson);
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Search?")]);
        var message = Assert.Single(response.Messages);

        Assert.Equal("Grounded answer.", message.Text);

        var functionCall = Assert.Single(message.Contents.OfType<FunctionCallContent>());
        Assert.True(functionCall.InformationalOnly);
        Assert.Equal("search-456", functionCall.CallId);
        Assert.Equal("google_search", functionCall.Name);
        Assert.NotNull(functionCall.Arguments);
        Assert.Empty(functionCall.Arguments);
    }

    [Fact]
    public async Task GetResponseAsync_MapsImageModelOutput_ToDataContent()
    {
        const string responseJson = """
            {
              "id": "interaction-image-1",
              "model": "gemini-3.1-flash-image",
              "steps": [
                {
                  "type": "model_output",
                  "content": [
                    {
                      "type": "image",
                      "mime_type": "image/jpeg",
                      "data": "/9j/"
                    }
                  ]
                }
              ]
            }
            """;

        using var httpClient = CreateHttpClient(new RecordingHandler(HttpStatusCode.OK, responseJson));
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3.1-flash-image");

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Generate an image.")]);

        var image = Assert.Single(response.Messages.Single().Contents.OfType<DataContent>());
        Assert.Equal("image/jpeg", image.MediaType);
        Assert.Equal(new byte[] { 0xff, 0xd8, 0xff }, image.Data.ToArray());
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsGeminiApiException_WithParsedProviderError()
    {
        const string errorBody = """
            {
              "error": {
                "code": 503,
                "message": "This model is currently experiencing high demand. Spikes in demand are usually temporary. Please try again later.",
                "status": "UNAVAILABLE"
              }
            }
            """;

        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable, errorBody);
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var ex = await Assert.ThrowsAsync<GeminiApiException>(async () =>
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

        Assert.Contains("Gemini API error:", ex.Message);
        Assert.Contains("high demand", ex.Message);
        Assert.Contains("status=UNAVAILABLE", ex.Message);
        Assert.Contains("code=503", ex.Message);
        Assert.Equal("503", ex.ProviderCode);
        Assert.Equal("UNAVAILABLE", ex.ProviderStatus);
        Assert.Equal(errorBody, ex.ResponseBody);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"),
        };
    }

    private static JsonObject ParseCapturedPayload(RecordingHandler handler)
    {
        Assert.False(string.IsNullOrWhiteSpace(handler.LastRequestBody));
        return JsonNode.Parse(handler.LastRequestBody!)!.AsObject();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private static readonly string DefaultResponseJson = JsonSerializer.Serialize(new
        {
            id = "interaction-1",
            model = "gemini-3-flash-preview",
            steps = new[]
            {
                new
                {
                    type = "model_output",
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "ok",
                        },
                    },
                },
            },
        });

        private readonly HttpStatusCode _statusCode;
        private readonly string _responseJson;
        private readonly Queue<string>? _responses;

        public RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string? responseJson = null, IEnumerable<string>? responses = null)
        {
            _statusCode = statusCode;
            _responseJson = responseJson ?? DefaultResponseJson;
            _responses = responses is null ? null : new Queue<string>(responses);
        }

        public string? LastRequestBody { get; private set; }

        public string? LastApiRevision { get; private set; }

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            LastApiRevision = request.Headers.TryGetValues("Api-Revision", out var apiRevisionValues)
                ? Assert.Single(apiRevisionValues)
                : null;

            var responseBody = _responses is { Count: > 0 }
                ? _responses.Dequeue()
                : _responseJson;

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private const string FunctionNamesByCallIdTestKey = "catherder.agents.ai.gemini.function_names_by_call_id";

    private const string DefaultOkResponseJson = """
        {
          "id": "interaction-1",
          "model": "gemini-3-flash-preview",
          "steps": [
            {
              "type": "model_output",
              "content": [{"type": "text", "text": "ok"}]
            }
          ]
        }
        """;

    private static Dictionary<string, string>? GetFunctionNameMap(AgentSession session) =>
        session.StateBag.TryGetValue(FunctionNamesByCallIdTestKey, out Dictionary<string, string>? map) ? map : null;

    /// <summary>SSE payload delivering a function_call step (mirrors the Phase2To4 event shapes).</summary>
    private const string SseFunctionCallPayload = """
        event: interaction.created
        data: {"interaction":{"id":"interaction-1","model":"gemini-3-flash-preview"},"event_type":"interaction.created"}

        event: step.start
        data: {"index":0,"step":{"type":"function_call","id":"k9323oby","name":"run_bash_command"},"event_type":"step.start"}

        event: step.delta
        data: {"index":0,"delta":{"type":"arguments","partial_arguments":"{\"command\":\"ls -R\"}"},"event_type":"step.delta"}

        event: step.stop
        data: {"index":0,"event_type":"step.stop"}

        event: interaction.completed
        data: {"interaction":{"id":"interaction-1","status":"requires_action","usage":{"total_tokens":77,"total_input_tokens":60,"total_output_tokens":17}},"event_type":"interaction.completed"}

        """;

    /// <summary>Recording handler with a per-response status/body/media-type queue.</summary>
    private sealed class SequentialHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Body, string MediaType)> _responses;

        public SequentialHandler(params (HttpStatusCode StatusCode, string Body, string MediaType)[] responses)
            => _responses = new Queue<(HttpStatusCode, string, string)>(responses);

        public int RequestCount { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("SequentialHandler ran out of queued responses.");
            }

            var (statusCode, body, mediaType) = _responses.Dequeue();
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            };
        }
    }

    [Theory]
    [InlineData(ReasoningEffort.None, "minimal")]
    [InlineData(ReasoningEffort.Low, "low")]
    [InlineData(ReasoningEffort.Medium, "medium")]
    [InlineData(ReasoningEffort.High, "high")]
    [InlineData(ReasoningEffort.ExtraHigh, "high")]
    public void MapThinkingLevel_TypedEffortIsAuthoritative(ReasoningEffort effort, string expected)
    {
        var options = new ChatOptions { Reasoning = new ReasoningOptions { Effort = effort } };

        Assert.Equal(expected, GeminiInteractionsChatClient.MapThinkingLevel(options));
    }

    [Fact]
    public void MapThinkingLevel_RawEffortExtendsBeyondTheTypedEnumButNeverOverridesIt()
    {
        // A level the typed enum has no member for (an extension value) is passed through.
        var extended = new ChatOptions
        {
            AdditionalProperties = new() { ["reasoning.effort"] = "minimal" },
        };
        Assert.Equal("minimal", GeminiInteractionsChatClient.MapThinkingLevel(extended));

        // "none" has no API value: minimal is the closest documented level (little to no thinking).
        var none = new ChatOptions
        {
            AdditionalProperties = new() { ["reasoning.effort"] = "none" },
        };
        Assert.Equal("minimal", GeminiInteractionsChatClient.MapThinkingLevel(none));

        // An explicit typed value wins over a raw extension value.
        var typed = new ChatOptions
        {
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High },
            AdditionalProperties = new() { ["reasoning.effort"] = "low" },
        };
        Assert.Equal("high", GeminiInteractionsChatClient.MapThinkingLevel(typed));
    }

    [Fact]
    public void MapThinkingLevel_DisabledReasoningMapsToMinimal()
    {
        var options = new ChatOptions
        {
            AdditionalProperties = new() { ["reasoning.enabled"] = false },
        };

        Assert.Equal("minimal", GeminiInteractionsChatClient.MapThinkingLevel(options));
    }
}
