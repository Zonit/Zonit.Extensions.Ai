using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Marker for internal <see cref="IPrompt"/> wrappers that carry the original
/// prompt's CLR type name. Used by the agent runner / metadata pipeline to
/// surface the user-facing prompt name (e.g. <c>BriefPrompt</c>) instead of the
/// wrapper's own type (<c>RenderedPrompt`1</c>) on
/// <see cref="MetaData.PromptName"/>.
/// </summary>
internal interface IRenderedPrompt
{
    /// <summary>
    /// Original prompt CLR type name as returned by <c>Type.Name</c> at the
    /// point of wrapping (e.g. <c>"BriefPrompt"</c>). <c>null</c> only if the
    /// caller passed a non-typed prompt before wrapping.
    /// </summary>
    string? OriginalPromptTypeName { get; }
}

/// <summary>
/// Internal <see cref="IPrompt{TResponse}"/> wrapper produced by the application's
/// facade after rendering a <see cref="PromptBase"/> through <see cref="IPromptRenderer"/>.
/// Providers observe the already-rendered <c>Text</c>; raw templates never reach the
/// provider layer.
/// </summary>
internal sealed class RenderedPrompt<TResponse> : IPrompt<TResponse>, IRenderedPrompt
{
    public RenderedPrompt(string text, IReadOnlyList<Asset>? files, string? originalPromptTypeName = null)
    {
        Text = text ?? string.Empty;
        Files = files;
        OriginalPromptTypeName = originalPromptTypeName;
    }

    public string Text { get; }
    public IReadOnlyList<Asset>? Files { get; }
    public string? OriginalPromptTypeName { get; }
}

/// <summary>
/// Non-generic counterpart of <see cref="RenderedPrompt{TResponse}"/> — used where
/// the call site only has an <see cref="IPrompt"/> reference (e.g. streaming).
/// </summary>
internal sealed class RenderedPrompt : IPrompt, IRenderedPrompt
{
    public RenderedPrompt(string text, IReadOnlyList<Asset>? files, string? originalPromptTypeName = null)
    {
        Text = text ?? string.Empty;
        Files = files;
        OriginalPromptTypeName = originalPromptTypeName;
    }

    public string Text { get; }
    public IReadOnlyList<Asset>? Files { get; }
    public string? OriginalPromptTypeName { get; }
}
