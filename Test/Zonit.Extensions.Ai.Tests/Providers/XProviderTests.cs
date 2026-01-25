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
        var model = new Grok3();

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
        SetupMockResponse("""{"id":"chatcmpl-123","choices":[{"message":{"content":"Hello"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok3();
        var prompt = new TestPrompt { Text = "Say hello" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        var json = JsonDocument.Parse(capturedRequest!);
        json.RootElement.GetProperty("model").GetString().Should().Contain("grok");
        json.RootElement.GetProperty("messages").EnumerateArray().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_WithWebSearch_ShouldIncludeAgentTools()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"chatcmpl-123","choices":[{"message":{"content":"Found news"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok3
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
        capturedRequest.Should().Contain("\"type\":\"live_search\"");
        capturedRequest.Should().Contain("\"mode\":\"on\"");
        capturedRequest.Should().Contain("max_search_results");
    }

    [Fact]
    public async Task GenerateAsync_WithWebSearchSources_ShouldIncludeSourceConfig()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"chatcmpl-123","choices":[{"message":{"content":"Found"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok3
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
        capturedRequest.Should().Contain("\"type\":\"live_search\"");
        capturedRequest.Should().Contain("sources");
        capturedRequest.Should().Contain("\"type\":\"web\"");
        capturedRequest.Should().Contain("\"type\":\"x\"");
        capturedRequest.Should().Contain("elonmusk");
    }

    [Fact]
    public async Task GenerateAsync_WithDateRange_ShouldIncludeDateFilters()
    {
        // Arrange
        string? capturedRequest = null;
        SetupMockResponse("""{"id":"chatcmpl-123","choices":[{"message":{"content":"Found"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok3
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
        SetupMockResponse("""{"id":"chatcmpl-123","choices":[{"message":{"content":"Image analyzed"}}],"usage":{"prompt_tokens":100,"completion_tokens":10}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok3();
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
        SetupMockResponse("""{"id":"chatcmpl-123","choices":[{"message":{"content":"{\"message\":\"Hello\",\"count\":5}"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok3();
        var prompt = new StructuredPrompt { Text = "Generate data" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest.Should().Contain("response_format");
        capturedRequest.Should().Contain("json_schema");
        capturedRequest.Should().Contain("\"strict\":true");
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnCorrectResult()
    {
        // Arrange
        SetupMockResponse("""{"id":"chatcmpl-123","choices":[{"message":{"content":"Hello World"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""");

        var provider = CreateProvider();
        var model = new Grok3();
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
        SetupMockResponse("""{"id":"chatcmpl-123","choices":[{"message":{"content":"No search"}}],"usage":{"prompt_tokens":10,"completion_tokens":5}}""",
            request => capturedRequest = request);

        var provider = CreateProvider();
        var model = new Grok3
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
