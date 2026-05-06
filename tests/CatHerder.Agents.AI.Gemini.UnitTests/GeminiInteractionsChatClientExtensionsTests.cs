using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CatHerder.Agents.AI.Gemini.UnitTests;

public sealed class GeminiInteractionsChatClientExtensionsTests
{
    [Fact]
    public void AsGeminiInteractionsChatClient_ReturnsGeminiClientWithMetadata()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://example.test/v1beta/"),
        };

        using var client = httpClient.AsGeminiInteractionsChatClient("gemini-test-model");

        var metadata = Assert.IsType<ChatClientMetadata>(client.GetService(typeof(ChatClientMetadata)));
        Assert.Equal("gemini-interactions", metadata.ProviderName);
        Assert.Equal("gemini-test-model", metadata.DefaultModelId);
        Assert.Equal(new Uri("https://example.test/v1beta/"), metadata.ProviderUri);
    }

    [Fact]
    public void CreateGeminiInteractionsChatClient_ConfiguresMetadataAndApiKeyHeader()
    {
        using var client = GeminiInteractionsChatClientExtensions.CreateGeminiInteractionsChatClient(
            "test-key",
            "gemini-test-model",
            endpoint: new Uri("https://example.test/v1beta/"));

        var metadata = Assert.IsType<ChatClientMetadata>(client.GetService(typeof(ChatClientMetadata)));
        Assert.Equal("gemini-test-model", metadata.DefaultModelId);
        Assert.Equal(new Uri("https://example.test/v1beta/"), metadata.ProviderUri);
    }

    [Fact]
    public void AsAIAgent_CreatesChatClientAgentWithOptions()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://example.test/v1beta/"),
        };
        using var client = httpClient.AsGeminiInteractionsChatClient("gemini-test-model");

        var agent = client.AsAIAgent(
            instructions: "Be concise.",
            name: "GeminiAgent",
            description: "Test agent");

        Assert.IsType<ChatClientAgent>(agent);
        Assert.NotNull(agent.ChatClient);
    }

    [Fact]
    public void Constructor_RejectsMissingModelId()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://example.test/v1beta/"),
        };

        Assert.Throws<ArgumentException>(() => new GeminiInteractionsChatClient(httpClient, ""));
    }
}
