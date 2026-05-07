using System.Diagnostics;
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
        var toolTurn = Assert.IsType<JsonObject>(input[2]);
        var toolTurnContent = Assert.IsType<JsonArray>(toolTurn["content"]);
        var functionResult = Assert.IsType<JsonObject>(Assert.Single(toolTurnContent));

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
        var toolTurn = Assert.IsType<JsonObject>(input[1]);
        var toolTurnContent = Assert.IsType<JsonArray>(toolTurn["content"]);
        var functionResult = Assert.IsType<JsonObject>(Assert.Single(toolTurnContent));

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
        var toolTurn = Assert.IsType<JsonObject>(Assert.Single(input));
        var toolTurnContent = Assert.IsType<JsonArray>(toolTurn["content"]);
        var functionResultPayload = Assert.IsType<JsonObject>(Assert.Single(toolTurnContent));

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
                  "role": "user",
                  "content": [
                    {
                      "type": "text",
                      "text": "Find the weather"
                    }
                  ]
                },
                {
                  "role": "model",
                  "content": [
                    {
                      "type": "function_call",
                      "id": "call-9",
                      "name": "get_weather",
                      "arguments": {
                        "location": "Oslo"
                      }
                    }
                  ]
                },
                {
                  "role": "user",
                  "content": [
                    {
                      "type": "function_result",
                      "name": "get_weather",
                      "call_id": "call-9",
                      "result": "Sunny"
                    }
                  ]
                }
              ],
              "system_instruction": "Be terse.",
              "generation_config": {
                "temperature": 0.2,
                "max_output_tokens": 123
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
                BuildEvent("interaction.start", """
                    {
                      "interaction": {
                        "id": "interaction-stream-1",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.start"
                    }
                    """),
                BuildEvent("content.start", """
                    {
                      "index": 0,
                      "content": {
                        "type": "text"
                      },
                      "event_type": "content.start"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "text": "OK.",
                        "type": "text"
                      },
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.stop", """
                    {
                      "index": 0,
                      "event_type": "content.stop"
                    }
                    """),
                BuildEvent("interaction.complete", """
                    {
                      "interaction": {
                        "id": "interaction-stream-1",
                        "status": "completed",
                        "usage": {
                          "total_tokens": 12,
                          "total_input_tokens": 6,
                          "total_output_tokens": 2,
                          "cached_input_tokens": 1,
                          "thoughts_token_count": 3,
                          "tool_use_prompt_token_count": 4
                        },
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.complete"
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
        Assert.Equal(4, usage.Details.AdditionalCounts["tool_use_prompt_token_count"]);
        Assert.Equal("OK.", response.Messages.Single().Text);
        Assert.Equal("interaction-stream-1", response.ConversationId);
        Assert.Equal(12, response.Usage?.TotalTokenCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_FunctionCallStream_EmitsFunctionCallAndFinalResponse()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.start", """
                    {
                      "interaction": {
                        "id": "interaction-stream-2",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.start"
                    }
                    """),
                BuildEvent("content.start", """
                    {
                      "index": 0,
                      "content": {
                        "type": "function_call",
                        "id": "call-123"
                      },
                      "event_type": "content.start"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "name": "get_weather",
                        "arguments": {
                          "location": "Oslo"
                        },
                        "type": "function_call",
                        "id": "call-123"
                      },
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.stop", """
                    {
                      "index": 0,
                      "event_type": "content.stop"
                    }
                    """),
                BuildEvent("interaction.complete", """
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
                      "event_type": "interaction.complete"
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
    public async Task GetStreamingResponseAsync_GeminiBuiltInToolStream_EmitsInformationalToolContent_AndToolTelemetry()
    {
        var activities = new List<Activity>();
        using var listener = CreateAgentActivityListener(activities);
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.start", """
                    {
                      "interaction": {
                        "id": "interaction-stream-built-in",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.start"
                    }
                    """),
                BuildEvent("content.start", """
                    {
                      "index": 0,
                      "content": {
                        "type": "google_search_call",
                        "id": "search-456"
                      },
                      "event_type": "content.start"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "type": "google_search_call",
                        "id": "search-456",
                        "arguments": {
                          "queries": ["restaurants bergen"]
                        }
                      },
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.stop", """
                    {
                      "index": 0,
                      "event_type": "content.stop"
                    }
                    """),
                BuildEvent("content.start", """
                    {
                      "index": 1,
                      "content": {
                        "type": "google_search_result",
                        "call_id": "search-456"
                      },
                      "event_type": "content.start"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 1,
                      "delta": {
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
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.stop", """
                    {
                      "index": 1,
                      "event_type": "content.stop"
                    }
                    """),
                BuildEvent("content.start", """
                    {
                      "index": 2,
                      "content": {
                        "type": "text"
                      },
                      "event_type": "content.start"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 2,
                      "delta": {
                        "type": "text",
                        "text": "Try these places."
                      },
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.stop", """
                    {
                      "index": 2,
                      "event_type": "content.stop"
                    }
                    """),
                BuildEvent("interaction.complete", """
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
                      "event_type": "interaction.complete"
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

        var activity = Assert.Single(activities, activity => Equals(activity.GetTagItem("gen_ai.tool.call.id"), "search-456"));
        Assert.Equal("execute_tool google_search", activity.OperationName);
        Assert.Equal("search-456", activity.GetTagItem("gen_ai.tool.call.id"));
        Assert.Contains("restaurants bergen", activity.GetTagItem("gen_ai.tool.call.arguments")?.ToString());
        Assert.Contains("rendered_content", activity.GetTagItem("gen_ai.tool.call.result")?.ToString());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_MixedTextAndThought_EmitsTextAndReasoningWithoutThrowing()
    {
        var handler = new StreamingRequestHandler(
            CreateSseResponse(CreateSsePayload(
                BuildEvent("interaction.start", """
                    {
                      "interaction": {
                        "id": "interaction-stream-3",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.start"
                    }
                    """),
                BuildEvent("content.start", """
                    {
                      "index": 0,
                      "content": {
                        "type": "thought"
                      },
                      "event_type": "content.start"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "signature": "sig-1",
                        "type": "thought_signature"
                      },
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "type": "thought_summary",
                        "content": {
                          "text": "Thinking..."
                        }
                      },
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.stop", """
                    {
                      "index": 0,
                      "event_type": "content.stop"
                    }
                    """),
                BuildEvent("content.start", """
                    {
                      "index": 1,
                      "content": {
                        "type": "text"
                      },
                      "event_type": "content.start"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 1,
                      "delta": {
                        "text": "Answer",
                        "type": "text"
                      },
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.stop", """
                    {
                      "index": 1,
                      "event_type": "content.stop"
                    }
                    """),
                BuildEvent("interaction.complete", """
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
                      "event_type": "interaction.complete"
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
                BuildEvent("interaction.start", """
                    {
                      "interaction": {
                        "id": "interaction-stream-4",
                        "status": "in_progress",
                        "object": "interaction",
                        "model": "gemini-3-flash-preview"
                      },
                      "event_type": "interaction.start"
                    }
                    """),
                BuildEvent("weird.event", """
                    {
                      "foo": "bar"
                    }
                    """),
                BuildEvent("content.start", """
                    {
                      "index": 0,
                      "content": {
                        "type": "text"
                      },
                      "event_type": "content.start"
                    }
                    """),
                BuildEvent("content.delta", """
                    {
                      "index": 0,
                      "delta": {
                        "text": "OK",
                        "type": "text"
                      },
                      "event_type": "content.delta"
                    }
                    """),
                BuildEvent("content.stop", """
                    {
                      "index": 0,
                      "event_type": "content.stop"
                    }
                    """),
                BuildEvent("interaction.complete", """
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
                      "event_type": "interaction.complete"
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
    public async Task GetStreamingResponseAsync_FallsBack_WhenServerReturnsJson()
    {
        const string jsonResponse = """
            {
              "id": "interaction-json-fallback",
              "model": "gemini-3-flash-preview",
              "outputs": [
                {
                  "type": "text",
                  "text": "fallback ok"
                }
              ],
              "usage": {
                "input_tokens": 2,
                "output_tokens": 2,
                "total_tokens": 4
              }
            }
            """;

        var handler = new StreamingRequestHandler(
            CreateJsonResponse(jsonResponse),
            CreateJsonResponse(jsonResponse));

        using var httpClient = CreateHttpClient(handler);
        using var client = new GeminiInteractionsChatClient(httpClient, "gemini-3-flash-preview");

        var updates = await CollectUpdatesAsync(client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]));
        var response = updates.ToChatResponse();

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("\"stream\":true", handler.RequestBodies[0]);
        Assert.DoesNotContain("\"stream\":true", handler.RequestBodies[1]);
        Assert.Equal("fallback ok", response.Messages.Single().Text);
        Assert.Equal(4, response.Usage?.TotalTokenCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_Cancellation_MidStream_StopsCleanly()
    {
        var firstChunk = CreateSsePayload(
            BuildEvent("interaction.start", """
                {
                  "interaction": {
                    "id": "interaction-stream-5",
                    "status": "in_progress",
                    "object": "interaction",
                    "model": "gemini-3-flash-preview"
                  },
                  "event_type": "interaction.start"
                }
                """),
            BuildEvent("content.start", """
                {
                  "index": 0,
                  "content": {
                    "type": "text"
                  },
                  "event_type": "content.start"
                }
                """),
            BuildEvent("content.delta", """
                {
                  "index": 0,
                  "delta": {
                    "text": "partial",
                    "type": "text"
                  },
                  "event_type": "content.delta"
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

        var hasNext = await enumerator.MoveNextAsync();
        Assert.False(hasNext);
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
              "outputs": [
                {
                  "type": "text",
                  "text": "ok"
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