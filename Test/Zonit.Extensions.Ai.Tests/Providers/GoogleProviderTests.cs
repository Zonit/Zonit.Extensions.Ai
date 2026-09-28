using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Google;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Request shape and response parsing for the Gemini 3.x line: the thinking level
/// travels as <c>thinkingConfig.thinkingLevel</c>, deprecated sampling parameters stay
/// off the wire, a multi-part answer is not truncated to its first part, implicit
/// cache hits are billed at the cached rate, and the agent loop echoes the
/// <c>thoughtSignature</c> Gemini 3.x requires on a returned function call.
/// </summary>
public class GoogleProviderTests
{
    private const string HelloResponse =
        """{"candidates":[{"content":{"role":"model","parts":[{"text":"Hello"}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5}}""";

    [Fact]
    public async Task GenerateAsync_Gemini3WithReason_SendsThinkingLevel()
    {
        string? captured = null;
        var provider = CreateProvider(HelloResponse, r => captured = r);

        await provider.GenerateAsync(
            new Gemini38Flash { Reason = Gemini38Flash.ReasonType.High },
            new TestPrompt { Text = "Hi" },
            CancellationToken.None);

        using var doc = JsonDocument.Parse(captured!);
        doc.RootElement.GetProperty("generationConfig").GetProperty("thinkingConfig")
            .GetProperty("thinkingLevel").GetString().Should().Be("high");
    }

    [Fact]
    public async Task GenerateAsync_MinimalLevel_IsSentAsMinimal()
    {
        string? captured = null;
        var provider = CreateProvider(HelloResponse, r => captured = r);

        await provider.GenerateAsync(
            new Gemini35FlashLite { Reason = Gemini35FlashLite.ReasonType.Minimal },
            new TestPrompt { Text = "Hi" },
            CancellationToken.None);

        captured.Should().Contain("\"thinkingLevel\":\"minimal\"");
    }

    [Fact]
    public async Task GenerateAsync_Gemini3WithoutReason_SendsNeitherThinkingConfigNorSampling()
    {
        // Google deprecated temperature / top_p on 21 July 2026; the GoogleBase default
        // of topP 0.95 must not leak onto Gemini 3.x requests.
        string? captured = null;
        var provider = CreateProvider(HelloResponse, r => captured = r);

        await provider.GenerateAsync(new Gemini31Pro(), new TestPrompt { Text = "Hi" }, CancellationToken.None);

        captured.Should().NotContain("thinkingConfig");
        captured.Should().NotContain("topP");
        captured.Should().NotContain("temperature");
    }

    [Fact]
    public async Task GenerateAsync_JoinsEveryAnswerPart_AndSkipsThoughts()
    {
        const string body =
            """{"candidates":[{"content":{"role":"model","parts":[{"text":"(summary of reasoning)","thought":true},{"text":"Hello, ","thoughtSignature":"sig-a"},{"text":"world"}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5}}""";
        var provider = CreateProvider(body);

        var result = await provider.GenerateAsync(new Gemini38Flash(), new TestPrompt { Text = "Hi" }, CancellationToken.None);

        result.Value.Should().Be("Hello, world");
    }

    [Fact]
    public async Task GenerateAsync_ImplicitCacheHits_AreBilledAtTheCachedRate()
    {
        // promptTokenCount already includes cachedContentTokenCount.
        const string body =
            """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}],"usageMetadata":{"promptTokenCount":1000000,"cachedContentTokenCount":400000,"candidatesTokenCount":100000,"thoughtsTokenCount":100000}}""";
        var provider = CreateProvider(body);

        var model = new Gemini38Flash { PricingDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) };
        var result = await provider.GenerateAsync(model, new TestPrompt { Text = "Hi" }, CancellationToken.None);

        var usage = result.MetaData.Usage!;
        usage.CachedTokens.Should().Be(400_000);
        // 600K × $0.75 + 400K × $0.075 = 0.45 + 0.03
        usage.InputCost.Value.Should().BeApproximately(0.48m, 1e-9m);
        // (100K answer + 100K thinking) × $3.75
        usage.OutputCost.Value.Should().BeApproximately(0.75m, 1e-9m);
    }

    [Fact]
    public async Task EmbedAsync_SendsOutputDimensionality()
    {
        string? captured = null;
        var provider = CreateProvider("""{"embedding":{"values":[0.1,0.2]}}""", r => captured = r);

        await provider.EmbedAsync(new GeminiEmbedding2 { Dimensions = 768 }, "text", CancellationToken.None);

        captured.Should().Contain("\"model\":\"models/gemini-embedding-2\"");
        captured.Should().Contain("\"outputDimensionality\":768");
    }

    [Fact]
    public async Task AgentTurn_EchoesThoughtSignature_OnTheReturnedFunctionCall()
    {
        const string toolCallTurn =
            """{"candidates":[{"content":{"role":"model","parts":[{"functionCall":{"name":"get_weather","args":{"city":"Warsaw"}},"thoughtSignature":"c2lnbmF0dXJl"}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5}}""";

        var requests = new List<string>();
        var httpClient = new HttpClient(CreateHandler(toolCallTurn, requests.Add))
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com"),
        };
        var adapter = new GoogleAgentAdapter(
            httpClient, Options.Create(new GoogleOptions { ApiKey = "test" }), NullLogger<GoogleAgentAdapter>.Instance);

        await using var session = adapter.BeginSession(new AgentSessionContext
        {
            Llm = new Gemini38Flash(),
            Prompt = new TestPrompt { Text = "Weather in Warsaw?" },
            Tools = Array.Empty<ITool>(),
        });

        var first = await session.RunTurnAsync(null, CancellationToken.None);
        first.ToolCalls.Should().ContainSingle();

        await session.RunTurnAsync(
            [
                new ToolResult
                {
                    CallId = first.ToolCalls[0].Id,
                    Name = "get_weather",
                    Output = JsonDocument.Parse("""{"temp":21}""").RootElement.Clone(),
                },
            ],
            CancellationToken.None);

        using var second = JsonDocument.Parse(requests[1]);
        var modelTurn = second.RootElement.GetProperty("contents").EnumerateArray()
            .Single(c => c.GetProperty("role").GetString() == "model");
        var part = modelTurn.GetProperty("parts")[0];
        part.GetProperty("functionCall").GetProperty("name").GetString().Should().Be("get_weather");
        part.GetProperty("thoughtSignature").GetString().Should().Be("c2lnbmF0dXJl");
    }

    private static GoogleProvider CreateProvider(string responseJson, Action<string>? captureRequest = null)
    {
        var httpClient = new HttpClient(CreateHandler(responseJson, captureRequest))
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com"),
        };

        return new GoogleProvider(
            httpClient, Options.Create(new GoogleOptions { ApiKey = "test" }), NullLogger<GoogleProvider>.Instance);
    }

    private static HttpMessageHandler CreateHandler(string responseJson, Action<string>? captureRequest)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                if (captureRequest is not null && request.Content is not null)
                    captureRequest(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            })
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });

        return handler.Object;
    }

    private class TestPrompt : IPrompt<string>
    {
        public string? System { get; set; }
        public required string Text { get; set; }
        public IReadOnlyList<Asset>? Files { get; set; }
    }
}
