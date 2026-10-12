using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Resolves the speed tier a request asks for (<see cref="IFast"/> / <see cref="IUltraFast"/>) and
/// reads back the tier the provider actually served, from the <c>service_tier</c>-style value it
/// echoes on the response.
/// </summary>
/// <remarks>
/// Fast mode is best-effort everywhere it exists: OpenAI and xAI both fall back to standard
/// scheduling when their fast capacity is exhausted, and only charge the premium when the
/// response confirms the tier. Billing has to follow the same rule, or a busy hour quietly
/// inflates every cost figure by 2× (6× on ultrafast).
/// </remarks>
public static class AiFastTier
{
    /// <summary>
    /// The speed tier this model asks for: <see cref="SpeedType.Standard"/> for a model without
    /// <see cref="IFast"/>, otherwise its <see cref="IFast.Speed"/>. Providers call this when
    /// building the request.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The model asks for <see cref="SpeedType.UltraFast"/> but does not implement
    /// <see cref="IUltraFast"/> — thrown before anything is sent, instead of silently running the
    /// request at another tier.
    /// </exception>
    public static SpeedType Requested(ILlm llm)
    {
        if (llm is not IFast fast) return SpeedType.Standard;

        if (fast.Speed == SpeedType.UltraFast && llm is not IUltraFast)
            throw new NotSupportedException(
                $"Model '{llm.Name}' does not offer the ultrafast tier. Use SpeedType.Fast, or a model " +
                "that implements IUltraFast (OpenAI Sol61, Astra6).");

        return fast.Speed;
    }

    /// <summary>
    /// The tier the provider served, from the value it echoed. Logs a warning when it differs
    /// from the requested tier.
    /// </summary>
    /// <param name="llm">The model used, to see which tier was requested.</param>
    /// <param name="echoedTier">
    /// The tier the provider reported (<c>service_tier</c> on OpenAI / xAI). <c>"ultrafast"</c>
    /// counts as ultrafast; <c>"fast"</c> and <c>"priority"</c> as fast — OpenAI renamed priority
    /// processing to fast mode and still echoes <c>priority</c> on GPT-5.6 and earlier, and
    /// <c>priority</c> is xAI's own spelling. Anything else is standard. <c>null</c> means the
    /// requested tier was served: a provider that reports no tier gives us nothing to contradict
    /// the request, and under-reporting cost is worse than over-reporting it.
    /// </param>
    /// <param name="logger">Logger for the downgrade warning.</param>
    /// <param name="provider">Provider name, for the log message.</param>
    /// <param name="operation">Calling operation, for the log message.</param>
    public static SpeedType Served(
        ILlm llm,
        string? echoedTier,
        ILogger logger,
        string provider,
        string operation)
    {
        var requested = RequestedOrStandard(llm);
        if (requested == SpeedType.Standard || echoedTier is null) return requested;

        var echoed = echoedTier switch
        {
            "ultrafast" => SpeedType.UltraFast,
            "fast" or "priority" => SpeedType.Fast,
            _ => SpeedType.Standard,
        };

        // Never bill above what was asked for, whatever the echo says.
        var served = echoed < requested ? echoed : requested;
        if (served != requested)
        {
            logger.LogWarning(
                "{Provider} {Operation} requested {Requested} on '{Model}' but the response was served on tier "
                + "'{Tier}' — the requested capacity was unavailable, so it is billed at the {Served} rate.",
                provider, operation, requested, llm.Name, echoedTier, served);
        }

        return served;
    }

    /// <summary>
    /// Returns whether the requested premium tier was served in full. Kept for providers that
    /// offer a single fast tier; <see cref="Served"/> also tells which tier a downgrade landed on.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the model did not ask for a premium tier (nothing to correct) or the tier
    /// was granted; <c>false</c> when the request was downgraded.
    /// </returns>
    public static bool WasGranted(
        ILlm llm,
        string? echoedTier,
        ILogger logger,
        string provider,
        string operation)
        => Served(llm, echoedTier, logger, provider, operation) == RequestedOrStandard(llm);

    /// <summary>
    /// Like <see cref="Requested"/> but never throws: an ultrafast request on a model without
    /// <see cref="IUltraFast"/> resolves to standard. For billing, which must not fail after the
    /// fact.
    /// </summary>
    internal static SpeedType RequestedOrStandard(ILlm llm) => llm switch
    {
        IUltraFast { Speed: SpeedType.UltraFast } => SpeedType.UltraFast,
        IFast { Speed: SpeedType.Fast } => SpeedType.Fast,
        _ => SpeedType.Standard,
    };
}
