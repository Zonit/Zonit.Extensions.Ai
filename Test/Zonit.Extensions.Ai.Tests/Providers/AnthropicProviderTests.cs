using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Linq;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.Tests.Schema;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Tests for AnthropicProvider - request building, file handling, PDF support.
/// </summary>
public class AnthropicProviderTests
{
    private readonly Mock<ILogger<AnthropicProvider>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly AnthropicOptions _options;

    public AnthropicProviderTests()
    {
        _loggerMock = new Mock<ILogger<AnthropicProvider>>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _options = new AnthropicOptions { ApiKey = "test-api-key" };
    }

    [Fact]
    public void SupportsModel_WithAnthropicBase_ShouldReturnTrue()
    {
        // Arrange
        var provider = CreateProvider();
        var model = new Sonnet45();

        // Act & Assert
        provider.SupportsModel(model).Should().BeTrue();
    }

    [Fact]
    public void Name_ShouldReturnAnthropic()
    {
        // Arrange
        var provider = CreateProvider();

        // Act & Assert
        provider.Name.Should().Be("Anthropic");
    }

    [Fact]
    public async Task GenerateAsync_ShouldSendRequestWithCorrectFormat()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"msg_123","content":[{"type":"text","text":"Hello"}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Sonnet45();
        var prompt = new TestPrompt { Text = "Say hello" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        var json = JsonDocument.Parse(capturedRequest!);
        json.RootElement.GetProperty("model").GetString().Should().Contain("claude");
        json.RootElement.GetProperty("messages").EnumerateArray().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_WithImage_ShouldIncludeImageInRequest()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"msg_123","content":[{"type":"text","text":"Image analyzed"}],"usage":{"input_tokens":100,"output_tokens":10}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Sonnet45();
        Asset.MimeType pngMime = Asset.MimeType.ImagePng;
        var prompt = new TestPrompt
        {
            Text = "Describe this image",
            Files = [new Asset([0x89, 0x50, 0x4E, 0x47], "test.png", pngMime)]
        };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("\"type\":\"image\"");
        capturedRequest.Should().Contain("image/png");
        capturedRequest.Should().Contain("base64");
    }

    [Fact]
    public async Task GenerateAsync_WithPdf_ShouldIncludeDocumentInRequest()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"msg_123","content":[{"type":"text","text":"PDF analyzed"}],"usage":{"input_tokens":100,"output_tokens":10}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Sonnet45();
        Asset.MimeType pdfMime = Asset.MimeType.ApplicationPdf;
        var prompt = new TestPrompt
        {
            Text = "Analyze this PDF",
            Files = [new Asset([0x25, 0x50, 0x44, 0x46], "test.pdf", pdfMime)]
        };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("\"type\":\"document\"");
        capturedRequest.Should().Contain("application/pdf");
    }

    [Fact]
    public async Task GenerateAsync_WithThinkingBudget_ShouldIncludeThinkingInRequest()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"msg_123","content":[{"type":"text","text":"Thought about it"}],"usage":{"input_tokens":10,"output_tokens":50}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Sonnet45 { ThinkingBudget = 10000 };
        var prompt = new TestPrompt { Text = "Think about this" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("thinking");
        capturedRequest.Should().Contain("budget_tokens");
        capturedRequest.Should().Contain("10000");
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnCorrectResult()
    {
        // Arrange
        SetupMockResponse("""{"id":"msg_123","content":[{"type":"text","text":"Hello World"}],"usage":{"input_tokens":10,"output_tokens":5}}""");

        var provider = CreateProvider();
        var model = new Sonnet45();
        var prompt = new TestPrompt { Text = "Say hello" };

        // Act
        var result = await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        result.Value.Should().Be("Hello World");
        result.MetaData.Should().NotBeNull();
        result.MetaData.Usage!.InputTokens.Should().Be(10);
        result.MetaData.Usage.OutputTokens.Should().Be(5);
    }

    [Fact]
    public async Task GenerateAsync_WithFunctionTool_ShouldIncludeToolInRequest()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"msg_123","content":[{"type":"text","text":"Called function"}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Sonnet45
        {
            Tools = [Zonit.Extensions.Ai.Anthropic.Tools.FunctionTool.Create(
                "get_weather",
                "Gets weather",
                new { type = "object", properties = new { location = new { type = "string" } } }
            )]
        };
        var prompt = new TestPrompt { Text = "What's the weather?" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("tools");
        capturedRequest.Should().Contain("get_weather");
    }

    [Fact]
    public async Task GenerateAsync_WithCacheTokens_ReportsInclusiveInputAndPricesEachBucket()
    {
        // Anthropic reports input_tokens EXCLUSIVE of the cache buckets (the three
        // counts are disjoint). The provider must normalize InputTokens to the
        // inclusive grand total so the uncached remainder is still billed — the
        // pre-fix bug collapsed it to zero whenever caching was active.
        SetupMockResponse("""{"id":"msg_c","content":[{"type":"text","text":"ok"}],"usage":{"input_tokens":1000,"output_tokens":500,"cache_read_input_tokens":5000,"cache_creation_input_tokens":2000}}""");

        var provider = CreateProvider();
        var model = new Opus48();
        var prompt = new TestPrompt { Text = "hi" };

        // Act
        var result = await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        var usage = result.MetaData.Usage!;
        usage.InputTokens.Should().Be(8000, "1000 uncached + 5000 cache-read + 2000 cache-write");
        usage.CachedTokens.Should().Be(5000);
        usage.CacheWriteTokens.Should().Be(2000);
        usage.OutputTokens.Should().Be(500);
        // regular 1000×$5 + cached 5000×$0.50 + write 2000×$6.25, per 1M = 0.020
        usage.InputCost.Value.Should().BeApproximately(0.020m, 1e-9m);
        usage.OutputCost.Value.Should().BeApproximately(0.0125m, 1e-9m);
    }

    [Fact]
    public async Task GenerateAsync_StructuredOutput_DoesNotPrefillAssistant_AndEndsWithUser()
    {
        // Regression: Claude Sonnet 4.6+ rejects assistant-message prefill
        // ("This model does not support assistant message prefill. The
        // conversation must end with a user message."). Structured output must
        // NOT append an assistant "{" turn — the request must end on the user
        // message. See AnthropicProvider.BuildRequest.
        string? capturedRequest = null;
        SetupMockResponse(
            """{"id":"msg_1","content":[{"type":"text","text":"{\"name\":\"Ada\",\"age\":36,\"active\":true,\"score\":9.5,\"note\":\"hi\",\"tag\":\"PIONEER\"}"}],"usage":{"input_tokens":1,"output_tokens":1}}""",
            r => capturedRequest = r);

        var provider = CreateProvider();
        await provider.GenerateAsync(new Sonnet46(), new FlatPrompt(), CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedRequest!);
        var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        messages.Should().NotBeEmpty();
        messages[^1].GetProperty("role").GetString().Should().Be(
            "user",
            "Sonnet 4.6+ requires the conversation to end with a user message (no assistant prefill)");
        messages.Should().NotContain(
            m => m.GetProperty("role").GetString() == "assistant",
            "structured output must not use assistant-message prefill");
    }

    private AnthropicProvider CreateProvider()
    {
        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.anthropic.com")
        };

        return new AnthropicProvider(
            httpClient,
            Options.Create(_options),
            _loggerMock.Object);
    }

    private void SetupMockResponse(string responseJson, Action<string>? captureRequest = null)
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                if (captureRequest != null && request.Content != null)
                {
                    var content = await request.Content.ReadAsStringAsync();
                    captureRequest(content);
                }
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
    }

    private class TestPrompt : IPrompt<string>
    {
        public string? System { get; set; }
        public required string Text { get; set; }
        public IReadOnlyList<Asset>? Files { get; set; }
    }
}
