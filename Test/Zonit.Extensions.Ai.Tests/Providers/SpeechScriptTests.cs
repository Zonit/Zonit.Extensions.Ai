using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.ElevenLabs;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Tests the core batch speech overload <c>GenerateAsync(ISpeechLlm, IReadOnlyList&lt;string&gt;)</c>:
/// each line is auto-given the last sentence of the previous line and the first sentence of the
/// next line as prosody context, so a whole script is rendered as one continuous narration.
/// </summary>
public class SpeechScriptTests
{
    [Fact]
    public async Task Script_AutoStitchesBoundarySentences_PerLine()
    {
        var recorder = new RecordingSpeechProvider();
        var ai = BuildAi(recorder);
        var voice = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };

        string[] lines =
        [
            "Hello there. This is the opening.",   // two sentences
            "The middle line stands alone.",
            "Now the end. Goodbye everyone.",       // two sentences
        ];

        var results = await ai.GenerateAsync(voice, lines, CancellationToken.None);

        results.Should().HaveCount(3);
        recorder.Prompts.Should().HaveCount(3);

        // First line: no previous; next = FIRST sentence of line 2.
        recorder.Prompts[0].PreviousText.Should().BeNull();
        recorder.Prompts[0].NextText.Should().Be("The middle line stands alone.");

        // Middle line: previous = LAST sentence of line 1; next = FIRST sentence of line 3.
        recorder.Prompts[1].Text.Should().Be("The middle line stands alone.");
        recorder.Prompts[1].PreviousText.Should().Be("This is the opening.");
        recorder.Prompts[1].NextText.Should().Be("Now the end.");

        // Last line: previous = LAST sentence of line 2; no next.
        recorder.Prompts[2].PreviousText.Should().Be("The middle line stands alone.");
        recorder.Prompts[2].NextText.Should().BeNull();
    }

    [Fact]
    public async Task Script_WithNoPunctuation_CapsContextByCharsFromCorrectEnd()
    {
        var recorder = new RecordingSpeechProvider();
        var ai = BuildAi(recorder);
        var voice = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };

        // No punctuation → the whole line is one "sentence"; the char cap must kick in.
        var prev = string.Join(" ", Enumerable.Repeat("alpha", 100)); // ~599 chars, no '.'
        var next = string.Join(" ", Enumerable.Repeat("omega", 100));
        string[] lines = [prev, "the middle line", next];

        await ai.GenerateAsync(voice, lines, CancellationToken.None);

        // previous = TAIL of prev (nearest the boundary), capped and whole-word.
        var p = recorder.Prompts[1].PreviousText!;
        p.Length.Should().BeLessThanOrEqualTo(300);
        prev.Should().EndWith(p);
        p.Should().StartWith("alpha");

        // next = HEAD of next, capped and whole-word.
        var n = recorder.Prompts[1].NextText!;
        n.Length.Should().BeLessThanOrEqualTo(300);
        next.Should().StartWith(n);
        n.Should().EndWith("omega");
    }

    [Fact]
    public async Task PromptArray_SynthesizesEachAsGiven_NoAutoStitch()
    {
        var recorder = new RecordingSpeechProvider();
        var ai = BuildAi(recorder);
        var voice = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };

        // Each line carries its own delivery via inline v3 audio tags; no neighbour stitching.
        SpeechPrompt[] prompts =
        [
            new() { Text = "[excited] Hello there!" },
            new() { Text = "[whispers] A little secret.", PreviousText = "custom lead-in" },
        ];

        var results = await ai.GenerateAsync(voice, prompts, CancellationToken.None);

        results.Should().HaveCount(2);
        recorder.Prompts[0].Text.Should().Be("[excited] Hello there!");
        recorder.Prompts[0].PreviousText.Should().BeNull();   // not auto-filled
        recorder.Prompts[0].NextText.Should().BeNull();
        recorder.Prompts[1].PreviousText.Should().Be("custom lead-in"); // caller's own context kept
    }

    [Fact]
    public async Task Script_WithEmptyLine_Throws()
    {
        var ai = BuildAi(new RecordingSpeechProvider());
        var voice = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };

        var act = () => ai.GenerateAsync(voice, ["ok", "   "], CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static IAiProvider BuildAi(IModelProvider provider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAi();
        services.AddSingleton<IModelProvider>(provider);
        return services.BuildServiceProvider().GetRequiredService<IAiProvider>();
    }

    /// <summary>Records every <see cref="SpeechPrompt"/> it receives and returns a canned audio asset.</summary>
    private sealed class RecordingSpeechProvider : IModelProvider
    {
        public List<SpeechPrompt> Prompts { get; } = [];
        public string Name => "recording";
        public bool SupportsModel(ILlm llm) => llm is ISpeechLlm;

        public Task<Result<Asset>> GenerateSpeechAsync(ISpeechLlm llm, SpeechPrompt prompt, CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            return Task.FromResult(new Result<Asset>
            {
                Value = new Asset([0x01], "out.mp3"),
                MetaData = new MetaData { Model = llm, Provider = Name, PromptName = "Speech", Usage = new TokenUsage() },
            });
        }

        public Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
            ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Asset>> GenerateImageAsync(IImageLlm llm, IPrompt<Asset> prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<Asset>> GenerateVideoAsync(IVideoLlm llm, IPrompt<Asset> prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<float[]>> EmbedAsync(IEmbeddingLlm llm, string input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
            ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<string>> TranscribeAsync(IAudioLlm llm, Asset audioFile, string? language = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
