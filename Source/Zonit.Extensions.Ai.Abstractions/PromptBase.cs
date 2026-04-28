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
/// Base class for image generation prompts.
/// Inherit from this class to create typed image prompts with parameters.
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
public abstract class ImagePromptBase : PromptBase<Asset>
{
}

/// <summary>
/// Simple image prompt for quick one-off usage without creating a class.
/// Returns an Asset containing the generated image.
/// </summary>
public sealed class SimpleImagePrompt : IPrompt<Asset>
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
