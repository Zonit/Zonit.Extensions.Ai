namespace Zonit.Extensions.Ai;

/// <summary>
/// Implemented by models that support a faster inference mode at premium pricing
/// (e.g. Anthropic Claude Opus 4.8 fast mode). When <see cref="Speed"/> is
/// <see cref="SpeedType.Fast"/>:
/// <list type="bullet">
///   <item><description>the provider opts the request into fast mode, and</description></item>
///   <item><description>cost calculation uses <see cref="FastPriceInput"/> /
///   <see cref="FastPriceOutput"/> instead of the standard
///   <see cref="ILlm.PriceInput"/> / <see cref="ILlm.PriceOutput"/>.</description></item>
/// </list>
/// The model wires this up by overriding <see cref="ILlm.GetInputPrice"/> /
/// <see cref="ILlm.GetOutputPrice"/> to branch on <see cref="Speed"/>, so callers
/// get the correct cost automatically just by flipping the flag.
/// </summary>
public interface IFast
{
    /// <summary>
    /// Selected inference speed. Defaults to <see cref="SpeedType.Standard"/>;
    /// set to <see cref="SpeedType.Fast"/> to opt the request into fast mode.
    /// </summary>
    SpeedType Speed { get; }

    /// <summary>Price per 1M input tokens in fast mode.</summary>
    decimal FastPriceInput { get; }

    /// <summary>Price per 1M output tokens in fast mode.</summary>
    decimal FastPriceOutput { get; }
}
