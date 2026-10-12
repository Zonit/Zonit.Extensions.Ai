namespace Zonit.Extensions.Ai;

/// <summary>
/// Inference speed tier for models that expose a faster mode at premium pricing
/// (see <see cref="IFast"/>).
/// </summary>
public enum SpeedType
{
    /// <summary>
    /// Standard speed and pricing. The default for every model.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Fast mode — higher output tokens per second at premium pricing. Same
    /// model weights and behaviour, only faster. Supported by a small set of
    /// models (e.g. Anthropic Claude Opus 4.8). Selecting it on a model that
    /// does not implement <see cref="IFast"/> has no effect.
    /// </summary>
    Fast = 1,

    /// <summary>
    /// Ultrafast mode — a tier above <see cref="Fast"/>, with its own capacity and rate limits, at
    /// a much higher premium (OpenAI <c>service_tier: "ultrafast"</c>, 6× the standard rate). Only
    /// models that implement <see cref="IUltraFast"/> offer it; selecting it on any other model is
    /// rejected with <see cref="NotSupportedException"/> before the request is sent.
    /// </summary>
    UltraFast = 2,
}
