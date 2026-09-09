using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.OpenAi;
using WebSearchTool = Zonit.Extensions.Ai.OpenAi.Tools.WebSearchTool;
using FileSearchTool = Zonit.Extensions.Ai.OpenAi.Tools.FileSearchTool;

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
        Asset.MimeType pngMime = Asset.MimeType.ImagePng;
        var prompt = new TestPrompt
        {
            Text = "Describe this image",
            Files = [new Asset([0x89, 0x50, 0x4E, 0x47], "test.png", pngMime)]
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
        Asset.MimeType pdfMime = Asset.MimeType.ApplicationPdf;
        var prompt = new TestPrompt
        {
            Text = "Analyze this PDF",
            Files = [new Asset([0x25, 0x50, 0x44, 0x46], "test.pdf", pdfMime)]
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

    [Fact]
    public async Task TranscribeAsync_AudioWithoutExtensionInName_SendsExtensionAndContentType()
    {
        // Regression: OpenAI/Whisper detects the audio format from the filename
        // extension and/or the part's Content-Type. A voice note whose OriginalName
        // has no extension (e.g. "file_123" from a Telegram voice message) must
        // still upload with the detected extension + Content-Type, or the API
        // returns 400 "invalid file format".
        var handler = new TestHttpHandler { ResponseJson = """{"text":"witaj świecie"}""" };
        var provider = CreateProvider(handler);

        // OGG voice note ("OggS" magic bytes), original name WITHOUT an extension.
        var audio = new Asset([0x4F, 0x67, 0x67, 0x53], "file_123", Asset.MimeType.AudioOgg);

        var result = await provider.TranscribeAsync(new GPT4oTranscribe(), audio, language: "pl");

        result.Value.Should().Be("witaj świecie");
        handler.CapturedRequest.Should().NotBeNull();
        handler.CapturedRequest.Should().Contain(".ogg", "the upload filename must carry the detected extension");
        handler.CapturedRequest.Should().Contain("audio/ogg", "the part Content-Type must advertise the audio format");
        handler.CapturedRequest.Should().NotContain("filename=\"file_123\"", "the extensionless name must not be sent as-is");
    }

    [Fact]
    public async Task GenerateImageAsync_TokenPricedModel_BillsFromTheEndpointsUsageBlock()
    {
        // A real /v1/images/generations response: the usage block splits both
        // sides into text and image tokens, each on its own rate.
        _testHandler.ResponseJson = """
            {"created":1757000000,
             "data":[{"b64_json":"aGVsbG8="}],
             "usage":{"total_tokens":5640,
                      "input_tokens":1480,
                      "input_tokens_details":{"text_tokens":80,"image_tokens":1400},
                      "output_tokens":4160,
                      "output_tokens_details":{"image_tokens":4160,"text_tokens":0}}}
            """;

        var provider = CreateProvider();
        var model = new GPTImage25Flare
        {
            Quality = GPTImage25Base.QualityType.High,
            Size = GPTImage25Base.SizeType.Square,
        };

        var result = await provider.GenerateImageAsync(model, new ImagePrompt { Text = "a red bicycle" });

        var usage = result.MetaData!.Usage!;
        usage.InputTokens.Should().Be(1_480);
        usage.OutputTokens.Should().Be(4_160);

        // text  :     80/1M × $5  = 0.0004
        // image :  1_400/1M × $8  = 0.0112
        usage.InputCost.Value.Should().BeApproximately(0.0116m, 1e-9m);
        // output:  4_160/1M × $30 = 0.1248
        usage.OutputCost.Value.Should().BeApproximately(0.1248m, 1e-9m);
    }

    [Fact]
    public async Task GenerateImageAsync_FlatRateModel_KeepsThePerImagePrice()
    {
        // GPT Image 1.5 is billed per image, so the usage block (if any) must
        // not be turned into a token bill.
        _testHandler.ResponseJson = """
            {"created":1757000000,
             "data":[{"b64_json":"aGVsbG8="}],
             "usage":{"input_tokens":80,"output_tokens":4160}}
            """;

        var provider = CreateProvider();
        var model = new GPTImage15
        {
            Quality = GPTImage15.QualityType.High,
            Size = GPTImage15.SizeType.Square,
        };

        var result = await provider.GenerateImageAsync(model, new ImagePrompt { Text = "a red bicycle" });

        var usage = result.MetaData!.Usage!;
        usage.InputCost.Value.Should().Be(0m);
        usage.OutputCost.Value.Should().Be(0.200m);
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
