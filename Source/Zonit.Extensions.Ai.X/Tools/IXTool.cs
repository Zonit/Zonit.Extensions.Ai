namespace Zonit.Extensions.Ai.X.Tools;

/// <summary>
/// Marker interface for tools that the xAI Responses API can consume.
/// Provider-specific so the type system rejects cross-provider tool usage at
/// compile time — for example
/// <c>new Grok43 { Tools = [new OpenAi.Tools.WebSearchTool()] }</c>
/// will not compile because OpenAI's tool does not implement
/// <see cref="IXTool"/>.
/// </summary>
public interface IXTool : IToolBase
{
}
