using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.ElevenLabs;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Tests for ElevenLabsProvider TTS request building — in particular the
/// prosody-continuity stitching via <see cref="SpeechPrompt.PreviousText"/> /
/// <see cref="SpeechPrompt.NextText"/> (ElevenLabs <c>previous_text</c> / <c>next_text</c>).
/// </summary>
public class ElevenLabsProviderTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock = new();

    [Fact]
    public async Task GenerateSpeech_WithSurroundingText_SendsPreviousAndNextText()
    {
        string? captured = null;
        SetupAudioResponse(req => captured = req);

        var provider = CreateProvider();
        var model = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };
        var prompt = new SpeechPrompt
        {
            Text = "I could not believe my eyes.",
            PreviousText = "She opened the ancient door.",
            NextText = "The room was full of gold.",
        };

        await provider.GenerateSpeechAsync(model, prompt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured.Should().Contain("\"previous_text\":\"She opened the ancient door.\"");
        captured.Should().Contain("\"next_text\":\"The room was full of gold.\"");
        captured.Should().Contain("\"text\":\"I could not believe my eyes.\"");
    }

    [Fact]
    public async Task GenerateSpeech_WithSeed_SendsSeedForDeterministicOutput()
    {
        string? captured = null;
        SetupAudioResponse(req => captured = req);

        var provider = CreateProvider();
        // One fixed seed across a script keeps the narrator's character consistent.
        var model = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel, Seed = 12345 };

        await provider.GenerateSpeechAsync(model, new SpeechPrompt { Text = "Line one." }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured.Should().Contain("\"seed\":12345");
    }

    [Fact]
    public void Model_ByDefault_HasRandomSeedInRange()
    {
        var model = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };

        model.Seed.Should().NotBeNull();
        model.Seed!.Value.Should().BeInRange(0, ElevenLabsSpeechBase.MaxSeed);
    }

    [Fact]
    public async Task GenerateSpeech_ByDefault_AutoSendsSeed()
    {
        string? captured = null;
        SetupAudioResponse(req => captured = req);

        var provider = CreateProvider();
        // No explicit seed → a random one is generated at model construction and sent every call.
        var model = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };

        await provider.GenerateSpeechAsync(model, new SpeechPrompt { Text = "Line one." }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured.Should().Contain($"\"seed\":{model.Seed}");
    }

    [Fact]
    public async Task GenerateSpeech_WithExplicitNullSeed_OmitsSeed()
    {
        string? captured = null;
        SetupAudioResponse(req => captured = req);

        var provider = CreateProvider();
        // Explicit opt-out: let ElevenLabs randomize per call.
        var model = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel, Seed = null };

        await provider.GenerateSpeechAsync(model, new SpeechPrompt { Text = "Line one." }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured.Should().NotContain("seed");
    }

    [Fact]
    public async Task GenerateSpeech_WithoutSurroundingText_OmitsPreviousAndNextText()
    {
        string? captured = null;
        SetupAudioResponse(req => captured = req);

        var provider = CreateProvider();
        var model = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };
        var prompt = new SpeechPrompt { Text = "Just one standalone line." };

        await provider.GenerateSpeechAsync(model, prompt, CancellationToken.None);

        captured.Should().NotBeNull();
        captured.Should().NotContain("previous_text");
        captured.Should().NotContain("next_text");
    }

    [Fact]
    public async Task GenerateSpeech_SendsSpeedInVoiceSettings()
    {
        string? captured = null;
        SetupAudioResponse(req => captured = req);

        var provider = CreateProvider();
        var model = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel, Speed = 0.9 };

        await provider.GenerateSpeechAsync(model, new SpeechPrompt { Text = "Line." }, CancellationToken.None);

        captured.Should().Contain("\"speed\":0.9");
    }

    [Fact]
    public async Task GenerateSpeech_WithLanguageCode_SendsIt_ElseOmits()
    {
        string? withCode = null, withoutCode = null;
        var provider = CreateProvider();

        SetupAudioResponse(req => withCode = req);
        await provider.GenerateSpeechAsync(
            new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel, LanguageCode = "pl" },
            new SpeechPrompt { Text = "Cześć." }, CancellationToken.None);

        SetupAudioResponse(req => withoutCode = req);
        await provider.GenerateSpeechAsync(
            new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel },
            new SpeechPrompt { Text = "Hello." }, CancellationToken.None);

        withCode.Should().Contain("\"language_code\":\"pl\"");
        withoutCode.Should().NotContain("language_code");
    }

    [Fact]
    public async Task GenerateSpeech_TextNormalization_SentOnlyWhenNotAuto()
    {
        string? off = null, auto = null;
        var provider = CreateProvider();

        SetupAudioResponse(req => off = req);
        await provider.GenerateSpeechAsync(
            new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel, TextNormalization = TextNormalizationMode.Off },
            new SpeechPrompt { Text = "Call 911." }, CancellationToken.None);

        SetupAudioResponse(req => auto = req);
        await provider.GenerateSpeechAsync(
            new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel },  // default Auto
            new SpeechPrompt { Text = "Call 911." }, CancellationToken.None);

        off.Should().Contain("\"apply_text_normalization\":\"off\"");
        auto.Should().NotContain("apply_text_normalization");
    }

    [Fact]
    public async Task GenerateSpeech_OnV3_OmitsStitchingContext()
    {
        string? captured = null;
        SetupAudioResponse(req => captured = req);

        var provider = CreateProvider();
        // v3 does not support request stitching — previous/next must not be sent even if set.
        var model = new ElevenV3 { Voice = ElevenVoices.Rachel };
        var prompt = new SpeechPrompt { Text = "[excited] Hi!", PreviousText = "before", NextText = "after" };

        await provider.GenerateSpeechAsync(model, prompt, CancellationToken.None);

        captured.Should().NotContain("previous_text");
        captured.Should().NotContain("next_text");
    }

    [Fact]
    public void ModelCapabilities_MatchElevenLabsMatrix()
    {
        var v3 = new ElevenV3 { Voice = ElevenVoices.Rachel };
        v3.SupportsAudioTags.Should().BeTrue();
        v3.SupportsRequestStitching.Should().BeFalse();
        v3.MaxCharacters.Should().Be(5_000);

        var v2 = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };
        v2.SupportsAudioTags.Should().BeFalse();
        v2.SupportsRequestStitching.Should().BeTrue();
        v2.MaxCharacters.Should().Be(10_000);
    }

    [Fact]
    public async Task GenerateWithTimestamps_ParsesAlignment_AndRollsUpToWords()
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    """
                    {
                      "audio_base64": "AQID",
                      "alignment": {
                        "characters": ["H","i"," ","y","o","u"],
                        "character_start_times_seconds": [0.0, 0.1, 0.2, 0.3, 0.4, 0.5],
                        "character_end_times_seconds":   [0.1, 0.2, 0.3, 0.4, 0.5, 0.6]
                      }
                    }
                    """, Encoding.UTF8, "application/json"),
            });

        var provider = CreateProvider();
        var model = new ElevenMultilingualV2 { Voice = ElevenVoices.Rachel };

        var result = await provider.GenerateSpeechWithTimestampsAsync(
            model, new SpeechPrompt { Text = "Hi you" }, CancellationToken.None);

        // audio decoded from base64 "AQID" → bytes 1,2,3
        result.Value.Audio.Data.Should().Equal((byte)1, (byte)2, (byte)3);

        // character-level alignment
        result.Value.Characters.Should().HaveCount(6);
        result.Value.Characters[0].Character.Should().Be("H");
        result.Value.Characters[0].Start.Should().Be(TimeSpan.Zero);
        result.Value.Characters[0].End.Should().Be(TimeSpan.FromSeconds(0.1));

        // rolled up to words (split on whitespace) — ready for subtitle cues
        var words = result.Value.ToWords();
        words.Should().HaveCount(2);
        words[0].Word.Should().Be("Hi");
        words[0].Start.Should().Be(TimeSpan.Zero);
        words[0].End.Should().Be(TimeSpan.FromSeconds(0.2));
        words[1].Word.Should().Be("you");
        words[1].Start.Should().Be(TimeSpan.FromSeconds(0.3));
        words[1].End.Should().Be(TimeSpan.FromSeconds(0.6));
    }

    private ElevenLabsProvider CreateProvider()
    {
        var httpClient = new HttpClient(_httpHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.elevenlabs.io"),
        };
        var options = new ElevenLabsOptions { ApiKey = "test-api-key" };
        return new ElevenLabsProvider(
            httpClient,
            Options.Create(options),
            new Mock<ILogger<ElevenLabsProvider>>().Object);
    }

    private void SetupAudioResponse(Action<string> captureRequest)
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                if (request.Content != null)
                    captureRequest(await request.Content.ReadAsStringAsync());
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent([0x01, 0x02, 0x03]),
            });
    }
}
