using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CatHerder.Agents.AI.Gemini;
using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini.UnitTests;

public sealed class GeminiInteractionsChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_OmitsTools_WhenNoGeminiBuiltInToolsConfigured()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

        var payload = ParseCapturedPayload(handler);
        Assert.Null(payload["tools"]);
    }

    [Fact]
    public async Task GetResponseAsync_IncludesConfiguredGeminiBuiltInTools()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(
            httpClient,
            "gemini-3-flash-preview",
            new GeminiInteractionsChatClientOptions
            {
                BuiltInTools =
                [
                    GeminiBuiltInToolKind.UrlContext,
                    GeminiBuiltInToolKind.GoogleSearch,
                    GeminiBuiltInToolKind.GoogleMaps,
                    GeminiBuiltInToolKind.CodeExecution,
                ],
            });

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Summarize https://www.example.com")]);

        var payload = ParseCapturedPayload(handler);
        var tools = Assert.IsType<JsonArray>(payload["tools"]);

        Assert.Collection(
            tools,
            item => Assert.Equal("url_context", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("google_search", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("google_maps", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("code_execution", item!["type"]!.GetValue<string>()));
    }

    [Fact]
    public async Task GetResponseAsync_DeduplicatesConfiguredGeminiBuiltInTools()
    {
        var handler = new RecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(
            httpClient,
            "gemini-3-flash-preview",
            new GeminiInteractionsChatClientOptions
            {
                BuiltInTools =
                [
                    GeminiBuiltInToolKind.GoogleSearch,
                    GeminiBuiltInToolKind.GoogleSearch,
                    GeminiBuiltInToolKind.UrlContext,
                ],
            });

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Use search and the provided URL")],
            new ChatOptions { ConversationId = "interaction-123" });

        var payload = ParseCapturedPayload(handler);
        var tools = Assert.IsType<JsonArray>(payload["tools"]);

        Assert.Equal("interaction-123", payload["previous_interaction_id"]?.GetValue<string>());
        Assert.Collection(
            tools,
            item => Assert.Equal("google_search", item!["type"]!.GetValue<string>()),
            item => Assert.Equal("url_context", item!["type"]!.GetValue<string>()));
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

        var toolTurn = Assert.IsType<JsonObject>(input[2]);
        Assert.Equal("user", toolTurn["role"]!.GetValue<string>());

        var toolTurnContent = Assert.IsType<JsonArray>(toolTurn["content"]);
        var functionResult = Assert.IsType<JsonObject>(Assert.Single(toolTurnContent));

        Assert.Equal("function_result", functionResult["type"]!.GetValue<string>());
        Assert.Equal("get_weather", functionResult["name"]!.GetValue<string>());
        Assert.Equal("call-1", functionResult["call_id"]!.GetValue<string>());
        Assert.Equal("tool-result", functionResult["result"]!.GetValue<string>());
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
              "outputs": [
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
    public async Task GetResponseAsync_MapsFunctionCallOutputToFunctionCallContent()
    {
        const string responseJson = """
            {
              "id": "interaction-2",
              "model": "gemini-3-flash-preview",
              "outputs": [
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
        public async Task GetResponseAsync_MapsGeminiBuiltInToolOutputs_AndEmitsToolTelemetry()
        {
                const string responseJson = """
                        {
                            "id": "interaction-3",
                            "model": "gemini-3-flash-preview",
                            "outputs": [
                                {
                                    "type": "google_search_call",
                                    "id": "search-123",
                                    "arguments": {
                                        "queries": ["weather oslo"]
                                    }
                                },
                                {
                                    "type": "google_search_result",
                                    "call_id": "search-123",
                                    "result": [
                                        {
                                            "url": "https://example.com/weather",
                                            "title": "Example Weather"
                                        }
                                    ],
                                    "rendered_content": "<div>chips</div>"
                                },
                                {
                                    "type": "text",
                                    "text": "Oslo is cloudy today."
                                }
                            ]
                        }
                        """;

                var activities = new List<Activity>();
                using var listener = CreateAgentActivityListener(activities);
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

                var activity = Assert.Single(activities, activity => Equals(activity.GetTagItem("gen_ai.tool.call.id"), "search-123"));
                Assert.Equal("execute_tool google_search", activity.OperationName);
                Assert.Equal("google_search", activity.GetTagItem("gen_ai.tool.name"));
                Assert.Equal("search-123", activity.GetTagItem("gen_ai.tool.call.id"));
                Assert.Contains("weather oslo", activity.GetTagItem("gen_ai.tool.call.arguments")?.ToString());
                Assert.Contains("rendered_content", activity.GetTagItem("gen_ai.tool.call.result")?.ToString());
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

    private static ActivityListener CreateAgentActivityListener(List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "CatHerder.Agents.AI.Gemini",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity),
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private const string DefaultResponseJson = """
            {
              "id": "interaction-1",
              "model": "gemini-3-flash-preview",
              "outputs": [
                {
                  "type": "text",
                  "text": "ok"
                }
              ]
            }
            """;

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

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var responseBody = _responses is { Count: > 0 }
                ? _responses.Dequeue()
                : _responseJson;

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}