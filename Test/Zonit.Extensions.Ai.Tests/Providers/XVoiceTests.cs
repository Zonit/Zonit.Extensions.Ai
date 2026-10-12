using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// xAI voice: speech-to-text on <c>/v1/stt</c> (<see cref="GrokVoiceTranscribe20"/>), text-to-speech
/// on <c>/v1/tts</c> (<see cref="GrokTts"/>), plus the Grok Imagine Video 1.5 Lite rate card.
/// </summary>
public class XVoiceTests
{
    private readonly Mock<HttpMessageHandler> _handler = new();
    private HttpRequestMessage? _request;
    private string? _body;

    [Fact]
    public async Task Transcribe_PostsMultipartToStt_WithTheFileLast_AndBillsByDuration()
    {
        Respond("""{"text":"Hello world","language":"en","duration":1800.0,"words":[]}""", "application/json");

        var audio = new Asset(Encoding.ASCII.GetBytes("RIFF....WAVEfmt "), "clip.wav");
        var result = await CreateProvider().TranscribeAsync(new GrokVoiceTranscribe20(), audio, "en");

        result.Value.Should().Be("Hello world");
        _request!.RequestUri!.AbsolutePath.Should().Be("/v1/stt");
        _body.Should().Contain("grok-voice-transcribe-2.0");

        // xAI may ignore fields that follow the file on streamable uploads.
        _body!.IndexOf("name=model", StringComparison.Ordinal).Should().BeLessThan(_body.IndexOf("name=file", StringComparison.Ordinal));
        _body.IndexOf("name=language", StringComparison.Ordinal).Should().BeLessThan(_body.IndexOf("name=file", StringComparison.Ordinal));

        // Half an hour at $0.10 per hour.
        result.MetaData.Usage!.InputCost.Value.Should().Be(0.05m);
    }

    [Fact]
    public async Task Speech_PostsTheTtsBody_AndReturnsTheAudio()
    {
        Respond(new byte[] { 0xFF, 0xFB, 0x90, 0x00 }, "audio/mpeg");

        var model = new GrokTts { Voice = GrokVoices.Ara, Language = "pl", Speed = 1.2 };
        var result = await CreateProvider().GenerateSpeechAsync(model, new SpeechPrompt { Text = "Cześć" });

        _request!.RequestUri!.AbsolutePath.Should().Be("/v1/tts");
        var json = JsonDocument.Parse(_body!).RootElement;
        json.GetProperty("text").GetString().Should().Be("Cześć");
        json.GetProperty("voice_id").GetString().Should().Be("ara");
        json.GetProperty("language").GetString().Should().Be("pl");
        json.GetProperty("speed").GetDouble().Should().Be(1.2);
        json.GetProperty("output_format").GetProperty("codec").GetString().Should().Be("mp3");
        json.GetProperty("output_format").GetProperty("sample_rate").GetInt32().Should().Be(24_000);
        json.GetProperty("output_format").GetProperty("bit_rate").GetInt32().Should().Be(128_000);
        json.TryGetProperty("model", out _).Should().BeFalse("the TTS endpoint takes no model id");
        json.TryGetProperty("with_timestamps", out _).Should().BeFalse();

        result.Value.Data.Should().HaveCount(4);
        // 5 characters at $15 per 1M.
        result.MetaData.Usage!.OutputCost.Value.Should().Be(0.000075m);
    }

    [Fact]
    public async Task Speech_WithoutBitRateCodec_LeavesBitRateOff_AndDefaultsOffTheWire()
    {
        Respond(new byte[] { 1, 2, 3 }, "audio/wav");

        await CreateProvider().GenerateSpeechAsync(new GrokTts { Format = GrokAudioFormat.Wav_44100 }, new SpeechPrompt { Text = "Hi" });

        var json = JsonDocument.Parse(_body!).RootElement;
        json.GetProperty("output_format").GetProperty("codec").GetString().Should().Be("wav");
        json.GetProperty("output_format").GetProperty("sample_rate").GetInt32().Should().Be(44_100);
        json.GetProperty("output_format").TryGetProperty("bit_rate", out _).Should().BeFalse();
        json.TryGetProperty("speed", out _).Should().BeFalse();
        json.GetProperty("language").GetString().Should().Be("auto");
        json.GetProperty("voice_id").GetString().Should().Be("eve");
    }

    [Fact]
    public async Task SpeechWithTimestamps_MapsGraphCharsAndTimes()
    {
        var audio = Convert.ToBase64String(new byte[] { 9, 8, 7 });
        Respond($$$"""
            {"audio":"{{{audio}}}","content_type":"audio/mpeg","duration":0.12,
             "audio_timestamps":{"graph_chars":["H","i"],"graph_times":[[0.0,0.06],[0.06,0.12]]}}
            """, "application/json");

        var result = await CreateProvider().GenerateSpeechWithTimestampsAsync(new GrokTts(), new SpeechPrompt { Text = "Hi" });

        JsonDocument.Parse(_body!).RootElement.GetProperty("with_timestamps").GetBoolean().Should().BeTrue();
        result.Value.Audio.Data.Should().Equal(9, 8, 7);
        result.Value.Characters.Should().HaveCount(2);
        result.Value.Characters[1].Character.Should().Be("i");
        result.Value.Characters[1].Start.Should().Be(TimeSpan.FromSeconds(0.06));
        result.Value.Characters[1].End.Should().Be(TimeSpan.FromSeconds(0.12));
    }

    [Fact]
    public async Task Speech_RejectsOutOfRangeSettings_BeforeSending()
    {
        var provider = CreateProvider();

        await FluentActions.Awaiting(() => provider.GenerateSpeechAsync(new GrokTts { Speed = 2.0 }, new SpeechPrompt { Text = "Hi" }))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*Speed*");
        await FluentActions.Awaiting(() => provider.GenerateSpeechAsync(new GrokTts(), new SpeechPrompt { Text = new string('a', GrokTts.MaxCharacters + 1) }))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*60000*");

        _request.Should().BeNull();
    }

    [Theory]
    [InlineData(GrokImagineVideo15Lite.ResolutionType.Resolution480p, 0.10)]
    [InlineData(GrokImagineVideo15Lite.ResolutionType.Resolution720p, 0.70)]
    [InlineData(GrokImagineVideo15Lite.ResolutionType.Resolution1080p, 0.70)]
    public void VideoLite_PricesPerSecondByResolution(GrokImagineVideo15Lite.ResolutionType resolution, double fiveSeconds)
    {
        var model = new GrokImagineVideo15Lite { Resolution = resolution, DurationSeconds = 5 };

        model.Name.Should().Be("grok-imagine-video-1.5-lite");
        model.GetVideoGenerationPrice().Should().Be((decimal)fiveSeconds);
    }

    [Fact]
    public void NewVoiceAndVideoModels_RouteToTheXProvider()
    {
        var provider = CreateProvider();

        provider.SupportsModel(new GrokTts()).Should().BeTrue();
        provider.SupportsModel(new GrokVoiceTranscribe20()).Should().BeTrue();
        provider.SupportsModel(new GrokImagineVideo15Lite { Resolution = GrokImagineVideo15Lite.ResolutionType.Resolution480p }).Should().BeTrue();
    }

    private void Respond(string body, string contentType) => Respond(Encoding.UTF8.GetBytes(body), contentType);

    private void Respond(byte[] body, string contentType)
    {
        _handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (request, _) =>
            {
                _request = request;
                _body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                var content = new ByteArrayContent(body);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });
    }

    private XProvider CreateProvider()
        => new(
            new HttpClient(_handler.Object) { BaseAddress = new Uri("https://api.x.ai") },
            Options.Create(new XOptions { ApiKey = "test-api-key" }),
            new Mock<ILogger<XProvider>>().Object);
}
