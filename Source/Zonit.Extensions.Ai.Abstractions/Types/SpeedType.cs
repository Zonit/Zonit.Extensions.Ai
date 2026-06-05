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
}
