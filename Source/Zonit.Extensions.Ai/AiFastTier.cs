using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Reads back whether a requested fast tier (<see cref="IFast"/> with
/// <see cref="SpeedType.Fast"/>) was actually served, from the <c>service_tier</c>-style value
/// the provider echoes on the response.
/// </summary>
/// <remarks>
/// Fast mode is best-effort everywhere it exists: OpenAI and xAI both fall back to standard
/// scheduling when their fast capacity is exhausted, and only charge the premium when the
/// response confirms the tier. Billing has to follow the same rule, or a busy hour quietly
/// inflates every cost figure by 2×.
/// </remarks>
public static class AiFastTier
{
    /// <summary>
    /// Returns whether the premium tier applies to this response, logging a warning when a
    /// request that asked for fast mode came back on another tier.
    /// </summary>
    /// <param name="llm">The model used, to see whether fast mode was requested at all.</param>
    /// <param name="echoedTier">
    /// The tier the provider reported (<c>service_tier</c> on OpenAI / xAI, or the equivalent).
    /// <c>"fast"</c> and <c>"priority"</c> both count as granted — OpenAI renamed priority
    /// processing to fast mode and still echoes <c>priority</c> on GPT-5.6 and earlier, and
    /// <c>priority</c> is xAI's own spelling. <c>null</c> counts as granted: a provider that
    /// reports no tier gives us nothing to contradict the request, and under-reporting cost is
    /// worse than over-reporting it.
    /// </param>
    /// <param name="logger">Logger for the downgrade warning.</param>
    /// <param name="provider">Provider name, for the log message.</param>
    /// <param name="operation">Calling operation, for the log message.</param>
    /// <returns>
    /// <c>true</c> when the model did not ask for fast mode (nothing to correct) or the tier was
    /// granted; <c>false</c> when the request was downgraded, so the caller bills it at the
    /// standard rate.
    /// </returns>
    public static bool WasGranted(
        ILlm llm,
        string? echoedTier,
        ILogger logger,
        string provider,
        string operation)
    {
        if (llm is not IFast { Speed: SpeedType.Fast }) return true;
        if (echoedTier is null or "fast" or "priority") return true;

        logger.LogWarning(
            "{Provider} {Operation} requested fast mode on '{Model}' but the response was served on tier "
            + "'{Tier}' — fast capacity was unavailable, so the request ran at standard speed and is billed "
            + "at the standard rate.",
            provider, operation, llm.Name, echoedTier);

        return false;
    }
}
