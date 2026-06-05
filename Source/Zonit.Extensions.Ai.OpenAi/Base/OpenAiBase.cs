using Zonit.Extensions.Ai.OpenAi.Tools;

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// Base class for all OpenAI models.
/// </summary>
public abstract class OpenAiBase : LlmBase, ILlm
{
    /// <summary>
    /// Whether to store logs for this model usage.
    /// </summary>
    public bool StoreLogs { get; set; } = false;

    /// <summary>
    /// Provider-typed tool collection. The shadowing on <see cref="LlmBase.Tools"/>
    /// makes the type system reject assignments of foreign-provider tools at
    /// compile time — for example
    /// <c>new GPT5 { Tools = [new Anthropic.Tools.WebSearchTool()] }</c>
    /// will not compile.
    /// </summary>
    public new IOpenAiTool[]? Tools { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Bridges the typed <see cref="Tools"/> back to the abstract
    /// <see cref="IToolBase"/> surface consumed by provider-agnostic
    /// pipelines (logging, telemetry).
    /// </remarks>
    IToolBase[]? ILlm.Tools => Tools is null
        ? null
        : Array.ConvertAll(Tools, static t => (IToolBase)t);
}
