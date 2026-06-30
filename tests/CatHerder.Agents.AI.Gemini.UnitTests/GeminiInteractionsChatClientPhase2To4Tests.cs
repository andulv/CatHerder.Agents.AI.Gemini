using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using CatHerder.Agents.AI.Gemini;
using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini.UnitTests;

public sealed class GeminiInteractionsChatClientPhase2To4Tests
{
    [Fact]
    public async Task Resolves_FunctionResult_WhenCallAppearsBeforeResult_InSameMessages()
    {
        var handler = new JsonRecordingHandler();
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
        var functionResult = Assert.IsType<JsonObject>(input[2]);

        Assert.Equal("function_result", functionResult["type"]!.GetValue<string>());
        Assert.Equal("get_weather", functionResult["name"]!.GetValue<string>());
        Assert.Equal("call-1", functionResult["call_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Resolves_FunctionResult_WhenCallAppearsAfterResult_InSameMessages()
    {
        var handler = new JsonRecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var messages = new ChatMessage[]
        {
            new(ChatRole.User, "Use the tool"),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "tool-result")]),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_weather", new Dictionary<string, object?> { ["location"] = "Oslo" })]),
        };

        await client.GetResponseAsync(messages, new ChatOptions { ConversationId = "interaction-123" });

        var payload = ParseCapturedPayload(handler);
        var input = Assert.IsType<JsonArray>(payload["input"]);
        var functionResult = Assert.IsType<JsonObject>(input[1]);

        Assert.Equal("function_result", functionResult["type"]!.GetValue<string>());
        Assert.Equal("get_weather", functionResult["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Resolves_FunctionResult_FromAdditionalProperties_WhenCallNotInTranscript()
    {
        var handler = new JsonRecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var functionResult = new FunctionResultContent("call-2", "tool-result")
        {
          AdditionalProperties = new AdditionalPropertiesDictionary
          {
            ["function_name"] = "get_weather",
          },
        };

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.Tool, [functionResult])],
            new ChatOptions { ConversationId = "interaction-123" });

        var payload = ParseCapturedPayload(handler);
        var input = Assert.IsType<JsonArray>(payload["input"]);
        var functionResultPayload = Assert.IsType<JsonObject>(Assert.Single(input));

        Assert.Equal("get_weather", functionResultPayload["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetResponseAsync_UsesTypedRequestPayload_ForToolRoundTripTranscript()
    {
        var handler = new JsonRecordingHandler();
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(
            httpClient,
            "gemini-3-flash-preview",
            new GeminiInteractionsChatClientOptions
            {
                BuiltInTools = [GeminiBuiltInToolKind.GoogleSearch],
            });

        var tool = AIFunctionFactory.Create(
            (string location) => $"Sunny in {location}",
            name: "get_weather",
            description: "Gets weather for a city.");

        var messages = new ChatMessage[]
        {
            new(ChatRole.System, "Be terse."),
            new(ChatRole.User, "Find the weather"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-9", "get_weather", new Dictionary<string, object?> { ["location"] = "Oslo" })]),
            new(ChatRole.Tool, [new FunctionResultContent("call-9", "Sunny")]),
        };

        await client.GetResponseAsync(
            messages,
            new ChatOptions
            {
                Tools = [tool],
                ConversationId = "interaction-9",
                Temperature = 0.2f,
                MaxOutputTokens = 123,
            });

        var actual = ParseCapturedPayload(handler);
        var expected = JsonNode.Parse("""
            {
              "model": "gemini-3-flash-preview",
              "input": [
                {
                  "type": "user_input",
                  "content": [
                    {
                      "type": "text",
                      "text": "Find the weather"
                    }
                  ]
                },
                {
                  "type": "function_call",
                  "id": "call-9",
                  "name": "get_weather",
                  "arguments": {
                    "location": "Oslo"
                  }
                },
                {
                  "type": "function_result",
                  "name": "get_weather",
                  "call_id": "call-9",
                  "result": "Sunny"
                }
              ],
              "system_instruction": "Be terse.",
              "generation_config": {
                "temperature": 0.2,
                "max_output_tokens": 123,
                "thinking_summaries": "auto"
              },
              "tools": [
                {
                  "type": "google_search"
                },
                {
                  "type": "function",
                  "name": "get_weather",
                  "description": "Gets weather for a city.",
                  "parameters": {
                    "type": "object",
                    "properties": {
                      "location": {
                        "type": "string"
                      }
                    },
                    "required": [
                      "location"
                    ]
                  }
                }
              ],
              "previous_interaction_id": "interaction-9"
            }
            """)!;

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"Expected payload does not match actual payload.\nActual: {actual.ToJsonString()}\nExpected: {expected.ToJsonString()}");
    }

    [Fact]
    public async Task GetStreamingResponseAsync_TextOnlyStream_EmitsOrderedTextUpdatesAndFinalUsage()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-stream-1",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": {
                        "type": "model_output"
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "text": "OK.",
                        "type": "text"
                      },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-stream-1",
                        "status": "completed",
                        "usage": {
                          "total_tokens": 12,
                          "total_input_tokens": 6,
                          "total_output_tokens": 2,
                          "total_cached_tokens": 1,
                          "total_thought_tokens": 3,
                          "total_tool_use_tokens": 4
                        },
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));
        var response = updates.ToChatResponse();

        Assert.Contains(updates, update => update.Text == "OK.");
        var usage = Assert.Single(AllContents(updates).OfType<UsageContent>());
        Assert.Equal(6, usage.Details.InputTokenCount);
        Assert.Equal(2, usage.Details.OutputTokenCount);
        Assert.Equal(12, usage.Details.TotalTokenCount);
        Assert.Equal(1, usage.Details.CachedInputTokenCount);
        Assert.Equal(3, usage.Details.ReasoningTokenCount);
        Assert.NotNull(usage.Details.AdditionalCounts);
        Assert.Equal(4, usage.Details.AdditionalCounts["total_tool_use_tokens"]);
        Assert.Equal("OK.", response.Messages.Single().Text);
        Assert.Equal("interaction-stream-1", response.ConversationId);
        Assert.Equal(12, response.Usage?.TotalTokenCount);
    }

    [Fact]
    public async Task GetResponseAsync_Throws_WhenCanonicalUsageFieldIsInvalid()
    {
        const string jsonResponse = """
            {
              "id": "interaction-invalid-usage",
              "model": "gemini-3-flash-preview",
              "steps": [
                {
                  "type": "model_output",
                  "content": [
                    {
                      "type": "text",
                      "text": "ok"
                    }
                  ]
                }
              ],
              "usage": {
                "total_tokens": "not-a-number"
              }
            }
            """;

        using var httpClient = CreateHttpClient(new JsonRecordingHandler(jsonResponse));
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var exception = await Assert.ThrowsAsync<GeminiProtocolException>(async () =>
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

        Assert.Contains("$.usage.total_tokens", exception.JsonPath);
    }

    [Theory]
    [InlineData("""
        {
          "id": "interaction-non-object-step",
          "model": "gemini-3-flash-preview",
          "steps": [ "not-an-object" ]
        }
        """, "$.steps[0]")]
    [InlineData("""
        {
          "id": "interaction-missing-step-type",
          "model": "gemini-3-flash-preview",
          "steps": [ { "content": [] } ]
        }
        """, "$.steps[0].type")]
    [InlineData("""
        {
          "id": "interaction-missing-content",
          "model": "gemini-3-flash-preview",
          "steps": [ { "type": "model_output" } ]
        }
        """, "$.steps[0].content")]
    [InlineData("""
        {
          "id": "interaction-non-object-content",
          "model": "gemini-3-flash-preview",
          "steps": [
            {
              "type": "model_output",
              "content": [ "not-an-object" ]
            }
          ]
        }
        """, "$.steps[0].content[0]")]
    [InlineData("""
        {
          "id": "interaction-missing-content-type",
          "model": "gemini-3-flash-preview",
          "steps": [
            {
              "type": "model_output",
              "content": [ { "text": "ok" } ]
            }
          ]
        }
        """, "$.steps[0].content[0].type")]
    [InlineData("""
        {
          "id": "interaction-missing-function-id",
          "model": "gemini-3-flash-preview",
          "steps": [
            {
              "type": "function_call",
              "name": "get_weather",
              "arguments": {}
            }
          ]
        }
        """, "$.steps[0].id")]
    [InlineData("""
        {
          "id": "interaction-missing-function-name",
          "model": "gemini-3-flash-preview",
          "steps": [
            {
              "type": "function_call",
              "id": "call-1",
              "arguments": {}
            }
          ]
        }
        """, "$.steps[0].name")]
    [InlineData("""
        {
          "id": "interaction-invalid-arguments",
          "model": "gemini-3-flash-preview",
          "steps": [
            {
              "type": "function_call",
              "id": "call-1",
              "name": "get_weather",
              "arguments": "not-an-object"
            }
          ]
        }
        """, "$.steps[0].arguments")]
    [InlineData("""
        {
          "id": "interaction-invalid-usage-shape",
          "model": "gemini-3-flash-preview",
          "steps": [
            {
              "type": "model_output",
              "content": [
                {
                  "type": "text",
                  "text": "ok"
                }
              ]
            }
          ],
          "usage": "not-an-object"
        }
        """, "$.usage")]
    public async Task GetResponseAsync_ThrowsGeminiProtocolException_ForMalformedCurrentSchema(string jsonResponse, string jsonPath)
    {
        using var httpClient = CreateHttpClient(new JsonRecordingHandler(jsonResponse));
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var exception = await Assert.ThrowsAsync<GeminiProtocolException>(async () =>
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

        var expectedOperation = jsonPath.StartsWith("$.usage", StringComparison.Ordinal)
            ? "UsageMapping"
            : nameof(GeminiInteractionsChatClient.GetResponseAsync);
        Assert.Equal(expectedOperation, exception.OperationName);
        Assert.Equal(jsonPath, exception.JsonPath);
        Assert.NotEmpty(exception.Message);
    }

    [Fact]
    public async Task GetResponseAsync_PreservesZeroCanonicalUsageFields()
    {
        const string jsonResponse = """
            {
              "id": "interaction-zero-usage",
              "model": "gemini-3-flash-preview",
              "steps": [
                {
                  "type": "model_output",
                  "content": [
                    {
                      "type": "text",
                      "text": "ok"
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 0,
                "total_output_tokens": 0,
                "total_tokens": 0,
                "total_cached_tokens": 0,
                "total_thought_tokens": 0
              }
            }
            """;

        using var httpClient = CreateHttpClient(new JsonRecordingHandler(jsonResponse));
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]);

        Assert.NotNull(response.Usage);
        Assert.Equal(0, response.Usage.InputTokenCount);
        Assert.Equal(0, response.Usage.OutputTokenCount);
        Assert.Equal(0, response.Usage.TotalTokenCount);
        Assert.Equal(0, response.Usage.CachedInputTokenCount);
        Assert.Equal(0, response.Usage.ReasoningTokenCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_FunctionCallStream_EmitsFunctionCallAndFinalResponse()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-stream-2",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": {
                        "type": "function_call",
                        "id": "call-123",
                        "name": "get_weather"
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "type": "arguments",
                        "partial_arguments": "{\"location\":\"Oslo\"}"
                      },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-stream-2",
                        "status": "requires_action",
                        "usage": {
                          "total_tokens": 77,
                          "total_input_tokens": 60,
                          "total_output_tokens": 17
                        },
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Weather?")]));
        var response = updates.ToChatResponse();

        var functionCall = Assert.Single(AllContents(updates).OfType<FunctionCallContent>());
        Assert.Equal("call-123", functionCall.CallId);
        Assert.Equal("get_weather", functionCall.Name);
        Assert.Equal("Oslo", functionCall.Arguments!["location"]?.ToString());

        var responseFunctionCall = Assert.Single(response.Messages.Single().Contents.OfType<FunctionCallContent>());
        Assert.Equal("call-123", responseFunctionCall.CallId);
        Assert.Equal(77, response.Usage?.TotalTokenCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_FunctionCallStream_UsesArgumentsDeltaAfterEmptyStartArguments()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-stream-arguments-delta",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": {
                        "type": "function_call",
                        "id": "call-arguments-delta",
                        "name": "RunBashCommand",
                        "arguments": {}
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "type": "arguments_delta",
                        "arguments": "{\"command\":\"ls -la /workspace\"}"
                      },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-stream-arguments-delta",
                        "status": "requires_action",
                        "usage": {
                          "total_tokens": 77,
                          "total_input_tokens": 60,
                          "total_output_tokens": 17
                        },
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "List files")]));

        var functionCall = Assert.Single(AllContents(updates).OfType<FunctionCallContent>());
        Assert.Equal("call-arguments-delta", functionCall.CallId);
        Assert.Equal("RunBashCommand", functionCall.Name);
        Assert.Equal("ls -la /workspace", functionCall.Arguments!["command"]?.ToString());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_GeminiBuiltInToolStream_EmitsInformationalToolContent_AndToolTelemetry()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-stream-built-in",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": {
                        "type": "google_search_call",
                        "id": "search-456",
                        "arguments": {
                          "queries": ["restaurants bergen"]
                        }
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 1,
                      "step": {
                        "type": "google_search_result",
                        "call_id": "search-456",
                        "result": [
                          {
                            "url": "https://example.com/bergen",
                            "title": "Bergen Restaurants"
                          }
                        ],
                        "rendered_content": "<div>chips</div>"
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 1,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 2,
                      "step": {
                        "type": "model_output"
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 2,
                      "delta": {
                        "type": "text",
                        "text": "Try these places."
                      },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 2,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-stream-built-in",
                        "status": "completed",
                        "usage": {
                          "total_tokens": 15,
                          "total_input_tokens": 8,
                          "total_output_tokens": 7
                        },
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Find restaurants") ]));
        var response = updates.ToChatResponse();

        var functionCall = Assert.Single(AllContents(updates).OfType<FunctionCallContent>());
        Assert.True(functionCall.InformationalOnly);
        Assert.Equal("search-456", functionCall.CallId);
        Assert.Equal("google_search", functionCall.Name);

        var functionResult = Assert.Single(AllContents(updates).OfType<FunctionResultContent>());
        Assert.Equal("search-456", functionResult.CallId);
        Assert.Contains("rendered_content", functionResult.Result?.ToString());
        Assert.Equal("Try these places.", response.Messages.Single().Text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_MixedTextAndThought_EmitsTextAndReasoningWithoutThrowing()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-stream-3",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": {
                        "type": "thought",
                        "signature": "sig-1",
                        "summary": [
                          {
                            "type": "text",
                            "text": "Thinking..."
                          }
                        ]
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 1,
                      "step": {
                        "type": "model_output"
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 1,
                      "delta": {
                        "text": "Answer",
                        "type": "text"
                      },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 1,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-stream-3",
                        "status": "completed",
                        "usage": {
                          "total_tokens": 10,
                          "total_input_tokens": 5,
                          "total_output_tokens": 5
                        },
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Think")]));
        var response = updates.ToChatResponse();

        Assert.Contains(AllContents(updates), content => content is TextReasoningContent reasoning && reasoning.Text == "Thinking...");
        Assert.Contains(updates, update => update.Text == "Answer");
        Assert.Equal("Answer", response.Messages.Single().Text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_UnknownEventType_IsIgnored()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-stream-4",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("weird.event", """
                    {
                      "foo": "bar"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": {
                        "type": "model_output"
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "text": "OK",
                        "type": "text"
                      },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-stream-4",
                        "status": "completed",
                        "usage": {
                          "total_tokens": 3,
                          "total_input_tokens": 2,
                          "total_output_tokens": 1
                        },
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]));
        var response = updates.ToChatResponse();

        Assert.Equal("OK", response.Messages.Single().Text);
    }

    [Fact]
    public async Task ErrorContent_StreamingStatusError_MapsProviderCodeAndDetails()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.status_update", """
                    {
                      "status": "error",
                      "error": {
                        "code": "RESOURCE_EXHAUSTED",
                        "message": "Quota exceeded.",
                        "status": "RESOURCE_EXHAUSTED"
                      },
                      "event_type": "interaction.status_update"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]));

        var error = Assert.Single(AllContents(updates).OfType<ErrorContent>());
        Assert.Equal("Quota exceeded.", error.Message);
        Assert.Equal("RESOURCE_EXHAUSTED", error.ErrorCode);
        Assert.Contains("RESOURCE_EXHAUSTED", error.Details?.ToString());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_CompletedStatus_MapsStopFinishReason()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-stream-finish",
                        "status": "in_progress",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-stream-finish",
                        "status": "completed",
                        "usage": {
                          "total_tokens": 1
                        },
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]));

        Assert.Contains(updates, update => update.FinishReason == ChatFinishReason.Stop);
    }

    [Theory]
    [InlineData("event: step.delta\ndata: {\n\n", "step.delta", "$")]
    [InlineData("event: step.delta\ndata: []\n\n", "step.delta", "$")]
    [InlineData("data: {\"index\":0,\"delta\":{\"type\":\"text\",\"text\":\"OK\"}}\n\n", null, "event")]
    [InlineData("event: step.start\ndata: {\"step\":{\"type\":\"model_output\"}}\n\n", "step.start", "$.index")]
    [InlineData("event: step.delta\ndata: {\"index\":\"zero\",\"delta\":{\"type\":\"text\",\"text\":\"OK\"}}\n\n", "step.delta", "$.index")]
    [InlineData("event: step.delta\ndata: {\"index\":0,\"delta\":{\"text\":\"OK\"}}\n\n", "step.delta", "$.delta.type")]
    [InlineData("event: step.delta\ndata: {\"index\":0,\"delta\":{\"type\":\"arguments\"}}\n\n", "step.delta", "$.delta.partial_arguments")]
    public async Task GeminiSse_MalformedKnownFrame_ThrowsGeminiProtocolException(string ssePayload, string? eventType, string jsonPath)
    {
        var handler = new StreamingRequestHandler(CreateSseResponse(ssePayload));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var exception = await Assert.ThrowsAsync<GeminiProtocolException>(async () =>
            await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")])));

        Assert.Equal(nameof(GeminiInteractionsChatClient.GetStreamingResponseAsync), exception.OperationName);
        Assert.Equal(eventType, exception.SseEventType);
        Assert.Equal(jsonPath, exception.JsonPath);
    }

    [Fact]
    public async Task GeminiSse_IncompleteFunctionCallArguments_ThrowsGeminiProtocolException()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": {
                        "type": "function_call",
                        "id": "call-1",
                        "name": "get_weather"
                      },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "type": "arguments",
                        "partial_arguments": "{\"location\":"
                      },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var exception = await Assert.ThrowsAsync<GeminiProtocolException>(async () =>
            await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Weather?")])));

        Assert.Equal(nameof(GeminiInteractionsChatClient.GetStreamingResponseAsync), exception.OperationName);
        Assert.Equal("step.stop", exception.SseEventType);
        Assert.Equal("$.delta.arguments", exception.JsonPath);
    }

    [Fact]
    public async Task StreamingFallback_ServerReturnsJson_ThrowsByDefault()
    {
        const string jsonResponse = """
            {
              "id": "interaction-json-fallback",
              "model": "gemini-3-flash-preview",
              "steps": [
                {
                  "type": "model_output",
                  "content": [
                    {
                      "type": "text",
                      "text": "fallback ok"
                    }
                  ]
                }
              ],
              "usage": {
                "total_input_tokens": 2,
                "total_output_tokens": 2,
                "total_tokens": 4
              }
            }
            """;

        var handler = new StreamingRequestHandler(
            CreateJsonResponse(jsonResponse));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var exception = await Assert.ThrowsAsync<GeminiSseNegotiationException>(async () =>
            await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")])));

        Assert.Contains("Content-Type", exception.Message);
        var requestBody = Assert.Single(handler.RequestBodies);
        Assert.Contains("\"stream\":true", requestBody);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_Cancellation_MidStream_StopsCleanly()
    {
        var firstChunk = CreateSsePayload(
            BuildEvent("interaction.created", """
                {
                  "interaction": {
                    "id": "interaction-stream-5",
                    "status": "in_progress",
                    "object": "interaction",
                    "model": "gemini-3-flash-preview"
                  },
                  "event_type": "interaction.created"
                }
                """),
            BuildEvent("step.start", """
                {
                  "index": 0,
                  "step": {
                    "type": "model_output"
                  },
                  "event_type": "step.start"
                }
                """),
            BuildEvent("step.delta", """
                {
                  "index": 0,
                  "delta": {
                    "text": "partial",
                    "type": "text"
                  },
                  "event_type": "step.delta"
                }
                """));

        var handler = new StreamingRequestHandler(CreateStreamingResponse(new BlockingAfterFirstChunkContent(firstChunk)));
        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        using var cts = new CancellationTokenSource();
        await using var enumerator = client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")], cancellationToken: cts.Token)
            .GetAsyncEnumerator(cts.Token);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("partial", enumerator.Current.Text);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_StandaloneErrorEvent_EmitsErrorContent()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-err",
                        "status": "in_progress",
                        "model": "gemini-3.5-flash"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("error", """
                    {
                      "error": {
                        "message": "High demand, try again later.",
                        "code": "api_error"
                      },
                      "event_type": "error"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3.5-flash");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]));

        var error = Assert.Single(AllContents(updates).OfType<ErrorContent>());
        Assert.Equal("High demand, try again later.", error.Message);
        Assert.Equal("api_error", error.ErrorCode);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_IncompleteStatus_EmitsTruncationWarning()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-trunc",
                        "status": "in_progress",
                        "model": "gemini-3.5-flash"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": { "type": "model_output" },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 0,
                      "delta": { "text": "Her er", "type": "text" },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-trunc",
                        "status": "incomplete",
                        "usage": {
                          "total_tokens": 100,
                          "total_input_tokens": 98,
                          "total_output_tokens": 2
                        },
                        "model": "gemini-3.5-flash"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3.5-flash");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]));

        var textContents = AllContents(updates).OfType<TextContent>().Select(c => c.Text).ToList();
        Assert.Contains(textContents, t => t!.Contains("truncated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(updates, u => u.FinishReason == ChatFinishReason.Length);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_CompletedStatus_DoesNotEmitTruncationWarning()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.created", """
                    {
                      "interaction": {
                        "id": "interaction-ok",
                        "status": "in_progress",
                        "model": "gemini-3.5-flash"
                      },
                      "event_type": "interaction.created"
                    }
                    """),
                BuildEvent("step.start", """
                    {
                      "index": 0,
                      "step": { "type": "model_output" },
                      "event_type": "step.start"
                    }
                    """),
                BuildEvent("step.delta", """
                    {
                      "index": 0,
                      "delta": { "text": "Full response", "type": "text" },
                      "event_type": "step.delta"
                    }
                    """),
                BuildEvent("step.stop", """
                    {
                      "index": 0,
                      "event_type": "step.stop"
                    }
                    """),
                BuildEvent("interaction.completed", """
                    {
                      "interaction": {
                        "id": "interaction-ok",
                        "status": "completed",
                        "usage": {
                          "total_tokens": 10,
                          "total_input_tokens": 8,
                          "total_output_tokens": 2
                        },
                        "model": "gemini-3.5-flash"
                      },
                      "event_type": "interaction.completed"
                    }
                    """),
                BuildEvent("done", "[DONE]"))));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3.5-flash");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]));

        var textContents = AllContents(updates).OfType<TextContent>().Select(c => c.Text).ToList();
        Assert.DoesNotContain(textContents, t => t!.Contains("truncated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetResponseAsync_ThinkingConfig_EffortFromReasoningOptions()
    {
        var handler = new JsonRecordingHandler("""
                {
                  "id": "interaction-thinking",
                  "steps": [
                    {
                      "type": "model_output",
                      "content": [{"type": "text", "text": "ok"}]
                    }
                  ],
                  "status": "completed",
                  "model": "gemini-3.5-flash",
                  "usage": { "total_tokens": 1 }
                }
                """);

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3.5-flash");

        var options = new ChatOptions { Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High } };
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")], options, CancellationToken.None);

        var payload = ParseCapturedPayload(handler);
        var genConfig = payload["generation_config"]!.AsObject();
        Assert.Equal("auto", genConfig["thinking_summaries"]!.GetValue<string>());
        Assert.Equal("high", genConfig["thinking_level"]!.GetValue<string>());
        Assert.Null(genConfig["thinking_config"]);
    }

    [Fact]
    public async Task GetResponseAsync_ThinkingConfig_RawEffortFromAdditionalProperties()
    {
        var handler = new JsonRecordingHandler("""
                {
                  "id": "interaction-thinking2",
                  "status": "completed",
                  "model": "gemini-3.5-flash",
                  "steps": [
                    {
                      "type": "model_output",
                      "content": [{"type": "text", "text": "ok"}]
                    }
                  ],
                  "usage": { "total_tokens": 1 }
                }
                """);

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3.5-flash");

        var options = new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { ["reasoning.effort"] = "minimal" },
        };
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")], options, CancellationToken.None);

        var payload = ParseCapturedPayload(handler);
        var genConfig = payload["generation_config"]!.AsObject();
        Assert.Equal("auto", genConfig["thinking_summaries"]!.GetValue<string>());
        Assert.Equal("minimal", genConfig["thinking_level"]!.GetValue<string>());
        Assert.Null(genConfig["thinking_config"]);
    }

    [Fact]
    public async Task GetResponseAsync_NoReasoning_StillSendsThinkingSummaries()
    {
        var handler = new JsonRecordingHandler("""
                {
                  "id": "interaction-thinking2",
                  "status": "completed",
                  "model": "gemini-3.5-flash",
                  "steps": [
                    {
                      "type": "model_output",
                      "content": [{"type": "text", "text": "ok"}]
                    }
                  ],
                  "usage": { "total_tokens": 1 }
                }
                """);

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3.5-flash");

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hi")], null, CancellationToken.None);

        var payload = ParseCapturedPayload(handler);
        var genConfig = payload["generation_config"]!.AsObject();
        Assert.Equal("auto", genConfig["thinking_summaries"]!.GetValue<string>());
        Assert.Null(genConfig["thinking_level"]);
        Assert.Null(genConfig["thinking_config"]);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"),
        };
    }

    private static JsonObject ParseCapturedPayload(JsonRecordingHandler handler)
    {
        Assert.False(string.IsNullOrWhiteSpace(handler.LastRequestBody));
        return JsonNode.Parse(handler.LastRequestBody!)!.AsObject();
    }

    private static async Task<List<ChatResponseUpdate>> CollectUpdatesAsync(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        var results = new List<ChatResponseUpdate>();
        await foreach (var update in updates)
        {
            results.Add(update);
        }

        return results;
    }

    private static IEnumerable<AIContent> AllContents(IEnumerable<ChatResponseUpdate> updates)
    {
        foreach (var update in updates)
        {
            if (update.Contents is not { Count: > 0 })
            {
                continue;
            }

            foreach (var content in update.Contents)
            {
                yield return content;
            }
        }
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage CreateSseResponse(string ssePayload)
    {
        return CreateStreamingResponse(new StringContent(ssePayload, Encoding.UTF8, "text/event-stream"));
    }

    private static HttpResponseMessage CreateStreamingResponse(HttpContent content)
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content,
        };
    }

    private static string BuildEvent(string eventType, string data)
    {
        var dataLines = data
            .Trim()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => $"data: {line.Trim()}");

        return $"event: {eventType}\n{string.Join("\n", dataLines)}";
    }

    private static string CreateSsePayload(params string[] frames)
    {
        return string.Join("\n\n", frames) + "\n\n";
    }

    private sealed class JsonRecordingHandler : HttpMessageHandler
    {
        private const string DefaultResponseJson = """
            {
              "id": "interaction-1",
              "model": "gemini-3-flash-preview",
              "steps": [
                {
                  "type": "model_output",
                  "content": [
                    {
                      "type": "text",
                      "text": "ok"
                    }
                  ]
                }
              ]
            }
            """;

        private readonly string _responseJson;

        public JsonRecordingHandler(string? responseJson = null)
        {
            _responseJson = responseJson ?? DefaultResponseJson;
        }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StreamingRequestHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public StreamingRequestHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }

    private sealed class BlockingAfterFirstChunkContent : HttpContent
    {
        private readonly byte[] _firstChunk;

        public BlockingAfterFirstChunkContent(string firstChunk)
        {
            _firstChunk = Encoding.UTF8.GetBytes(firstChunk);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            throw new NotSupportedException();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult<Stream>(new BlockingAfterFirstChunkStream(_firstChunk));
        }
    }

    private sealed class BlockingAfterFirstChunkStream : Stream
    {
        private readonly byte[] _firstChunk;
        private int _position;
        private bool _firstChunkCompleted;

        public BlockingAfterFirstChunkStream(byte[] firstChunk)
        {
            _firstChunk = firstChunk;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_firstChunkCompleted)
            {
                var remaining = _firstChunk.Length - _position;
                if (remaining > 0)
                {
                    var bytesToCopy = Math.Min(buffer.Length, remaining);
                    _firstChunk.AsMemory(_position, bytesToCopy).CopyTo(buffer);
                    _position += bytesToCopy;
                    if (_position >= _firstChunk.Length)
                    {
                        _firstChunkCompleted = true;
                    }

                    return bytesToCopy;
                }

                _firstChunkCompleted = true;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
