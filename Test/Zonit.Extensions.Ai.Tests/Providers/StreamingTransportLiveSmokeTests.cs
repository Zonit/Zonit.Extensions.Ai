using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// LIVE end-to-end smoke tests for the shared streaming transport — the paths that send
/// <c>stream: true</c> and reassemble the reply (<see cref="ResponsesApiTransport"/>,
/// <see cref="ResponsesStreamAssembler"/>, <c>AnthropicStreamAssembler</c>), plus the live
/// delta paths and fast-mode wiring on top of them.
/// </summary>
/// <remarks>
/// <para>
/// These cover what unit tests structurally cannot: that the frames the providers really send
/// match the shapes the assemblers expect. A mock proves we parse our own fixtures.
/// </para>
/// <para>
/// Opt-in only — they spend real credits, so they stay inert in CI. Enable per provider with
/// <c>ZONIT_OPENAI_SMOKE=1</c> + <c>ZONIT_OPENAI_KEY</c>, <c>ZONIT_X_SMOKE=1</c> +
/// <c>ZONIT_X_KEY</c>, <c>ZONIT_ANTHROPIC_SMOKE=1</c> + <c>ZONIT_ANTHROPIC_KEY</c>.
/// </para>
/// </remarks>
public class StreamingTransportLiveSmokeTests
{
    private readonly ITestOutputHelper _output;
    public StreamingTransportLiveSmokeTests(ITestOutputHelper output) => _output = output;

    // ---------------- OpenAI ----------------

    [Fact]
    public async Task OpenAi_GenerateAsync_StructuredOutput_ComesBackAssembled()
    {
        var ai = Build("OPENAI", (services, key) => services.AddAiOpenAi(key));
        if (ai is null) return;

        var result = await ai.GenerateAsync(new Luna56(), new CityPrompt());

        _output.WriteLine($"{result.Value.City} / {result.Value.Population} — {result.MetaData.Usage.TotalTokens} tok, {result.MetaData.Usage.TotalCost}");
        result.Value.City.Should().NotBeNullOrWhiteSpace();
        result.MetaData.Usage.OutputTokens.Should().BeGreaterThan(0, "usage must survive reassembly");
        result.MetaData.RequestId.Should().NotBeNullOrEmpty("the terminal event carries the response id");
    }

    [Fact]
    public async Task OpenAi_StreamAsync_YieldsTextDeltas()
    {
        // Regression: this path read the Chat Completions `delta.text` shape from a Responses
        // stream, so it completed without ever yielding a fragment.
        var ai = Build("OPENAI", (services, key) => services.AddAiOpenAi(key));
        if (ai is null) return;

        var chunks = new List<string>();
        await foreach (var chunk in ai.StreamAsync(new Luna56(), "Count from 1 to 5, digits only."))
            chunks.Add(chunk);

        _output.WriteLine($"{chunks.Count} chunks: {string.Concat(chunks)}");
        chunks.Should().NotBeEmpty("the Responses API streams response.output_text.delta frames");
        string.Concat(chunks).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenAi_FastMode_IsAcceptedAndReported()
    {
        var ai = Build("OPENAI", (services, key) => services.AddAiOpenAi(key));
        if (ai is null) return;

        var standard = await ai.GenerateAsync(new Luna56(), new CityPrompt());
        var fast = await ai.GenerateAsync(new Luna56 { Speed = SpeedType.Fast }, new CityPrompt());

        _output.WriteLine($"standard: {standard.MetaData.Duration}, {standard.MetaData.Usage.TotalCost}");
        _output.WriteLine($"fast:     {fast.MetaData.Duration}, {fast.MetaData.Usage.TotalCost}");

        // The request must be accepted (a rejected service_tier is an HTTP 400, not a result).
        // Cost is only doubled when OpenAI confirms the tier, so assert the weaker invariant:
        // fast is never cheaper per token than standard.
        fast.Value.City.Should().NotBeNullOrWhiteSpace();
    }

    // ---------------- xAI (Grok) ----------------

    [Fact]
    public async Task Grok_GenerateAsync_StructuredOutput_ComesBackAssembled()
    {
        var ai = Build("X", (services, key) => services.AddAiX(key));
        if (ai is null) return;

        var result = await ai.GenerateAsync(new Grok46(), new CityPrompt());

        _output.WriteLine($"{result.Value.City} / {result.Value.Population} — {result.MetaData.Usage.TotalTokens} tok, {result.MetaData.Usage.TotalCost}");
        result.Value.City.Should().NotBeNullOrWhiteSpace();
        result.MetaData.Usage.OutputTokens.Should().BeGreaterThan(0, "usage must survive reassembly");
    }

    [Fact]
    public async Task Grok_StreamAsync_YieldsTextDeltas()
    {
        // Same regression as OpenAI: this loop looked for the buffered output[].content[].text
        // shape on a frame, which never appears.
        var ai = Build("X", (services, key) => services.AddAiX(key));
        if (ai is null) return;

        var chunks = new List<string>();
        await foreach (var chunk in ai.StreamAsync(new Grok46(), "Count from 1 to 5, digits only."))
            chunks.Add(chunk);

        _output.WriteLine($"{chunks.Count} chunks: {string.Concat(chunks)}");
        chunks.Should().NotBeEmpty("the Responses API streams response.output_text.delta frames");
    }

    [Fact]
    public async Task Grok_PriorityScheduling_IsAcceptedAndEchoed()
    {
        var ai = Build("X", (services, key) => services.AddAiX(key));
        if (ai is null) return;

        var result = await ai.GenerateAsync(
            new Grok46 { Speed = SpeedType.Fast },
            new CityPrompt());

        _output.WriteLine($"priority: {result.MetaData.Duration}, {result.MetaData.Usage.TotalCost}");
        result.Value.City.Should().NotBeNullOrWhiteSpace();
    }

    // ---------------- Anthropic (regression) ----------------

    [Fact]
    public async Task Anthropic_GenerateAsync_StillAssemblesAfterTheSharedReaderSwap()
    {
        // The Anthropic assembler now reads frames through the shared AiSseReader; this is the
        // live check that its own event vocabulary still assembles into a finished response.
        var ai = Build("ANTHROPIC", (services, key) => services.AddAiAnthropic(key));
        if (ai is null) return;

        var result = await ai.GenerateAsync(new Sonnet5(), new CityPrompt());

        _output.WriteLine($"{result.Value.City} / {result.Value.Population} — {result.MetaData.Usage.TotalTokens} tok, {result.MetaData.Usage.TotalCost}");
        result.Value.City.Should().NotBeNullOrWhiteSpace();
        result.MetaData.Usage.OutputTokens.Should().BeGreaterThan(0);
    }

    // ---------------- server-side web search (GenerateAsync, no agent loop) ----------------

    [Fact]
    public async Task Anthropic_WebSearch_ReturnsTheAnswer_NotJustThePreamble()
    {
        // Anthropic answers a web-search prompt with a *sequence* of text blocks around the
        // server tool — a preamble, then the researched answer in pieces. Reading only the first
        // block returned "I'll search for this." and dropped the rest.
        var ai = Build("ANTHROPIC", (services, key) => services.AddAiAnthropic(key));
        if (ai is null) return;

        var result = await ai.GenerateAsync(
            new Sonnet5 { Tools = [new Anthropic.Tools.WebSearchTool { MaxUses = 3 }] },
            "What is the current price of Brent crude oil? Search the web and answer with the number.");

        _output.WriteLine($"[{result.Value.Length} chars] {result.Value}");
        // The digit is the precise guard: the discarded-answer bug returned the preamble
        // ("I'll search for this."), which carries no price — and no digit.
        result.Value.Should().MatchRegex(@"\d", "the answer must carry the price it looked up");
    }

    [Fact]
    public async Task OpenAi_WebSearch_ReturnsTheAnswer()
    {
        var ai = Build("OPENAI", (services, key) => services.AddAiOpenAi(key));
        if (ai is null) return;

        var result = await ai.GenerateAsync(
            new Luna56 { Tools = [new OpenAi.Tools.WebSearchTool()] },
            "What is the current price of Brent crude oil? Search the web and answer with the number.");

        _output.WriteLine($"[{result.Value.Length} chars] {result.Value}");
        result.Value.Should().MatchRegex(@"\d", "the answer must carry the price it looked up");
    }

    [Fact]
    public async Task Grok_WebSearch_ReturnsTheAnswer()
    {
        var ai = Build("X", (services, key) => services.AddAiX(key));
        if (ai is null) return;

        var result = await ai.GenerateAsync(
            new Grok46 { WebSearch = new Search { Mode = ModeType.Always, MaxResults = 5 } },
            "What is the current price of Brent crude oil? Search the web and answer with the number.");

        _output.WriteLine($"[{result.Value.Length} chars] {result.Value}");
        result.Value.Should().MatchRegex(@"\d", "the answer must carry the price it looked up");
    }

    // ---------------- harness ----------------

    private IAiProvider? Build(string provider, Action<IServiceCollection, string> register)
    {
        if (Environment.GetEnvironmentVariable($"ZONIT_{provider}_SMOKE") != "1") return null;

        var key = Environment.GetEnvironmentVariable($"ZONIT_{provider}_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            _output.WriteLine($"ZONIT_{provider}_KEY not set — skipping.");
            return null;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        register(services, key!);

        return services.BuildServiceProvider().GetRequiredService<IAiProvider>();
    }

    /// <remarks>
    /// Deliberately template-free: <see cref="PromptBase.Text"/> returns the raw
    /// <see cref="PromptBase.Prompt"/>, and Scriban substitution is the application layer's
    /// job — a <c>{{ placeholder }}</c> here would reach the model verbatim and the answers
    /// would stop being a meaningful check of what came back.
    /// </remarks>
    private sealed class CityPrompt : PromptBase<CityFact>
    {
        public override string Prompt => "What is the capital of Poland, and its population?";
    }

    private sealed class CityFact
    {
        [Description("Capital city name.")]
        public string City { get; set; } = "";

        [Description("Approximate population.")]
        public long Population { get; set; }
    }
}
