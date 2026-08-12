namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Translates <see cref="ReasoningEffort"/> to the string xAI expects in
/// <c>reasoning.effort</c>.
/// </summary>
/// <remarks>
/// Every level except <see cref="ReasoningEffort.Extra"/> is just the enum name
/// lowercased. <c>Extra</c> is the exception: xAI spells that level
/// <c>"xhigh"</c>, so a naive <c>ToString().ToLowerInvariant()</c> would send
/// <c>"extra"</c> and take an HTTP 400. Grok 4.6 is the first Grok model to
/// accept the level at all.
/// </remarks>
internal static class XEffortWire
{
    /// <summary>
    /// Maps <paramref name="effort"/> to its xAI wire value.
    /// </summary>
    public static string Map(ReasoningEffort effort) => effort switch
    {
        ReasoningEffort.Extra => "xhigh",
        _ => effort.ToString().ToLowerInvariant(),
    };
}
