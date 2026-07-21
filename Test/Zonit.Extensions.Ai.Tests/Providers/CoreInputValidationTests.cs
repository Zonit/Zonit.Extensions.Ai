using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Verifies the provider-agnostic input validation that lives in core
/// (<c>AiProvider</c>): an attached file whose modality the model does not declare
/// in <see cref="ILlm.Input"/> is rejected before any provider is invoked, and a
/// generation call with neither prompt text nor a source file is rejected too.
/// Because this is enforced in core, it holds for every provider and modality.
/// </summary>
public class CoreInputValidationTests
{
    private static readonly byte[] FakeMp4 = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70];
    private static readonly byte[] FakePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task Video_model_without_Video_input_rejects_a_video_file()
    {
        var ai = BuildAi();
        // grok-imagine-video-1.5 declares Input = Text | Image (no Video).
        var model = new GrokImagineVideo15 { Resolution = GrokImagineVideo15.ResolutionType.Resolution720p };
        var prompt = new VideoPrompt
        {
            Text = "make it move",
            Video = new Asset(FakeMp4, "clip.mp4", Asset.MimeType.VideoMp4)
        };

        var act = () => ai.GenerateAsync(model, prompt, CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*does not accept video input*");
    }

    [Fact]
    public async Task Video_model_with_Video_input_accepts_a_video_file()
    {
        var ai = BuildAi();
        // grok-imagine-video declares Input = Text | Image | Video.
        var model = new GrokImagineVideo { Resolution = GrokImagineVideo.ResolutionType.Resolution720p };
        var prompt = new VideoPrompt
        {
            Text = "restyle this clip",
            Video = new Asset(FakeMp4, "clip.mp4", Asset.MimeType.VideoMp4)
        };

        var result = await ai.GenerateAsync(model, prompt, CancellationToken.None);

        result.Value.HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task Video_model_accepts_an_image_file_for_image_to_video()
    {
        var ai = BuildAi();
        var model = new GrokImagineVideo15 { Resolution = GrokImagineVideo15.ResolutionType.Resolution720p };
        var prompt = new VideoPrompt
        {
            Text = "animate this photo",
            Image = new Asset(FakePng, "photo.png", Asset.MimeType.ImagePng)
        };

        var result = await ai.GenerateAsync(model, prompt, CancellationToken.None);

        result.Value.HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task Generation_with_no_text_and_no_files_is_rejected()
    {
        var ai = BuildAi();
        var model = new GrokImagineVideo15 { Resolution = GrokImagineVideo15.ResolutionType.Resolution720p };
        var prompt = new VideoPrompt { Text = "   " };

        var act = () => ai.GenerateAsync(model, prompt, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*needs a text prompt or a source file*");
    }

    private static IAiProvider BuildAi()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAi();
        services.AddSingleton<IModelProvider, CannedMediaProvider>();
        return services.BuildServiceProvider().GetRequiredService<IAiProvider>();
    }

    /// <summary>
    /// Stand-in provider that claims every image/video model and returns a canned
    /// asset. It never validates input itself — that is core's job — so if a call
    /// reaches here the input passed validation.
    /// </summary>
    private sealed class CannedMediaProvider : IModelProvider
    {
        public string Name => "canned";
        public bool SupportsModel(ILlm llm) => llm is IImageLlm or IVideoLlm;

        private Result<Asset> Canned(ILlm llm) => new()
        {
            Value = new Asset([0x01, 0x02, 0x03], "out.mp4", Asset.MimeType.VideoMp4),
            MetaData = new MetaData
            {
                Model = llm,
                Provider = Name,
                PromptName = "canned",
                Usage = new TokenUsage()
            }
        };

        public Task<Result<Asset>> GenerateVideoAsync(IVideoLlm llm, IPrompt<Asset> prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(Canned(llm));
        public Task<Result<Asset>> GenerateImageAsync(IImageLlm llm, IPrompt<Asset> prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(Canned(llm));

        public Task<Result<TResponse>> GenerateAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
            ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Result<float[]>> EmbedAsync(IEmbeddingLlm llm, string input, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<string> StreamAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResponse>(
            ILlm llm, IPrompt<TResponse> prompt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Result<string>> TranscribeAsync(IAudioLlm llm, Asset audioFile, string? language = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
