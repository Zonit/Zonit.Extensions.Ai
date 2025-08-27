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

    ToolsType SupportedTools { get; }
    FeaturesType SupportedFeatures { get; } 
    EndpointsType SupportedEndpoints { get; }

    /// <summary>
    /// Oblicza cenę za tokeny input na podstawie liczby tokenów (może być różna w zależności od wielkości kontekstu)
    /// </summary>
    /// <param name="tokenCount">Liczba tokenów input</param>
    /// <returns>Cena za 1M tokenów</returns>
    decimal GetInputPrice(long tokenCount);

    /// <summary>
    /// Oblicza cenę za tokeny output na podstawie liczby tokenów
    /// </summary>
    /// <param name="tokenCount">Liczba tokenów output</param>
    /// <returns>Cena za 1M tokenów</returns>
    decimal GetOutputPrice(long tokenCount);

    IToolBase[]? Tools { get; init; }
}
