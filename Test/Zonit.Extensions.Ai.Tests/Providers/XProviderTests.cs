using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Tests for XProvider (Grok) - request building, WebSearch, file handling.
/// </summary>
public class XProviderTests
{
    private readonly Mock<ILogger<XProvider>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly XOptions _options;

    public XProviderTests()
    {
        _loggerMock = new Mock<ILogger<XProvider>>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _options = new XOptions { ApiKey = "test-api-key" };
    }

    [Fact]
    public void SupportsModel_WithXBase_ShouldReturnTrue()
    {
        // Arrange
        var provider = CreateProvider();
        var model = new Grok43();

        // Act & Assert
        provider.SupportsModel(model).Should().BeTrue();
    }

    [Fact]
    public void Name_ShouldReturnX()
    {
        // Arrange
        var provider = CreateProvider();

        // Act & Assert
        provider.Name.Should().Be("X");
    }

    [Fact]
    public async Task GenerateAsync_ShouldSendRequestToCorrectEndpoint()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Hello"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok43();
        var prompt = new TestPrompt { Text = "Say hello" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        var json = JsonDocument.Parse(capturedRequest!);
        json.RootElement.GetProperty("model").GetString().Should().Contain("grok");
        json.RootElement.GetProperty("input").EnumerateArray().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_WithWebSearch_ShouldIncludeAgentTools()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Found news"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok43
        {
            WebSearch = new Search
            {
                Mode = ModeType.Always,
                MaxResults = 10
            }
        };
        var prompt = new TestPrompt { Text = "Search for news" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("tools");
        capturedRequest.Should().Contain("\"type\":\"web_search\"");
        capturedRequest.Should().Contain("\"type\":\"x_search\"");
    }

    [Fact]
    public async Task GenerateAsync_WithWebSearchSources_ShouldIncludeSourceConfig()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Found"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok43
        {
            WebSearch = new Search
            {
                Mode = ModeType.Always,
                Sources = [
                    new WebSearchSource { Country = "US", SafeSearch = true },
                    new XSearchSource { IncludedXHandles = ["elonmusk"] }
                ]
            }
        };
        var prompt = new TestPrompt { Text = "Search" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("tools");
        capturedRequest.Should().Contain("\"type\":\"web_search\"");
        capturedRequest.Should().Contain("\"type\":\"x_search\"");
        capturedRequest.Should().Contain("allowed_x_handles");
        capturedRequest.Should().Contain("elonmusk");
    }

    [Fact]
    public async Task GenerateAsync_WithDateRange_ShouldIncludeDateFilters()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Found"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok43
        {
            WebSearch = new Search
            {
                Mode = ModeType.Always,
                FromDate = new DateTime(2025, 1, 1),
                ToDate = new DateTime(2025, 1, 15)
            }
        };
        var prompt = new TestPrompt { Text = "Search" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("from_date");
        capturedRequest.Should().Contain("2025-01-01");
        capturedRequest.Should().Contain("to_date");
        capturedRequest.Should().Contain("2025-01-15");
    }

    [Fact]
    public async Task GenerateAsync_WithImage_ShouldIncludeImageInRequest()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Image analyzed"}]}],"usage":{"input_tokens":100,"output_tokens":10}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok43();
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
        capturedRequest.Should().Contain("image_url");
        capturedRequest.Should().Contain("data:image/png;base64");
    }

    [Fact]
    public async Task GenerateAsync_WithStructuredOutput_ShouldIncludeJsonSchema()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"{\"message\":\"Hello\",\"count\":5}"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok43();
        var prompt = new StructuredPrompt { Text = "Generate data" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert — the Responses API uses `text.format`, NOT the Chat Completions
        // `response_format` field (which xAI rejects with HTTP 400 on /v1/responses).
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().NotContain("response_format");
        capturedRequest.Should().Contain("\"text\"");
        capturedRequest.Should().Contain("\"format\"");
        capturedRequest.Should().Contain("json_schema");
        capturedRequest.Should().Contain("\"strict\":true");
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnCorrectResult()
    {
        // Arrange
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Hello World"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""");

        var provider = CreateProvider();
        var model = new Grok43();
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
    public async Task GenerateAsync_WithWebSearchNever_ShouldNotIncludeAgentTools()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"No search"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok43
        {
            WebSearch = new Search { Mode = ModeType.Never }
        };
        var prompt = new TestPrompt { Text = "Test" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().NotContain("tools");
    }

    [Fact]
    public async Task GenerateAsync_WithGrok46ReasonExtra_ShouldSendXHighEffort()
    {
        // Arrange
        // grok-4.6 is the first Grok model to accept a fourth effort level, and
        // xAI spells it "xhigh". A plain ToString().ToLowerInvariant() on
        // ReasoningEffort.Extra would send "extra" and take an HTTP 400.
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Hi"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok46 { Reason = ReasoningEffort.Extra };
        var prompt = new TestPrompt { Text = "Test" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("\"effort\":\"xhigh\"");
        capturedRequest.Should().NotContain("\"effort\":\"extra\"");
    }

    [Fact]
    public async Task GenerateAsync_WithGrok46ReasonHigh_ShouldSendHighEffort()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Hi"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok46 { Reason = ReasoningEffort.High };
        var prompt = new TestPrompt { Text = "Test" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().Contain("\"effort\":\"high\"");
    }

    [Fact]
    public async Task GenerateAsync_WithGrok46NoReason_ShouldOmitReasoning()
    {
        // Arrange
        // Leaving Reason null must send no `reasoning` block at all so xAI
        // applies its own default ("high") — not an invented one.
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Hi"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var prompt = new TestPrompt { Text = "Test" };

        // Act
        await provider.GenerateAsync(new Grok46(), prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().NotContain("\"effort\"");
    }

    [Fact]
    public async Task GenerateAsync_WithPriorityScheduling_SendsServiceTierPriority()
    {
        // xAI's answer to OpenAI fast mode: same wire field, different value.
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","service_tier":"priority","output":[{"type":"message","content":[{"type":"output_text","text":"Hi"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();

        await provider.GenerateAsync(
            new Grok46 { Speed = SpeedType.Fast },
            new TestPrompt { Text = "Test" },
            CancellationToken.None);

        var json = JsonDocument.Parse(capturedRequest!);
        json.RootElement.GetProperty("service_tier").GetString().Should().Be("priority");
    }

    [Fact]
    public async Task GenerateAsync_AtStandardScheduling_OmitsServiceTier()
    {
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Hi"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();

        await provider.GenerateAsync(new Grok46(), new TestPrompt { Text = "Test" }, CancellationToken.None);

        var json = JsonDocument.Parse(capturedRequest!);
        json.RootElement.TryGetProperty("service_tier", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_RequestsStreamingOnTheWire()
    {
        // The buffered form held one response open for the whole generation, so a long answer
        // outlived the per-attempt timeout and was cancelled and retried from zero.
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"resp-123","output":[{"type":"message","content":[{"type":"output_text","text":"Hi"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();

        await provider.GenerateAsync(new Grok46(), new TestPrompt { Text = "Test" }, CancellationToken.None);

        var json = JsonDocument.Parse(capturedRequest!);
        json.RootElement.GetProperty("stream").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WhenPriorityWasDowngraded_BillsAtTheStandardRate()
    {
        // xAI serves priority best-effort and only charges the premium when it echoes
        // "priority" back. Billing the requested tier would double every cost figure for as
        // long as its priority capacity is exhausted.
        const string usage = """{"input_tokens":100000,"output_tokens":10000}""";
        SetupMockResponse(
            """{"id":"resp-123","service_tier":"default","output":[{"type":"message","content":[{"type":"output_text","text":"Hi"}]}],"usage":USAGE}""".Replace("USAGE", usage));

        var provider = CreateProvider();

        var result = await provider.GenerateAsync(
            new Grok46 { Speed = SpeedType.Fast },
            new TestPrompt { Text = "Test" },
            CancellationToken.None);

        // Standard grok-4.6 rates below 200K: $2 input / $6 output per 1M.
        result.MetaData.Usage.InputCost.Value.Should().Be(0.1m * 2.00m);
        result.MetaData.Usage.OutputCost.Value.Should().Be(0.01m * 6.00m);
    }

    [Fact]
    public async Task GenerateAsync_WhenPriorityWasGranted_BillsAtThePriorityRate()
    {
        const string usage = """{"input_tokens":100000,"output_tokens":10000}""";
        SetupMockResponse(
            """{"id":"resp-123","service_tier":"priority","output":[{"type":"message","content":[{"type":"output_text","text":"Hi"}]}],"usage":USAGE}""".Replace("USAGE", usage));

        var provider = CreateProvider();

        var result = await provider.GenerateAsync(
            new Grok46 { Speed = SpeedType.Fast },
            new TestPrompt { Text = "Test" },
            CancellationToken.None);

        result.MetaData.Usage.InputCost.Value.Should().Be(0.1m * 4.00m);
        result.MetaData.Usage.OutputCost.Value.Should().Be(0.01m * 12.00m);
    }

    private XProvider CreateProvider()
    {
        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.x.ai")
        };

        return new XProvider(
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
            // The text paths ask for stream:true and reassemble the frames, so the canned
            // buffered body is rendered as the SSE sequence the API would send.
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    ResponsesSse.FromResponseJson(responseJson), Encoding.UTF8, "text/event-stream")
            });
    }

    private class TestPrompt : IPrompt<string>
    {
        public string? System { get; set; }
        public required string Text { get; set; }
        public IReadOnlyList<Asset>? Files { get; set; }
    }

    private class StructuredPrompt : IPrompt<StructuredResponse>
    {
        public string? System { get; set; }
        public required string Text { get; set; }
        public IReadOnlyList<Asset>? Files { get; set; }
    }

    private class StructuredResponse
    {
        public string Message { get; set; } = "";
        public int Count { get; set; }
    }
}
