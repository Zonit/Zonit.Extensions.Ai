namespace Zonit.Extensions.Ai.Anthropic.Tools;

/// <summary>
/// Marker interface for tools that the Anthropic Messages API can consume.
/// Provider-specific so the type system rejects cross-provider tool usage at
/// compile time — for example
/// <c>new Sonnet46 { Tools = [new OpenAi.Tools.WebSearchTool()] }</c>
/// will not compile because OpenAI's tool does not implement
/// <see cref="IAnthropicTool"/>.
/// </summary>
public interface IAnthropicTool : IToolBase
{
}
