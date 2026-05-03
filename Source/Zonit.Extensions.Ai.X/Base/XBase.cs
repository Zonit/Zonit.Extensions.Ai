using Zonit.Extensions.Ai.X.Tools;

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Base class for all X/Grok models.
/// </summary>
public abstract class XBase : LlmBase, ILlm
{
    /// <summary>
    /// Provider-typed tool collection. Shadows <see cref="LlmBase.Tools"/>
    /// so the type system rejects cross-provider tool assignments at compile
    /// time — for example
    /// <c>new Grok43 { Tools = [new OpenAi.Tools.WebSearchTool()] }</c>
    /// will not compile.
    /// </summary>
    public new IXTool[]? Tools { get; init; }

    /// <inheritdoc />
    IToolBase[]? ILlm.Tools => Tools is null
        ? null
        : Array.ConvertAll(Tools, static t => (IToolBase)t);
}
