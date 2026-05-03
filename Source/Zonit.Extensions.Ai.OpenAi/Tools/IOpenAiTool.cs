namespace Zonit.Extensions.Ai.OpenAi.Tools;

/// <summary>
/// Marker interface for tools that the OpenAI Responses API can consume.
/// Every provider has its own marker so the type system rejects cross-provider
/// tool usage at compile time — for example
/// <c>new GPT5 { Tools = [new Anthropic.Tools.WebSearchTool()] }</c>
/// will not compile because Anthropic's tool does not implement
/// <see cref="IOpenAiTool"/>.
/// </summary>
public interface IOpenAiTool : IToolBase
{
}
