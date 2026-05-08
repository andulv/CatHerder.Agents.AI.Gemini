using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http.Headers;
using System.Threading;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CatHerder.Agents.AI.Gemini;

/// <summary>
/// IChatClient for the Gemini Interactions API over raw HTTP.
/// Supports Gemini Interactions built-in server-side tools.
/// </summary>
public sealed class GeminiInteractionsChatClient : IChatClient
{
    private const string ApiRevisionHeaderName = "Api-Revision";
    private const string ApiRevisionHeaderValue = "2026-05-20";
    private const string FunctionNamesByCallIdStateKey = "catherder.agents.ai.gemini.function_names_by_call_id";

    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly GeminiInteractionsChatClientOptions _options;
    private readonly ILogger? _logger;
    private readonly ChatClientMetadata _metadata;
    private readonly bool _disposeHttpClient;
    private int _streamingFallbackLogged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };


    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiInteractionsChatClient" /> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured for the Gemini Interactions endpoint.</param>
    /// <param name="modelId">The default Gemini model ID.</param>
    /// <param name="options">Optional client behavior settings.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public GeminiInteractionsChatClient(
        HttpClient httpClient,
        string modelId,
        GeminiInteractionsChatClientOptions? options = null,
        ILogger? logger = null)
        : this(httpClient, modelId, options, logger, disposeHttpClient: false)
    {
    }

    internal GeminiInteractionsChatClient(
        HttpClient httpClient,
        string modelId,
        GeminiInteractionsChatClientOptions? options,
        ILogger? logger,
        bool disposeHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        _httpClient = httpClient;
        _modelId = modelId;
        _options = options ?? GeminiInteractionsChatClientOptions.Empty;
        _logger = logger;
        _metadata = new ChatClientMetadata("gemini-interactions", httpClient.BaseAddress, modelId);
        _disposeHttpClient = disposeHttpClient;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, options, stream: false);
        _logger?.LogDebug("Sending Interactions request for model {ModelId}", options?.ModelId ?? _modelId);

        using var response = await SendInteractionRequestAsync(request, acceptEventStream: false, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var parsedError = TryParseGeminiError(responseBody);
            _logger?.LogError("Interactions API returned {StatusCode}: {Body}", (int)response.StatusCode, responseBody);

            throw new GeminiApiException(
                BuildErrorMessage(response.StatusCode, parsedError),
                response.StatusCode,
                parsedError?.Code,
                parsedError?.Status,
                responseBody);
        }

        var interaction = JsonNode.Parse(responseBody)!.AsObject();
        var chatResponse = MapInteractionToChatResponse(interaction, _logger);

        using var toolTelemetry = new GeminiBuiltInToolTelemetry();
        foreach (var message in chatResponse.Messages)
        {
            toolTelemetry.Observe(message.Contents);
        }

        return chatResponse;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var toolTelemetry = new GeminiBuiltInToolTelemetry();
        var sseEnumerator = StreamWithSseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        GeminiSseNegotiationException? negotiationFailure = null;

        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await sseEnumerator.MoveNextAsync();
                }
                catch (GeminiSseNegotiationException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    negotiationFailure = ex;
                    break;
                }

                if (!hasNext)
                {
                    break;
                }

                toolTelemetry.Observe(sseEnumerator.Current.Contents);
                yield return sseEnumerator.Current;
            }
        }
        finally
        {
            await sseEnumerator.DisposeAsync();
        }

        if (negotiationFailure is null)
        {
            yield break;
        }

        LogStreamingFallback(negotiationFailure.Message);
        var fallbackResponse = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in fallbackResponse.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    private async IAsyncEnumerable<ChatResponseUpdate> StreamWithSseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = BuildRequest(messages, options, stream: true);
        _logger?.LogDebug("Starting Gemini Interactions SSE request for model {ModelId}", options?.ModelId ?? _modelId);

        HttpResponseMessage? response = null;

        try
        {
            response = await SendInteractionRequestAsync(request, acceptEventStream: true, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new GeminiSseNegotiationException("Gemini Interactions SSE negotiation failed before response headers were received.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new GeminiSseNegotiationException($"Gemini Interactions SSE negotiation failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).");
            }

            if (!IsSseResponse(response))
            {
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "(missing)";
                throw new GeminiSseNegotiationException($"Gemini Interactions SSE negotiation returned unexpected Content-Type '{contentType}'.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            var reducer = new GeminiSseEventReducer(_logger);
            var currentEvent = default(string);
            var currentData = new List<string>();

            while (true)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    foreach (var update in ProcessSseFrame(reducer, currentEvent, currentData))
                    {
                        yield return update;
                    }

                    currentEvent = null;
                    currentData.Clear();
                    continue;
                }

                if (line.StartsWith(':'))
                {
                    continue;
                }

                if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                {
                    currentEvent = line[6..].Trim();
                    continue;
                }

                if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    currentData.Add(line[5..].TrimStart());
                    continue;
                }

                _logger?.LogDebug("Ignoring unrecognized Gemini SSE line: {Line}", line);
            }

            foreach (var update in ProcessSseFrame(reducer, currentEvent, currentData))
            {
                yield return update;
            }
        }
    }

    private HttpRequestMessage CreateInteractionRequestMessage(GeminiInteractionRequest request, bool acceptEventStream)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, acceptEventStream ? "interactions?alt=sse" : "interactions")
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json"),
        };

        requestMessage.Headers.TryAddWithoutValidation(ApiRevisionHeaderName, ApiRevisionHeaderValue);

        if (acceptEventStream)
        {
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        }

        return requestMessage;
    }

    private async Task<HttpResponseMessage> SendInteractionRequestAsync(
        GeminiInteractionRequest request,
        bool acceptEventStream,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        using var requestMessage = CreateInteractionRequestMessage(request, acceptEventStream);
        return await _httpClient.SendAsync(requestMessage, completionOption, cancellationToken);
    }

    private GeminiInteractionRequest BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
    {
        var normalized = NormalizeMessages(messages, options?.Instructions);
        return new GeminiInteractionRequest
        {
            Model = options?.ModelId ?? _modelId,
            SystemInstruction = string.IsNullOrWhiteSpace(normalized.SystemInstruction) ? null : normalized.SystemInstruction,
            Input = MapInput(normalized.InputTurns),
            GenerationConfig = MapChatOptionsToGenerationConfig(options),
            ResponseFormat = MapResponseFormat(options?.ResponseFormat),
            Tools = MapTools(options, _options.BuiltInTools),
            PreviousInteractionId = options?.ConversationId,
            Stream = stream ? true : null,
        };
    }

    private static NormalizedMessages NormalizeMessages(IEnumerable<ChatMessage> messages, string? optionInstructions)
    {
        var messageList = messages.ToList();
        var systemInstructionParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(optionInstructions))
        {
            systemInstructionParts.Add(optionInstructions);
        }

        systemInstructionParts.AddRange(
            messageList
                .Where(message => message.Role == ChatRole.System)
                .Select(message => message.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))!);

        var systemInstruction = string.Join("\n", systemInstructionParts);

        return new NormalizedMessages(systemInstruction, messageList.Where(message => message.Role != ChatRole.System).ToList());
    }

    private static object MapInput(IReadOnlyList<ChatMessage> turns)
    {
        if (turns.Count == 0)
        {
            return string.Empty;
        }

        if (turns.Count == 1 && turns[0].Role == ChatRole.User)
        {
            var singleUserContent = MapTurnContent(turns[0], turns);
            if (singleUserContent.Count == 1 && singleUserContent[0].Type == "text")
            {
                return singleUserContent[0].Text ?? string.Empty;
            }

            return singleUserContent;
        }

        return MapInputSteps(turns);
    }

    private static List<GeminiInteractionInputStep> MapInputSteps(IReadOnlyList<ChatMessage> turns)
    {
        var steps = new List<GeminiInteractionInputStep>();

        foreach (var turn in turns)
        {
            if (turn.Role == ChatRole.Assistant)
            {
                AddAssistantInputSteps(turn, turns, steps);
                continue;
            }

            if (turn.Role == ChatRole.Tool)
            {
                AddFunctionResultInputSteps(turn, turns, steps);
                continue;
            }

            var content = MapTurnContent(turn, turns)
                .Where(item => item.Type == "text" || item.Type == "image")
                .ToList();

            steps.Add(new GeminiInteractionInputStep
            {
                Type = "user_input",
                Content = content,
            });
        }

        return steps;
    }

    private static void AddAssistantInputSteps(ChatMessage turn, IReadOnlyList<ChatMessage> turns, List<GeminiInteractionInputStep> steps)
    {
        var modelOutputContent = new List<GeminiInteractionContent>();
        foreach (var item in MapTurnContent(turn, turns))
        {
            if (item.Type == "function_call")
            {
                steps.Add(new GeminiInteractionInputStep
                {
                    Type = "function_call",
                    Id = item.Id,
                    Name = item.Name,
                    Arguments = item.Arguments,
                });
                continue;
            }

            if (item.Type == "text" || item.Type == "image")
            {
                modelOutputContent.Add(item);
            }
        }

        if (modelOutputContent.Count > 0)
        {
            steps.Add(new GeminiInteractionInputStep
            {
                Type = "model_output",
                Content = modelOutputContent,
            });
        }
    }

    private static void AddFunctionResultInputSteps(ChatMessage turn, IReadOnlyList<ChatMessage> turns, List<GeminiInteractionInputStep> steps)
    {
        foreach (var item in MapTurnContent(turn, turns).Where(item => item.Type == "function_result"))
        {
            steps.Add(new GeminiInteractionInputStep
            {
                Type = "function_result",
                Name = item.Name,
                CallId = item.CallId,
                Result = item.Result,
            });
        }
    }

    private static List<GeminiInteractionContent> MapTurnContent(ChatMessage message, IReadOnlyList<ChatMessage> turns)
    {
        var content = new List<GeminiInteractionContent>();
        var hasTextContent = false;

        foreach (var item in message.Contents)
        {
            switch (item)
            {
                case FunctionCallContent functionCall when GeminiBuiltInToolBridge.IsInformationalBuiltInTool(functionCall):
                    break;

                case FunctionResultContent functionResult when GeminiBuiltInToolBridge.IsInformationalBuiltInTool(functionResult):
                    break;

                case TextContent textContent:
                    if (!string.IsNullOrWhiteSpace(textContent.Text))
                    {
                        hasTextContent = true;
                        content.Add(new GeminiInteractionContent
                        {
                            Type = "text",
                            Text = textContent.Text,
                        });
                    }

                    break;

                case FunctionCallContent functionCall:
                    RememberFunctionName(functionCall.CallId, functionCall.Name);
                    content.Add(new GeminiInteractionContent
                    {
                        Type = "function_call",
                        Id = functionCall.CallId,
                        Name = functionCall.Name,
                        Arguments = functionCall.Arguments ?? new Dictionary<string, object?>(),
                    });

                    break;

                case FunctionResultContent functionResult:
                    var resolvedFunctionName = ResolveFunctionName(functionResult, turns);
                    content.Add(new GeminiInteractionContent
                    {
                        Type = "function_result",
                        Name = resolvedFunctionName,
                        CallId = functionResult.CallId,
                        Result = ToFunctionResultValue(functionResult.Result),
                    });

                    break;
            }
        }

        if (!hasTextContent && !string.IsNullOrWhiteSpace(message.Text))
        {
            content.Add(new GeminiInteractionContent
            {
                Type = "text",
                Text = message.Text,
            });
        }

        if (content.Count == 0)
        {
            content.Add(new GeminiInteractionContent
            {
                Type = "text",
                Text = string.Empty,
            });
        }

        return content;
    }

    private static string ResolveFunctionName(FunctionResultContent functionResult, IReadOnlyList<ChatMessage> turns)
    {
        if (TryResolveFunctionNameFromSession(functionResult.CallId, out var sessionName))
        {
            return sessionName;
        }

        if (TryResolveFunctionNameFromTranscript(turns, functionResult.CallId, out var transcriptName))
        {
            return transcriptName;
        }

        if (functionResult.AdditionalProperties is { } additionalProperties)
        {
            foreach (var key in new[] { "name", "function_name", "functionName" })
            {
                if (additionalProperties.TryGetValue(key, out var value) && value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                {
                    return stringValue;
                }
            }
        }

        throw new InvalidOperationException(
            $"Cannot serialize function_result for call_id '{functionResult.CallId}' because the function name could not be resolved from AgentSession.StateBag, transcript function_call content, or AdditionalProperties keys 'name', 'function_name', or 'functionName'.");
    }

    private static bool TryResolveFunctionNameFromSession(string? callId, out string functionName)
    {
        functionName = string.Empty;
        if (string.IsNullOrWhiteSpace(callId))
        {
            return false;
        }

        var stateBag = AIAgent.CurrentRunContext?.Session?.StateBag;
        if (stateBag?.TryGetValue(FunctionNamesByCallIdStateKey, out Dictionary<string, string>? functionNamesByCallId) != true || functionNamesByCallId is null)
        {
            return false;
        }

        if (!functionNamesByCallId.TryGetValue(callId, out var foundName) || string.IsNullOrWhiteSpace(foundName))
        {
            return false;
        }

        functionName = foundName;
        return true;
    }

    private static bool TryResolveFunctionNameFromTranscript(IReadOnlyList<ChatMessage> turns, string? callId, out string functionName)
    {
        functionName = string.Empty;
        if (string.IsNullOrWhiteSpace(callId))
        {
            return false;
        }

        foreach (var turn in turns)
        {
            foreach (var functionCall in turn.Contents.OfType<FunctionCallContent>())
            {
                if (string.Equals(functionCall.CallId, callId, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(functionCall.Name))
                {
                    functionName = functionCall.Name;
                    return true;
                }
            }
        }

        return false;
    }

    private static void RememberFunctionName(string? callId, string? functionName)
    {
        if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(functionName))
        {
            return;
        }

        var stateBag = AIAgent.CurrentRunContext?.Session?.StateBag;
        if (stateBag is null)
        {
            return;
        }

        var functionNamesByCallId = stateBag.GetValue<Dictionary<string, string>>(FunctionNamesByCallIdStateKey)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        functionNamesByCallId[callId] = functionName;
        stateBag.SetValue(FunctionNamesByCallIdStateKey, functionNamesByCallId);
    }

    private static void RememberFunctionNames(IEnumerable<AIContent> contents)
    {
        foreach (var functionCall in contents.OfType<FunctionCallContent>())
        {
            RememberFunctionName(functionCall.CallId, functionCall.Name);
        }
    }

    private static object? ToFunctionResultValue(object? result)
    {
        if (result is null)
        {
            return null;
        }

        if (result is string text)
        {
            return text;
        }

        if (result is IEnumerable<AIContent> contentItems)
        {
            var items = new List<GeminiInteractionContent>();
            foreach (var item in contentItems)
            {
                if (item is TextContent textContent)
                {
                    items.Add(new GeminiInteractionContent
                    {
                        Type = "text",
                        Text = textContent.Text ?? string.Empty,
                    });
                }
                else if (item is DataContent dataContent && dataContent.HasTopLevelMediaType("image"))
                {
                    items.Add(new GeminiInteractionContent
                    {
                        Type = "image",
                        MimeType = dataContent.MediaType,
                        Data = Convert.ToBase64String(dataContent.Data.ToArray()),
                    });
                }
                else if (item is UriContent uriContent && uriContent.HasTopLevelMediaType("image"))
                {
                    items.Add(new GeminiInteractionContent
                    {
                        Type = "image",
                        MimeType = uriContent.MediaType,
                        Uri = uriContent.Uri.ToString(),
                    });
                }
            }

            return items;
        }

        return result;
    }

    private static GeminiInteractionGenerationConfig? MapChatOptionsToGenerationConfig(ChatOptions? options)
    {
        IReadOnlyList<string>? stopSequences = null;
        if (options?.StopSequences is { Count: > 0 })
        {
            stopSequences = options.StopSequences.ToList();
        }

        var generationConfig = new GeminiInteractionGenerationConfig
        {
            Temperature = options?.Temperature,
            MaxOutputTokens = options?.MaxOutputTokens,
            TopP = options?.TopP,
            TopK = options?.TopK,
            StopSequences = stopSequences,
        };

        return generationConfig.Temperature is null
            && generationConfig.MaxOutputTokens is null
            && generationConfig.TopP is null
            && generationConfig.TopK is null
            && generationConfig.StopSequences is null
            ? null
            : generationConfig;
    }

    private static GeminiInteractionResponseFormat? MapResponseFormat(ChatResponseFormat? responseFormat)
    {
        return responseFormat switch
        {
            null => null,
            ChatResponseFormatText => null,
            ChatResponseFormatJson json => new GeminiInteractionResponseFormat
            {
                Type = "text",
                MimeType = "application/json",
                Schema = json.Schema,
            },
            _ => null,
        };
    }

    private static IReadOnlyList<GeminiInteractionTool>? MapTools(ChatOptions? options, IReadOnlyList<GeminiBuiltInToolKind>? builtInTools)
    {
        var tools = new List<GeminiInteractionTool>();

        if (builtInTools is { Count: > 0 })
        {
            foreach (var toolKind in builtInTools.Distinct())
            {
                tools.Add(new GeminiInteractionTool
                {
                    Type = MapBuiltInToolType(toolKind),
                });
            }
        }

        if (options?.Tools is { Count: > 0 } configuredTools)
        {
            foreach (var tool in configuredTools)
            {
                if (tool is not AIFunctionDeclaration function)
                {
                    continue;
                }

                tools.Add(new GeminiInteractionTool
                {
                    Type = "function",
                    Name = function.Name,
                    Description = function.Description,
                    Parameters = ParseSchema(function.JsonSchema),
                });
            }
        }

        if (tools.Count == 0)
        {
            return null;
        }

        return tools;
    }

    private static JsonElement ParseSchema(JsonElement schema)
    {
        using var document = JsonDocument.Parse(schema.GetRawText());
        return document.RootElement.Clone();
    }

    private static string MapBuiltInToolType(GeminiBuiltInToolKind toolKind)
    {
        return toolKind switch
        {
            GeminiBuiltInToolKind.CodeExecution => "code_execution",
            GeminiBuiltInToolKind.UrlContext => "url_context",
            GeminiBuiltInToolKind.GoogleSearch => "google_search",
            GeminiBuiltInToolKind.GoogleMaps => "google_maps",
            _ => throw new ArgumentOutOfRangeException(nameof(toolKind), toolKind, "Unsupported Gemini built-in tool."),
        };
    }

    private static ChatResponse MapInteractionToChatResponse(JsonObject interaction, ILogger? logger)
    {
        var interactionId = interaction["id"]?.GetValue<string>();
        var modelId = interaction["model"]?.GetValue<string>();
        if (interaction["steps"] is not JsonArray steps)
        {
            throw new InvalidOperationException(
                $"Gemini Interactions response did not contain a 'steps' array. This client only supports the {ApiRevisionHeaderValue} steps schema.");
        }

        var textParts = new List<string>();
        var additionalContents = new List<AIContent>();

        foreach (var step in steps)
        {
            if (step is not JsonObject stepObject)
            {
                continue;
            }

            MapStep(stepObject, textParts, additionalContents, logger);
        }

        var responseText = string.Join("\n", textParts);
        var assistantMessage = new ChatMessage(ChatRole.Assistant, responseText);
        foreach (var content in additionalContents)
            assistantMessage.Contents.Add(content);

        var chatResponse = new ChatResponse(assistantMessage)
        {
            ResponseId = interactionId,
            ModelId = modelId,
        };

        // Map usage
        chatResponse.Usage = MapUsageDetails(interaction["usage"]);

        if (!string.IsNullOrEmpty(interactionId))
        {
            chatResponse.ConversationId = interactionId;
        }

        logger?.LogDebug(
            "Interaction {Id} completed. Tokens: in={Input} out={Output} total={Total}",
            interactionId,
            chatResponse.Usage?.InputTokenCount,
            chatResponse.Usage?.OutputTokenCount,
            chatResponse.Usage?.TotalTokenCount);

        return chatResponse;
    }

    private static void MapStep(JsonObject step, List<string> textParts, List<AIContent> additionalContents, ILogger? logger)
    {
        var type = step["type"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(type))
        {
            return;
        }

        if (type == "model_output")
        {
            MapModelOutputStep(step, textParts, additionalContents, logger);
            return;
        }

        if (type == "function_call")
        {
            AddFunctionCallContent(step, additionalContents);
            return;
        }

        if (GeminiBuiltInToolBridge.IsBuiltInToolCallType(type))
        {
            additionalContents.Add(GeminiBuiltInToolBridge.CreateToolCall(step));
            return;
        }

        if (GeminiBuiltInToolBridge.IsBuiltInToolResultType(type))
        {
            additionalContents.Add(GeminiBuiltInToolBridge.CreateToolResult(step));
            return;
        }

        if (type == "thought")
        {
            LogThoughtSummary(step, logger);
        }
    }

    private static void MapModelOutputStep(JsonObject step, List<string> textParts, List<AIContent> additionalContents, ILogger? logger)
    {
        if (step["content"] is not JsonArray contentBlocks)
        {
            return;
        }

        foreach (var contentBlock in contentBlocks)
        {
            if (contentBlock is not JsonObject contentObject)
            {
                continue;
            }

            var type = contentObject["type"]?.GetValue<string>();
            if (type == "text")
            {
                var text = contentObject["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    textParts.Add(text);
                }

                continue;
            }

            if (type == "function_call")
            {
                AddFunctionCallContent(contentObject, additionalContents);
                continue;
            }

            if (GeminiBuiltInToolBridge.IsBuiltInToolCallType(type))
            {
                additionalContents.Add(GeminiBuiltInToolBridge.CreateToolCall(contentObject));
                continue;
            }

            if (GeminiBuiltInToolBridge.IsBuiltInToolResultType(type))
            {
                additionalContents.Add(GeminiBuiltInToolBridge.CreateToolResult(contentObject));
                continue;
            }

            if (type == "thought")
            {
                LogThoughtSummary(contentObject, logger);
            }
        }
    }

    private static void AddFunctionCallContent(JsonObject payload, List<AIContent> additionalContents)
    {
        var callId = payload["id"]?.GetValue<string>();
        var name = payload["name"]?.GetValue<string>();
        var argumentsNode = payload["arguments"];
        if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        RememberFunctionName(callId, name);
        var argumentsJson = argumentsNode?.ToJsonString()
            ?? throw new InvalidOperationException($"Gemini function call '{name}' has null arguments.");
        additionalContents.Add(FunctionCallContent.CreateFromParsedArguments(
            argumentsJson,
            callId,
            name,
            static json => JsonSerializer.Deserialize<Dictionary<string, object?>>(json)));
    }

    private static void LogThoughtSummary(JsonObject thought, ILogger? logger)
    {
        if (thought["summary"] is JsonArray summaryBlocks)
        {
            foreach (var summaryBlock in summaryBlocks.OfType<JsonObject>())
            {
                var summaryText = summaryBlock["text"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(summaryText))
                {
                    logger?.LogDebug("Model thought: {Summary}", summaryText);
                }
            }

            return;
        }

        var summary = thought["summary"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            logger?.LogDebug("Model thought: {Summary}", summary);
        }
    }

    private static UsageDetails? MapUsageDetails(JsonNode? usageNode)
        => GeminiUsageMapper.Map(usageNode);

    private IEnumerable<ChatResponseUpdate> ProcessSseFrame(GeminiSseEventReducer reducer, string? eventType, IReadOnlyList<string> dataLines)
    {
        if (string.IsNullOrWhiteSpace(eventType) && dataLines.Count == 0)
        {
            return [];
        }

        var payloadText = string.Join("\n", dataLines);
        if (string.Equals(eventType, "done", StringComparison.OrdinalIgnoreCase) || string.Equals(payloadText, "[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        JsonObject? payload = null;
        if (!string.IsNullOrWhiteSpace(payloadText))
        {
            try
            {
                payload = JsonNode.Parse(payloadText) as JsonObject;
                if (payload is null)
                {
                    _logger?.LogDebug("Ignoring Gemini SSE frame {EventType} because the payload was not a JSON object.", eventType);
                    return [];
                }
            }
            catch (JsonException ex)
            {
                _logger?.LogDebug(ex, "Ignoring malformed Gemini SSE frame for event {EventType}.", eventType);
                return [];
            }
        }

        var updates = reducer.Reduce(eventType ?? string.Empty, payload);
        foreach (var update in updates)
        {
            RememberFunctionNames(update.Contents);
        }

        return updates;
    }

    private static bool IsSseResponse(HttpResponseMessage response)
    {
        return string.Equals(
            response.Content.Headers.ContentType?.MediaType,
            "text/event-stream",
            StringComparison.OrdinalIgnoreCase);
    }

    private void LogStreamingFallback(string reason)
    {
        if (Interlocked.Exchange(ref _streamingFallbackLogged, 1) == 0)
        {
            _logger?.LogWarning("Falling back to non-streaming Gemini response path because SSE negotiation failed: {Reason}", reason);
        }
    }

    private static GeminiApiError? TryParseGeminiError(string responseBody)
    {
        try
        {
            var root = JsonNode.Parse(responseBody)?.AsObject();
            var error = root?["error"]?.AsObject();
            if (error is null)
                return null;

            var code = TryGetString(error["code"]);
            var message = TryGetString(error["message"]);
            var status = TryGetString(error["status"]);
            return new GeminiApiError(code, message, status);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildErrorMessage(System.Net.HttpStatusCode statusCode, GeminiApiError? error)
    {
        if (error is null)
            return $"Gemini Interactions API request failed with HTTP {(int)statusCode} ({statusCode}).";

        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(error.Status))
            details.Add($"status={error.Status}");
        if (!string.IsNullOrWhiteSpace(error.Code))
            details.Add($"code={error.Code}");

        var suffix = details.Count > 0
            ? $" [{string.Join(", ", details)}]"
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(error.Message))
            return $"Gemini API error: {error.Message}{suffix}";

        return $"Gemini Interactions API request failed with HTTP {(int)statusCode} ({statusCode}){suffix}.";
    }

    private static string? TryGetString(JsonNode? node)
    {
        if (node is null)
            return null;

        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var asString) => asString,
            JsonValue value when value.TryGetValue<int>(out var asInt) => asInt.ToString(),
            JsonValue value when value.TryGetValue<long>(out var asLong) => asLong.ToString(),
            _ => node.ToJsonString(),
        };
    }

    private sealed record GeminiApiError(string? Code, string? Message, string? Status);

    private sealed record NormalizedMessages(string SystemInstruction, IReadOnlyList<ChatMessage> InputTurns);

    private sealed class GeminiSseNegotiationException : Exception
    {
        public GeminiSseNegotiationException(string message)
            : base(message)
        {
        }

        public GeminiSseNegotiationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is null && serviceType == typeof(ChatClientMetadata))
            return _metadata;

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

}
