using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace CatHerder.Agents.AI.Gemini.IntegrationTests;

public sealed class GeminiInteractionsLiveTests
{
    [LiveGeminiFact]
    public async Task RawInteractionsRequest_WithMay2026ApiRevision_ReturnsStepsSchema()
    {
        var config = LiveGeminiConfiguration.Current;
        using var httpClient = new HttpClient { BaseAddress = config.Endpoint };
        using var request = new HttpRequestMessage(HttpMethod.Post, "interactions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    model = config.ModelId,
                    input = "Reply with exactly one short sentence about the color blue.",
                }),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("x-goog-api-key", config.ApiKey);
        request.Headers.Add("Api-Revision", "2026-05-20");

        using var response = await httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        var root = JsonNode.Parse(body)!.AsObject();
        var steps = Assert.IsType<JsonArray>(root["steps"]);

        Assert.Null(root["outputs"]);
        Assert.Contains(
            steps.OfType<JsonObject>(),
            step => step["type"]?.GetValue<string>() == "model_output"
                && step["content"] is JsonArray content
                && content.OfType<JsonObject>().Any(block => block["type"]?.GetValue<string>() == "text"));
    }

    [LiveGeminiFact]
    public async Task GetResponseAsync_ReturnsTextFromRealApi()
    {
        using var client = LiveGeminiConfiguration.Current.CreateChatClient();

        var response = await client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "Reply with exactly one short sentence about the color blue.")
        ]);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.NotNull(response.Messages.SingleOrDefault());
    }

    [LiveGeminiFact]
    public async Task ChatClientAgent_RunAsync_ReturnsTextFromRealApi()
    {
        using var client = LiveGeminiConfiguration.Current.CreateChatClient();
        var agent = client.AsAIAgent(
            instructions: "You are concise. Answer with one short sentence.",
            name: "GeminiIntegrationAgent");
        var session = await agent.CreateSessionAsync();

        var response = await agent.RunAsync("Say hello in English.", session);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    [LiveGeminiFact]
    public async Task GetResponseAsync_InvalidKey_ThrowsGeminiApiException()
    {
        var config = LiveGeminiConfiguration.Current;
        using var client = GeminiInteractionsChatClientExtensions.CreateGeminiInteractionsChatClient(
            "invalid-test-key",
            config.ModelId,
            endpoint: config.Endpoint);

        var ex = await Assert.ThrowsAsync<GeminiApiException>(async () =>
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Hello")]));

        Assert.NotNull(ex.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(ex.ResponseBody));
    }

    [LiveGeminiFact]
    public async Task GetResponseAsync_AudioInput_ReturnsAnswerAboutAudio()
    {
        using var client = LiveGeminiConfiguration.Current.CreateChatClient();
        var clip = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "audio", "clip.wav"));

        var response = await client.GetResponseAsync([
            new ChatMessage(ChatRole.User, [
                new TextContent("Is this audio clip silent or does it contain sound? Answer with exactly one word: silent or sound."),
                new DataContent(clip, "audio/wav"),
            ]),
        ]);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.Contains("sound", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [LiveGeminiFact]
    public async Task GetStreamingResponseAsync_ReturnsTextFromRealApi()
    {
        using var client = LiveGeminiConfiguration.Current.CreateChatClient();
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync([
            new ChatMessage(ChatRole.User, "Stream one short sentence about fjords.")
        ]))
        {
            updates.Add(update);
        }

        Assert.Contains(updates, update => !string.IsNullOrWhiteSpace(update.Text));
    }

    [LiveGeminiFact]
    public async Task ChatClientAgent_LocalFunctionTool_RoundTripsThroughRealApi()
    {
        using var client = LiveGeminiConfiguration.Current.CreateChatClient();
        var weatherTool = AIFunctionFactory.Create(
            (string location) => $"The weather in {location} is crisp and clear.",
            name: "get_weather",
            description: "Gets the current weather for a location.");
        var agent = client.AsAIAgent(
            instructions: "Use available tools when asked about weather, then answer briefly.",
            name: "GeminiToolAgent",
            tools: [weatherTool]);
        var session = await agent.CreateSessionAsync();

        var response = await agent.RunAsync("Use the get_weather tool for Bergen and summarize the result.", session);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    [LiveGeminiFact]
    public async Task ChatClientAgent_FunctionReturningPdf_ModelReadsThePdf()
    {
        // function_result cannot carry a PDF, so the adapter sends it as user input after the
        // function result. The follow-up is chained (previous_interaction_id), so this also
        // proves the API accepts user input right after function results in that request.
        using var client = LiveGeminiConfiguration.Current.CreateChatClient();
        var pdfTool = AIFunctionFactory.Create(
            () => new DataContent(MinimalPdf("The code word is PELICAN-58."), "application/pdf"),
            name: "get_document",
            description: "Returns the requested document as a PDF.");
        var agent = client.AsAIAgent(
            instructions: "Use the get_document tool when asked about the document.",
            name: "GeminiPdfToolAgent",
            tools: [pdfTool]);
        var session = await agent.CreateSessionAsync();

        var response = await agent.RunAsync("Call get_document and tell me the code word in the document.", session);

        Assert.Contains("PELICAN-58", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] MinimalPdf(string text)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {text.Length + 30} >>\nstream\nBT /F1 24 Tf 72 700 Td ({text}) Tj ET\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    [LiveGeminiFact]
    public async Task RawFollowUp_FunctionResultWithoutName_IsRejectedWith400()
    {
        // plan065: proves the external assumption behind the call-id→name map. The adapter
        // itself refuses to send name-less results, so this test speaks raw Interactions
        // JSON: a server-side tool loop follow-up whose function_result omits "name" must
        // be rejected with 400, although the published reference marks "name" optional.
        var config = LiveGeminiConfiguration.Current;
        using var httpClient = new HttpClient { BaseAddress = config.Endpoint };

        static HttpRequestMessage Post(LiveGeminiConfiguration config, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "interactions")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("x-goog-api-key", config.ApiKey);
            request.Headers.Add("Api-Revision", "2026-05-20");
            return request;
        }

        // Request 1: trigger a function call.
        using var first = Post(config, new
        {
            model = config.ModelId,
            input = "Use the get_weather tool for Bergen and report the result.",
            tools = new[]
            {
                new
                {
                    type = "function",
                    name = "get_weather",
                    description = "Gets the current weather for a location.",
                    parameters = new
                    {
                        type = "object",
                        properties = new { location = new { type = "string" } },
                        required = new[] { "location" },
                    },
                },
            },
        });

        using var firstResponse = await httpClient.SendAsync(first);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        firstResponse.EnsureSuccessStatusCode();

        var firstJson = JsonNode.Parse(firstBody)!.AsObject();
        var interactionId = firstJson["id"]!.GetValue<string>();
        var functionCall = firstJson["steps"]!.AsArray()
            .Select(step => step!.AsObject())
            .First(step => step["type"]?.GetValue<string>() == "function_call");
        var callId = functionCall["id"]!.GetValue<string>();

        // Request 2: send the result without "name" — the API must reject it.
        using var second = Post(config, new
        {
            model = config.ModelId,
            previous_interaction_id = interactionId,
            input = new[]
            {
                new
                {
                    type = "function_result",
                    call_id = callId,
                    result = "Crisp and clear in Bergen.",
                },
            },
        });

        using var secondResponse = await httpClient.SendAsync(second);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        Assert.Contains("function_response.name", secondBody);
    }

    [LiveGeminiBuiltInToolFact]
    public async Task BuiltInGoogleSearch_ReturnsLiveResponse()
    {
        var config = LiveGeminiConfiguration.Current;
        using var client = config.CreateChatClient(GeminiInteractionsChatClientOptions.Empty);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Use Google Search and answer with the current homepage title of example.com.")],
            new ChatOptions { Tools = [new HostedWebSearchTool()] });

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }
}

public sealed class LiveGeminiFactAttribute : FactAttribute
{
    public LiveGeminiFactAttribute()
    {
        if (!LiveGeminiConfiguration.Current.HasRequiredSettings)
        {
            Skip = "Set GOOGLE_API_KEY and GEMINI_INTERACTIONS_MODEL to run Gemini live integration tests.";
        }
    }
}

public sealed class LiveGeminiBuiltInToolFactAttribute : FactAttribute
{
    public LiveGeminiBuiltInToolFactAttribute()
    {
        if (!LiveGeminiConfiguration.Current.HasRequiredSettings)
        {
            Skip = "Set GOOGLE_API_KEY and GEMINI_INTERACTIONS_MODEL to run Gemini live integration tests.";
            return;
        }

        if (!LiveGeminiConfiguration.Current.EnableBuiltInToolTests)
        {
            Skip = "Set GEMINI_INTERACTIONS_ENABLE_BUILTIN_TOOLS=true to run Gemini built-in tool live tests.";
        }
    }
}

internal sealed class LiveGeminiConfiguration
{
    private const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/";

    private LiveGeminiConfiguration(IConfiguration configuration)
    {
        ApiKey = configuration["GOOGLE_API_KEY"] ?? string.Empty;
        ModelId = configuration["GEMINI_INTERACTIONS_MODEL"] ?? string.Empty;
        Endpoint = new Uri(configuration["GEMINI_INTERACTIONS_BASE_URL"] ?? DefaultEndpoint);
        EnableBuiltInToolTests = string.Equals(
            configuration["GEMINI_INTERACTIONS_ENABLE_BUILTIN_TOOLS"],
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    public static LiveGeminiConfiguration Current { get; } = new(
        new ConfigurationBuilder()
            .AddUserSecrets<LiveGeminiConfiguration>(optional: true)
            .AddEnvironmentVariables()
            .Build());

    public string ApiKey { get; }

    public string ModelId { get; }

    public Uri Endpoint { get; }

    public bool EnableBuiltInToolTests { get; }

    public bool HasRequiredSettings => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ModelId);

    public IChatClient CreateChatClient(GeminiInteractionsChatClientOptions? options = null)
    {
        if (!HasRequiredSettings)
        {
            throw new InvalidOperationException("Gemini live integration tests require GOOGLE_API_KEY and GEMINI_INTERACTIONS_MODEL.");
        }

        return GeminiInteractionsChatClientExtensions.CreateGeminiInteractionsChatClient(
            ApiKey,
            ModelId,
            options,
            Endpoint);
    }
}
