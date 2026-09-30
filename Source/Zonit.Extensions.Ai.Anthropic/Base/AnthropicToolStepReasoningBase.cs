namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// A reasoning choice for models that offer <b>tool-step reasoning</b> next to regular
/// adaptive thinking: either a <typeparamref name="TReason"/> level (think before
/// answering and between tool calls) or a <typeparamref name="TToolStepReason"/> level
/// (skip up-front thinking, reason only between tool calls). Assign either enum
/// directly — the implicit conversions pick the mode:
/// <code>
/// new Sonnet55 { Reason = Sonnet55.ReasonType.Max }            // adaptive thinking
/// new Sonnet55 { Reason = Sonnet55.ToolStepReasonType.Medium } // tool-step reasoning
/// </code>
/// </summary>
/// <remarks>
/// One property with two enums rather than two properties: two properties could both
/// be set, and some combinations are rejected by the API (tool-step reasoning is only
/// accepted at <c>low</c> / <c>medium</c> / <c>high</c>). Each enum lists only the
/// levels its mode accepts, so an invalid combination does not compile.
/// </remarks>
public readonly struct AnthropicReason<TReason, TToolStepReason> : IEquatable<AnthropicReason<TReason, TToolStepReason>>
    where TReason : struct, Enum
    where TToolStepReason : struct, Enum
{
    private AnthropicReason(ReasoningEffort effort, bool toolStepsOnly)
    {
        Effort = effort;
        ToolStepsOnly = toolStepsOnly;
    }

    /// <summary>Selected effort, mapped onto the global <see cref="ReasoningEffort"/>.</summary>
    public ReasoningEffort Effort { get; }

    /// <summary>
    /// <c>true</c> for tool-step reasoning (wire <c>thinking.type = "between_tools"</c>),
    /// <c>false</c> for regular adaptive thinking.
    /// </summary>
    public bool ToolStepsOnly { get; }

    /// <summary>Regular adaptive thinking at <paramref name="level"/>.</summary>
    public static implicit operator AnthropicReason<TReason, TToolStepReason>(TReason level)
        => new((ReasoningEffort)System.Convert.ToInt32(level), toolStepsOnly: false);

    /// <summary>Tool-step reasoning at <paramref name="level"/>.</summary>
    public static implicit operator AnthropicReason<TReason, TToolStepReason>(TToolStepReason level)
        => new((ReasoningEffort)System.Convert.ToInt32(level), toolStepsOnly: true);

    /// <inheritdoc />
    public bool Equals(AnthropicReason<TReason, TToolStepReason> other)
        => Effort == other.Effort && ToolStepsOnly == other.ToolStepsOnly;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AnthropicReason<TReason, TToolStepReason> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Effort, ToolStepsOnly);

    /// <inheritdoc />
    public override string ToString() => ToolStepsOnly ? $"ToolStep:{Effort}" : Effort.ToString();

    /// <summary>Equality.</summary>
    public static bool operator ==(AnthropicReason<TReason, TToolStepReason> left, AnthropicReason<TReason, TToolStepReason> right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(AnthropicReason<TReason, TToolStepReason> left, AnthropicReason<TReason, TToolStepReason> right) => !left.Equals(right);
}

/// <summary>
/// Base class for Claude models that support <b>tool-step reasoning</b> (Anthropic
/// <c>thinking.type = "between_tools"</c>): the model skips up-front thinking and
/// reasons only after tool results arrive. The cheapest thinking mode these models
/// offer — they cannot switch thinking off — and the natural choice for price-sensitive
/// tool loops. See <see cref="AnthropicReason{TReason, TToolStepReason}"/> for how the
/// mode is selected.
/// </summary>
/// <typeparam name="TReason">
/// Adaptive-thinking levels the model accepts (numeric values aligned with
/// <see cref="ReasoningEffort"/>).
/// </typeparam>
/// <typeparam name="TToolStepReason">
/// Tool-step levels the model accepts — only <c>Low = 1</c>, <c>Medium = 2</c>,
/// <c>High = 3</c>; the API rejects tool-step reasoning at <c>xhigh</c> / <c>max</c>.
/// </typeparam>
/// <remarks>
/// These models also reject non-default sampling parameters, so, as on
/// <see cref="AnthropicFixedSamplingBase{TReason}"/>, <see cref="Temperature"/> and
/// <see cref="TopP"/> do not compile.
/// </remarks>
public abstract class AnthropicToolStepReasoningBase<TReason, TToolStepReason> : AnthropicAdaptiveBase
    where TReason : struct, Enum
    where TToolStepReason : struct, Enum
{
    /// <summary>
    /// Reasoning mode and level. Assign a <typeparamref name="TReason"/> value for
    /// adaptive thinking or a <typeparamref name="TToolStepReason"/> value for tool-step
    /// reasoning. <c>null</c> omits the <c>thinking</c> field, so the model thinks
    /// adaptively at its server default.
    /// </summary>
    public AnthropicReason<TReason, TToolStepReason>? Reason { get; init; }

    /// <inheritdoc />
    protected override ReasoningEffort? GetReasonEffort() => Reason?.Effort;

    /// <inheritdoc />
    protected internal override bool ReasonsAtToolStepsOnly => Reason is { ToolStepsOnly: true };

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
