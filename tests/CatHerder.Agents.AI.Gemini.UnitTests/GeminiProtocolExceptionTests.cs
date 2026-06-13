using CatHerder.Agents.AI.Gemini;

namespace CatHerder.Agents.AI.Gemini.UnitTests;

public sealed class GeminiProtocolExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        var exception = new GeminiProtocolException("Malformed Gemini response.");

        Assert.Equal("Malformed Gemini response.", exception.Message);
        Assert.IsAssignableFrom<InvalidOperationException>(exception);
    }

    [Fact]
    public void Constructor_SetsSafeMetadata()
    {
        var exception = new GeminiProtocolException(
            "Malformed Gemini response.",
            operationName: "GetResponseAsync",
            sseEventType: "step.delta",
            jsonPath: "$.delta.type",
            responseId: "interaction-123",
            modelId: "gemini-test-model");

        Assert.Equal("GetResponseAsync", exception.OperationName);
        Assert.Equal("step.delta", exception.SseEventType);
        Assert.Equal("$.delta.type", exception.JsonPath);
        Assert.Equal("interaction-123", exception.ResponseId);
        Assert.Equal("gemini-test-model", exception.ModelId);
    }

    [Fact]
    public void Constructor_PreservesInnerException()
    {
        var inner = new FormatException("Invalid number.");

        var exception = new GeminiProtocolException(
            "Malformed Gemini response.",
            jsonPath: "$.usage.total_tokens",
            innerException: inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void PublicContract_DoesNotExposeRawPayloadProperty()
    {
        var properties = typeof(GeminiProtocolException).GetProperties();

        Assert.DoesNotContain(properties, property => property.Name.Contains("Payload", StringComparison.Ordinal));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Body", StringComparison.Ordinal));
    }
}
