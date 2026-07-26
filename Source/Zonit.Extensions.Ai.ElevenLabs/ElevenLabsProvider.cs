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
        SpeechPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        var (voiceId, jsonPayload, text) = PrepareRequest(llm, prompt);

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

    /// <inheritdoc />
    public async Task<Result<SpeechTimestamps>> GenerateSpeechWithTimestampsAsync(
        ISpeechLlm llm,
        SpeechPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        var (voiceId, jsonPayload, text) = PrepareRequest(llm, prompt);

        // Same request body; the /with-timestamps endpoint returns JSON (audio_base64 + alignment).
        var url = $"/v1/text-to-speech/{Uri.EscapeDataString(voiceId)}/with-timestamps?output_format={llm.FormatValue}";
        _logger.LogDebug("ElevenLabs TTS+timestamps request to {Url} ({Chars} chars)", url, text.Length);

        var stopwatch = Stopwatch.StartNew();

        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, cancellationToken);

        stopwatch.Stop();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ElevenLabs TTS+timestamps error: {Status} - {Response}", response.StatusCode, responseJson);
            throw new HttpRequestException($"ElevenLabs API failed: {response.StatusCode}: {responseJson}");
        }

        var payload = JsonSerializer.Deserialize(responseJson, ElevenLabsJsonContext.Default.ElevenTtsTimestampsResponse)
            ?? throw new InvalidOperationException("ElevenLabs returned no timestamps payload.");

        if (string.IsNullOrEmpty(payload.AudioBase64))
            throw new InvalidOperationException("ElevenLabs returned an empty audio payload.");

        var audioBytes = Convert.FromBase64String(payload.AudioBase64);
        Asset audio = new(audioBytes, $"speech.{FileExtensionFor(llm.FormatValue)}");

        var characters = BuildCharacterTimings(payload.Alignment);

        return new Result<SpeechTimestamps>
        {
            Value = new SpeechTimestamps { Audio = audio, Characters = characters },
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "Speech",
                Duration = stopwatch.Elapsed,
                Usage = new TokenUsage { OutputCost = llm.GetSpeechGenerationPrice(text.Length) }
            }
        };
    }

    /// <summary>Maps ElevenLabs character-alignment arrays into ordered <see cref="CharacterTiming"/> values.</summary>
    private static IReadOnlyList<CharacterTiming> BuildCharacterTimings(ElevenAlignment? alignment)
    {
        var chars = alignment?.Characters;
        var starts = alignment?.CharacterStartTimesSeconds;
        var ends = alignment?.CharacterEndTimesSeconds;
        if (chars is null || starts is null || ends is null)
            return [];

        var count = Math.Min(chars.Count, Math.Min(starts.Count, ends.Count));
        var result = new CharacterTiming[count];
        for (var i = 0; i < count; i++)
            result[i] = new CharacterTiming(chars[i], TimeSpan.FromSeconds(starts[i]), TimeSpan.FromSeconds(ends[i]));
        return result;
    }

    /// <summary>
    /// Validates the request and builds the shared TTS payload (used by both the plain and the
    /// with-timestamps endpoints). Returns the voice id, serialized JSON body, and the text.
    /// </summary>
    private static (string VoiceId, string JsonPayload, string Text) PrepareRequest(ISpeechLlm llm, SpeechPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(prompt);

        var text = prompt.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Speech synthesis requires non-empty text.", nameof(prompt));

        var voiceId = llm.VoiceValue;
        if (string.IsNullOrWhiteSpace(voiceId))
            throw new InvalidOperationException(
                $"Model '{llm.Name}' has no voice set. Assign a voice id via the model's Voice property " +
                $"(e.g. ElevenVoices.Rachel or a cloned-voice id).");

        if (llm is ElevenLabsSpeechBase model && text.Length > model.MaxCharacters)
            throw new ArgumentException(
                $"Text is {text.Length} characters but model '{llm.Name}' accepts at most {model.MaxCharacters} per request. " +
                $"Split the text into smaller chunks.", nameof(prompt));

        var settings = llm as ElevenLabsSpeechBase;
        var stitching = settings?.SupportsRequestStitching ?? true;

        if (settings?.Seed is { } seed && (seed < 0 || seed > ElevenLabsSpeechBase.MaxSeed))
            throw new ArgumentException(
                $"Seed must be between 0 and {ElevenLabsSpeechBase.MaxSeed} (got {seed}).", nameof(llm));

        var request = new ElevenTtsRequest
        {
            Text = text,
            ModelId = llm.Name,
            // Surrounding lines are context for prosody continuity — not spoken, not billed.
            // Omitted when null, and omitted entirely on models without request stitching (v3),
            // which ignore these fields.
            PreviousText = stitching && !string.IsNullOrEmpty(prompt.PreviousText) ? prompt.PreviousText : null,
            NextText = stitching && !string.IsNullOrEmpty(prompt.NextText) ? prompt.NextText : null,
            // Best-effort deterministic sampling; ElevenLabs does not return the seed.
            Seed = settings?.Seed,
            // Force the spoken language when set; omitted (auto-detect) otherwise.
            LanguageCode = string.IsNullOrWhiteSpace(settings?.LanguageCode) ? null : settings!.LanguageCode,
            // Only send when the caller overrode the API default (Auto), to avoid 400s on
            // models that don't accept every mode (e.g. Flash/Turbo reject "on").
            ApplyTextNormalization = settings is { TextNormalization: not TextNormalizationMode.Auto } tn
                ? tn.TextNormalization.GetEnumValue()
                : null,
            VoiceSettings = new ElevenVoiceSettings
            {
                Stability = settings?.Stability ?? 0.5,
                SimilarityBoost = settings?.SimilarityBoost ?? 0.75,
                Style = settings?.Style ?? 0.0,
                UseSpeakerBoost = settings?.UseSpeakerBoost ?? true,
                Speed = settings?.Speed ?? 1.0,
            }
        };

        var jsonPayload = JsonSerializer.Serialize(request, ElevenLabsJsonContext.Default.ElevenTtsRequest);
        return (voiceId, jsonPayload, text);
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

    /// <summary>Text spoken just before <see cref="Text"/> — context only (ElevenLabs <c>previous_text</c>).</summary>
    public string? PreviousText { get; set; }

    /// <summary>Text spoken just after <see cref="Text"/> — context only (ElevenLabs <c>next_text</c>).</summary>
    public string? NextText { get; set; }

    /// <summary>Best-effort deterministic sampling seed (0..4294967295); ElevenLabs <c>seed</c>.</summary>
    public long? Seed { get; set; }

    /// <summary>Optional ISO language code to enforce the spoken language; ElevenLabs <c>language_code</c>.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>Text normalization mode ("auto"/"on"/"off"); ElevenLabs <c>apply_text_normalization</c>.</summary>
    public string? ApplyTextNormalization { get; set; }

    public ElevenVoiceSettings? VoiceSettings { get; set; }
}

internal sealed class ElevenVoiceSettings
{
    public double Stability { get; set; }
    public double SimilarityBoost { get; set; }
    public double Style { get; set; }
    public bool UseSpeakerBoost { get; set; }
    public double Speed { get; set; }
}

// Response DTOs for the /with-timestamps endpoint.
internal sealed class ElevenTtsTimestampsResponse
{
    public string? AudioBase64 { get; set; }
    public ElevenAlignment? Alignment { get; set; }
}

internal sealed class ElevenAlignment
{
    public List<string>? Characters { get; set; }
    public List<double>? CharacterStartTimesSeconds { get; set; }
    public List<double>? CharacterEndTimesSeconds { get; set; }
}
