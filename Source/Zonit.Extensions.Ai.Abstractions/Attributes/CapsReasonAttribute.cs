namespace Zonit.Extensions.Ai;

/// <summary>
/// Marks a <see cref="bool"/> model switch that, when <c>true</c>, limits the model's
/// <c>Reason</c> level to <see cref="MaxEffort"/> — the provider rejects higher levels
/// with HTTP 400 in that mode.
/// </summary>
/// <remarks>
/// The bundled analyzer (<c>ZAI001</c>) turns the violation into a <b>compile error</b> in
/// object initializers, e.g.
/// <c>new Sonnet55 { ToolStepReasoning = true, Reason = Sonnet55.ReasonType.Max }</c>.
/// Values the compiler cannot see (set at runtime) are checked by the provider before the
/// request is sent.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class CapsReasonAttribute : Attribute
{
    /// <summary>Creates the attribute.</summary>
    /// <param name="maxEffort">Highest <c>Reason</c> level allowed while the switch is on.</param>
    public CapsReasonAttribute(ReasoningEffort maxEffort) => MaxEffort = maxEffort;

    /// <summary>Highest <c>Reason</c> level allowed while the switch is on.</summary>
    public ReasoningEffort MaxEffort { get; }
}
