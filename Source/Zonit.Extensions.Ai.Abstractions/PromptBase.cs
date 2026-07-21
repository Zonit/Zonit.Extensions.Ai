using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Non-generic base class for typed prompts. Holds the raw <see cref="Prompt"/> template
/// (and any strongly-typed parameter properties declared by subclasses). Rendering of
/// Scriban templates happens in the application layer (<c>Zonit.Extensions.Ai</c>) and
/// is not performed by this base type.
/// </summary>
/// <remarks>
/// <para>
/// Domain layers can reference only <c>Zonit.Extensions.Ai.Abstractions</c> and define
/// prompts (data + raw template) without pulling in Scriban, HTTP clients, DI, or
/// provider implementations.
/// </para>
/// <para>
/// At application bootstrap, the <c>AiPromptBindingGenerator</c> source generator
/// (shipped with <c>Zonit.Extensions.Ai</c>) emits an AOT-safe binding for every concrete
/// <c>PromptBase</c> subclass visible in the consumer compilation; the application's
/// renderer uses those bindings for zero-reflection Scriban rendering. Domain prompts
/// that are only metadata-visible to the application fall back to reflection-based
/// property discovery at render time.
/// </para>
/// <para>
/// The <see cref="Text"/> getter intentionally returns the raw <see cref="Prompt"/> —
/// the application's renderer wraps prompts in an internal <c>RenderedPrompt</c> so
/// providers always observe an already-rendered <see cref="Text"/>.
/// </para>
/// </remarks>
public abstract class PromptBase : IPrompt
{
    /// <summary>
    /// Override to provide the raw prompt template. May contain Scriban placeholders
    /// (e.g. <c>{{ my_property }}</c>) that the application's renderer will substitute
    /// with the values of this prompt's public properties (snake_case mapping).
    /// </summary>
    public abstract string Prompt { get; }

    /// <summary>
    /// Returns the prompt text. Default implementation returns the raw <see cref="Prompt"/>
    /// without templating — the application layer renders into a wrapper before sending
    /// to providers.
    /// </summary>
    public virtual string Text => Prompt;

    /// <summary>
    /// Files attached to this prompt.
    /// Uses the Asset value object from Zonit.Extensions.
    /// </summary>
    public virtual IReadOnlyList<Asset>? Files { get; init; }
}

/// <inheritdoc cref="PromptBase"/>
public abstract class PromptBase<TResponse> : PromptBase, IPrompt<TResponse>
{
}

/// <summary>
/// Simple prompt for quick one-off usage without creating a class.
/// </summary>
/// <typeparam name="TResponse">The expected response type.</typeparam>
public sealed class SimplePrompt<TResponse> : IPrompt<TResponse>
{
    /// <summary>
    /// Creates a simple prompt with text.
    /// </summary>
    public SimplePrompt(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <inheritdoc />
    public string Text { get; }

    /// <inheritdoc />
    public IReadOnlyList<Asset>? Files { get; init; }
}

/// <summary>
/// Base class for typed/templated image generation prompts.
/// Inherit from this class to create image prompts with strongly-typed parameters.
/// For a one-off image prompt without a class, use <see cref="ImagePrompt"/>.
/// </summary>
/// <example>
/// <code>
/// public class ProductImagePrompt : ImagePromptBase
/// {
///     public required string ProductName { get; init; }
///     public required string Style { get; init; }
///
///     public override string Prompt =>
///         "Professional product photo of {{ product_name }} in {{ style }} style";
/// }
/// </code>
/// </example>
public abstract class ImagePromptBase : PromptBase<Asset>, IImagePrompt
{
}

/// <summary>
/// Base class for typed/templated video generation prompts.
/// Inherit from this class to create video prompts with strongly-typed parameters.
/// For a one-off video prompt without a class, use <see cref="VideoPrompt"/>.
/// </summary>
/// <example>
/// <code>
/// public class SceneVideoPrompt : VideoPromptBase
/// {
///     public required string Subject { get; init; }
///
///     public override string Prompt => "A cinematic shot of {{ subject }}";
/// }
/// </code>
/// </example>
public abstract class VideoPromptBase : PromptBase<Asset>, IVideoPrompt
{
}

/// <summary>
/// Ready-made image prompt — the standard, always-the-same shape for image generation.
/// Set <see cref="Text"/> (what to draw) and, optionally, <see cref="Image"/> as a
/// source image for image-to-image / edits.
/// </summary>
/// <example>
/// <code>
/// // text-to-image
/// var img = await ai.GenerateAsync(model, new ImagePrompt { Text = "a red bicycle" });
///
/// // image-to-image (edit / restyle a source image)
/// var edit = await ai.GenerateAsync(model, new ImagePrompt { Text = "make it snowy", Image = source });
/// </code>
/// </example>
public sealed class ImagePrompt : IImagePrompt
{
    /// <summary>What the image should depict (and, for edits, how to change the source).</summary>
    public required string Text { get; init; }

    /// <summary>Optional source image for image-to-image / edit workflows.</summary>
    public Asset? Image { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<Asset>? Files
        => Image is { HasValue: true } image ? [image] : null;
}

/// <summary>
/// Ready-made video prompt — the standard, always-the-same shape for video generation.
/// Set <see cref="Text"/> (what happens in the video) and, optionally, a source
/// <see cref="Image"/> (image-to-video starting frame) or <see cref="Video"/>
/// (video-to-video / edit). Whether a given model accepts an image or video source
/// is enforced centrally against the model's declared input channels.
/// </summary>
/// <example>
/// <code>
/// // text-to-video
/// var clip = await ai.GenerateAsync(model, new VideoPrompt { Text = "a butterfly over flowers" });
///
/// // image-to-video (animate a still)
/// var anim = await ai.GenerateAsync(model, new VideoPrompt { Text = "slow zoom in", Image = photo });
/// </code>
/// </example>
public sealed class VideoPrompt : IVideoPrompt
{
    /// <summary>What should happen in the video (and, for edits, how to change the source).</summary>
    public required string Text { get; init; }

    /// <summary>Optional source image — the starting frame for image-to-video.</summary>
    public Asset? Image { get; init; }

    /// <summary>Optional source video for video-to-video / edit workflows.</summary>
    public Asset? Video { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<Asset>? Files
    {
        get
        {
            List<Asset>? files = null;
            if (Image is { HasValue: true } image) (files ??= []).Add(image);
            if (Video is { HasValue: true } video) (files ??= []).Add(video);
            return files;
        }
    }
}

/// <summary>
/// Simple image prompt for quick one-off usage without creating a class.
/// Returns an Asset containing the generated image.
/// </summary>
/// <remarks>Prefer <see cref="ImagePrompt"/> (object-initializer syntax, optional source image).</remarks>
public sealed class SimpleImagePrompt : IImagePrompt
{
    /// <summary>
    /// Creates a simple image prompt with description.
    /// </summary>
    public SimpleImagePrompt(string description)
    {
        Text = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <inheritdoc />
    public string Text { get; }

    /// <inheritdoc />
    public IReadOnlyList<Asset>? Files { get; init; }
}
