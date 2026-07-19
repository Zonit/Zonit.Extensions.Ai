using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.ElevenLabs;

/// <summary>
/// ElevenLabs provider implementation. Supports text-to-speech (<see cref="ISpeechLlm"/>);
/// the other modalities are not offered by ElevenLabs and throw <see cref="NotSupportedException"/>.
/// </summary>
[AiProvider("elevenlabs")]
public sealed class ElevenLabsProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ElevenLabsProvider> _logger;
    private readonly ElevenLabsOptions _options;

    public ElevenLabsProvider(
        HttpClient httpClient,
        IOptions<ElevenLabsOptions> options,
        ILogger<ElevenLabsProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        ConfigureHttpClient();
    }

    /// <inheritdoc />
    public string Name => "ElevenLabs";

    /// <inheritdoc />
    public bool SupportsModel(ILlm llm) => llm is ElevenLabsSpeechBase;

    /// <inheritdoc />
    public async Task<Result<Asset>> GenerateSpeechAsync(
        ISpeechLlm llm,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(llm);
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Speech synthesis requires non-empty text.", nameof(text));

        var voiceId = llm.VoiceValue;
        if (string.IsNullOrWhiteSpace(voiceId))
            throw new InvalidOperationException(
                $"Model '{llm.Name}' has no voice set. Assign a voice id via the model's Voice property " +
                $"(e.g. ElevenVoices.Rachel or a cloned-voice id).");

        if (llm is ElevenLabsSpeechBase model && text.Length > model.MaxCharacters)
            throw new ArgumentException(
                $"Text is {text.Length} characters but model '{llm.Name}' accepts at most {model.MaxCharacters} per request. " +
                $"Split the text into smaller chunks.", nameof(text));

        var settings = llm as ElevenLabsSpeechBase;
        var request = new ElevenTtsRequest
        {
            Text = text,
            ModelId = llm.Name,
            VoiceSettings = new ElevenVoiceSettings
            {
                Stability = settings?.Stability ?? 0.5,
                SimilarityBoost = settings?.SimilarityBoost ?? 0.75,
                Style = settings?.Style ?? 0.0,
                UseSpeakerBoost = settings?.UseSpeakerBoost ?? true,
            }
        };

        var jsonPayload = JsonSerializer.Serialize(request, ElevenLabsJsonContext.Default.ElevenTtsRequest);

        var url = $"/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}?output_format={llm.FormatValue}";
        _logger.LogDebug("ElevenLabs TTS request to {Url} ({Chars} chars)", url, text.Length);

        var stopwatch = Stopwatch.StartNew();

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);

        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ElevenLabs TTS error: {Status} - {Response}", response.StatusCode, errorBody);
            throw new HttpRequestException($"ElevenLabs API failed: {response.StatusCode}: {errorBody}");
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (audioBytes.Length == 0)
            throw new InvalidOperationException("ElevenLabs returned an empty audio payload.");

        var fileName = $"speech.{FileExtensionFor(llm.FormatValue)}";
        Asset audio = new(audioBytes, fileName);

        var cost = llm.GetSpeechGenerationPrice(text.Length);

        return new Result<Asset>
        {
            Value = audio,
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "Speech",
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage
                {
                    OutputCost = cost
                }
            }
        };
    }

    /// <summary>
    /// Maps an ElevenLabs <c>output_format</c> wire value to a file extension for the returned asset.
    /// </summary>
    private static string FileExtensionFor(string format)
    {
        if (format.StartsWith("mp3", StringComparison.Ordinal)) return "mp3";
        if (format.StartsWith("pcm", StringComparison.Ordinal)) return "pcm";
        if (format.StartsWith("ulaw", StringComparison.Ordinal)) return "ulaw";
        if (format.StartsWith("opus", StringComparison.Ordinal)) return "opus";
        return "audio";
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _options.BaseUrl ?? "https://api.elevenlabs.io";
        _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Add("xi-api-key", _options.ApiKey);
    }

    // --- Modalities ElevenLabs does not provide ---

    /// <inheritdoc />
    public Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("ElevenLabs does not support text generation.");

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateImageAsync(
        IImageLlm llm, IPrompt<Asset> prompt, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("ElevenLabs does not support image generation.");

    /// <inheritdoc />
    public Task<Result<Asset>> GenerateVideoAsync(
        IVideoLlm llm, IPrompt<Asset> prompt, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("ElevenLabs does not support video generation.");

    /// <inheritdoc />
    public Task<Result<float[]>> EmbedAsync(
        IEmbeddingLlm llm, string input, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("ElevenLabs does not support embeddings.");

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
        ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("ElevenLabs does not support text streaming.");

    /// <inheritdoc />
    public Task<Result<string>> TranscribeAsync(
        IAudioLlm llm, Asset audioFile, string? language = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("ElevenLabs transcription (Scribe) is not wired in this package yet.");
}

// Request DTOs (AOT-safe; snake_case via ElevenLabsJsonContext).
internal sealed class ElevenTtsRequest
{
    public string Text { get; set; } = "";
    public string ModelId { get; set; } = "";
    public ElevenVoiceSettings? VoiceSettings { get; set; }
}

internal sealed class ElevenVoiceSettings
{
    public double Stability { get; set; }
    public double SimilarityBoost { get; set; }
    public double Style { get; set; }
    public bool UseSpeakerBoost { get; set; }
}
