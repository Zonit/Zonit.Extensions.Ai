namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Base class for adaptive-thinking Claude models that reject non-default sampling
/// parameters (Opus 4.7 and later, Sonnet 5 and later, Fable, Mythos): any
/// <c>temperature</c>, <c>top_p</c> or <c>top_k</c> other than the default is an HTTP 400.
/// </summary>
/// <remarks>
/// <see cref="Temperature"/> and <see cref="TopP"/> are hidden behind an
/// <see cref="ObsoleteAttribute"/> with <c>error: true</c>, so
/// <c>new Opus55 { Temperature = 0.2 }</c> fails to compile instead of failing at
/// runtime. The provider also never sends them for these models (see
/// <see cref="AnthropicBase.SupportsSamplingParameters"/>), so a value set through the
/// <see cref="ITextLlm"/> interface is ignored rather than rejected.
/// </remarks>
public abstract class AnthropicFixedSamplingBase<TReason> : AnthropicReasoningBase<TReason>
    where TReason : struct, Enum
{
    /// <summary>Not supported — this model rejects a non-default <c>temperature</c>.</summary>
    [Obsolete(AnthropicSampling.RejectedMessage, error: true)]
    public new double Temperature
    {
        get => base.Temperature;
        set => base.Temperature = value;
    }

    /// <summary>Not supported — this model rejects a non-default <c>top_p</c>.</summary>
    [Obsolete(AnthropicSampling.RejectedMessage, error: true)]
    public new double TopP
    {
        get => base.TopP;
        set => base.TopP = value;
    }

    /// <inheritdoc />
    protected internal override bool SupportsSamplingParameters => false;
}

/// <summary>Shared wording for the sampling-parameter compile error.</summary>
internal static class AnthropicSampling
{
    internal const string RejectedMessage =
        "This Claude model rejects non-default temperature / top_p / top_k with HTTP 400 " +
        "(Opus 4.7+, Sonnet 5+, Fable, Mythos). Steer output through the prompt or Reason instead.";
}
