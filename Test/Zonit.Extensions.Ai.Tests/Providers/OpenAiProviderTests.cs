using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Zonit.Extensions.Ai.OpenAi;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Tests for OpenAiProvider - request building, tool serialization, file handling.
/// </summary>
public class OpenAiProviderTests
{
    private readonly Mock<ILogger<OpenAiProvider>> _loggerMock;
    private readonly TestHttpHandler _testHandler;
    private readonly OpenAiOptions _options;

    public OpenAiProviderTests()
    {
        _loggerMock = new Mock<ILogger<OpenAiProvider>>();
        _testHandler = new TestHttpHandler();
        _options = new OpenAiOptions { ApiKey = "test-api-key" };
    }

    [Fact]
    public void SupportsModel_WithOpenAiBase_ShouldReturnTrue()
    {
        // Arrange
        var provider = CreateProvider();
        var model = new GPT41();

        // Act & Assert
        provider.SupportsModel(model).Should().BeTrue();
    }

    [Fact]
    public void SupportsModel_WithNonOpenAiModel_ShouldReturnFalse()
    {
        // Arrange
        var provider = CreateProvider();
        var model = new Mock<ILlm>().Object;

        // Act & Assert
        provider.SupportsModel(model).Should().BeFalse();
    }

    [Fact]
    public void Name_ShouldReturnOpenAI()
    {
        // Arrange
        var provider = CreateProvider();

        // Act & Assert
        provider.Name.Should().Be("OpenAI");
    }

    [Fact]
    public async Task GenerateAsync_ShouldSendRequestWithCorrectFormat()
    {
        // Arrange
        _testHandler.ResponseJson = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Hello"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""";

        var provider = CreateProvider();
        var model = new GPT41();
        var prompt = new TestPrompt { Text = "Say hello" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        _testHandler.CapturedRequest.Should().NotBeNull();
        var json = JsonDocument.Parse(_testHandler.CapturedRequest!);
        json.RootElement.GetProperty("model").GetString().Should().StartWith("gpt-4.1");
        json.RootElement.GetProperty("input").EnumerateArray().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_WithTools_ShouldIncludeToolsInRequest()
    {
        // Arrange
        _testHandler.ResponseJson = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Test"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""";

        var provider = CreateProvider();
        var model = new GPT41
        {
            Tools = [new WebSearchTool { ContextSize = WebSearchTool.ContextSizeType.High }]
        };
        var prompt = new TestPrompt { Text = "Search for news" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        _testHandler.CapturedRequest.Should().NotBeNull();
        var json = JsonDocument.Parse(_testHandler.CapturedRequest!);
        json.RootElement.GetProperty("tools").EnumerateArray().Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_WithFileSearchTool_ShouldIncludeVectorStoreConfig()
    {
        // Arrange
        _testHandler.ResponseJson = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Found"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""";

        var provider = CreateProvider();
        var model = new GPT41
        {
            Tools = [new FileSearchTool
            {
                VectorId = "vs_test123",
                MaxNumResults = 10,
                RankingOptions = new FileSearchTool.RankingOptionsType { Ranker = "auto", ScoreThreshold = 0.5 }
            }]
        };
        var prompt = new TestPrompt { Text = "Search files" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        _testHandler.CapturedRequest.Should().NotBeNull();
        _testHandler.CapturedRequest.Should().Contain("file_search");
        _testHandler.CapturedRequest.Should().Contain("vs_test123");
        _testHandler.CapturedRequest.Should().Contain("ranking_options");
    }

    [Fact]
    public async Task GenerateAsync_WithImage_ShouldIncludeImageInRequest()
    {
        // Arrange
        _testHandler.ResponseJson = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Image analyzed"}]}],"usage":{"input_tokens":100,"output_tokens":10}}""";

        var provider = CreateProvider();
        var model = new GPT41();
        var prompt = new TestPrompt
        {
            Text = "Describe this image",
            Files = [File.FromBytes([0x89, 0x50, 0x4E, 0x47], "image/png", "test.png")]
        };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        _testHandler.CapturedRequest.Should().NotBeNull();
        _testHandler.CapturedRequest.Should().Contain("input_image");
        _testHandler.CapturedRequest.Should().Contain("image/png");
    }

    [Fact]
    public async Task GenerateAsync_WithPdfDocument_ShouldIncludeInputFileInRequest()
    {
        // Arrange
        _testHandler.ResponseJson = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"PDF analyzed"}]}],"usage":{"input_tokens":100,"output_tokens":10}}""";

        var provider = CreateProvider();
        var model = new GPT41();
        var prompt = new TestPrompt
        {
            Text = "Analyze this PDF",
            Files = [File.FromBytes([0x25, 0x50, 0x44, 0x46], "application/pdf", "test.pdf")]
        };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        _testHandler.CapturedRequest.Should().NotBeNull();
        _testHandler.CapturedRequest.Should().Contain("input_file");
        _testHandler.CapturedRequest.Should().Contain("application/pdf");
    }

    [Fact]
    public async Task GenerateAsync_WithStoreLogs_ShouldIncludeStoreInRequest()
    {
        // Arrange
        _testHandler.ResponseJson = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Stored"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""";

        var provider = CreateProvider();
        var model = new GPT41 { StoreLogs = true };
        var prompt = new TestPrompt { Text = "Test" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        _testHandler.CapturedRequest.Should().NotBeNull();
        _testHandler.CapturedRequest.Should().Contain("\"store\":true");
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnCorrectResult()
    {
        // Arrange
        _testHandler.ResponseJson = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Hello World"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""";

        var provider = CreateProvider();
        var model = new GPT41();
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
    public async Task GenerateAsync_WithStructuredOutput_ShouldIncludeJsonSchema()
    {
        // Arrange
        _testHandler.ResponseJson = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"{\"message\":\"Hello\",\"count\":5}"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""";

        var provider = CreateProvider();
        var model = new GPT41();
        var prompt = new StructuredPrompt { Text = "Generate data" };

        // Act
        await provider.GenerateAsync(model, prompt, CancellationToken.None);

        // Assert
        _testHandler.CapturedRequest.Should().NotBeNull();
        _testHandler.CapturedRequest.Should().Contain("json_schema");
        _testHandler.CapturedRequest.Should().Contain("\"strict\":true");
    }

    private OpenAiProvider CreateProvider(TestHttpHandler? handler = null)
    {
        var httpClient = new HttpClient(handler ?? _testHandler)
        {
            BaseAddress = new Uri("https://api.openai.com")
        };

        return new OpenAiProvider(
            httpClient,
            Options.Create(_options),
            _loggerMock.Object);
    }

    private class TestHttpHandler : HttpMessageHandler
    {
        public string ResponseJson { get; set; } = """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"Test"}]}],"usage":{"input_tokens":10,"output_tokens":5}}""";
        public string? CapturedRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                CapturedRequest = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(ResponseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    // Test prompt implementations
    private class TestPrompt : IPrompt<string>
    {
        public string? System { get; set; }
        public required string Text { get; set; }
        public IReadOnlyList<File>? Files { get; set; }
    }

    private class StructuredPrompt : IPrompt<StructuredResponse>
    {
        public string? System { get; set; }
        public required string Text { get; set; }
        public IReadOnlyList<File>? Files { get; set; }
    }

    private class StructuredResponse
    {
        public string Message { get; set; } = "";
        public int Count { get; set; }
    }
}
