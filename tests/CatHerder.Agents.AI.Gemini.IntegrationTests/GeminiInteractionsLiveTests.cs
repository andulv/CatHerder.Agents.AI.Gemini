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

    [LiveGeminiBuiltInToolFact]
    public async Task BuiltInGoogleSearch_ReturnsLiveResponse()
    {
        var config = LiveGeminiConfiguration.Current;
        using var client = config.CreateChatClient(new GeminiInteractionsChatClientOptions
        {
            BuiltInTools = [GeminiBuiltInToolKind.GoogleSearch],
        });

        var response = await client.GetResponseAsync([
            new ChatMessage(ChatRole.User, "Use Google Search and answer with the current homepage title of example.com.")
        ]);

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
