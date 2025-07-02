namespace Zonit.Extensions.Ai.Llm;

public interface ILlmBase
{
    int MaxTokens { get; init; }

    string Name { get; }

    decimal PriceInput { get; }
    decimal PriceOutput { get; }

    decimal? BatchPriceInput { get; }
    decimal? BatchPriceOutput { get; }

    int MaxInputTokens { get; }
    int MaxOutputTokens { get; }

    ChannelType Input { get; }
    ChannelType Output { get; }

    ToolsType Tools { get; }
    EndpointsType Endpoints { get; }
}
