using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Internal <see cref="IPrompt{TResponse}"/> wrapper produced by the application's
/// facade after rendering a <see cref="PromptBase"/> through <see cref="IPromptRenderer"/>.
/// Providers observe the already-rendered <c>Text</c>; raw templates never reach the
/// provider layer.
/// </summary>
internal sealed class RenderedPrompt<TResponse> : IPrompt<TResponse>
{
    public RenderedPrompt(string text, IReadOnlyList<Asset>? files)
    {
        Text = text ?? string.Empty;
        Files = files;
    }

    public string Text { get; }
    public IReadOnlyList<Asset>? Files { get; }
}

/// <summary>
/// Non-generic counterpart of <see cref="RenderedPrompt{TResponse}"/> — used where
/// the call site only has an <see cref="IPrompt"/> reference (e.g. streaming).
/// </summary>
internal sealed class RenderedPrompt : IPrompt
{
    public RenderedPrompt(string text, IReadOnlyList<Asset>? files)
    {
        Text = text ?? string.Empty;
        Files = files;
    }

    public string Text { get; }
    public IReadOnlyList<Asset>? Files { get; }
}
