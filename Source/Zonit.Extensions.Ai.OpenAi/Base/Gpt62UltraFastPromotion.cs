namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// OpenAI's launch promotion on the ultrafast tier of the GPT-6.2 models (<see cref="Sol62"/>,
/// <see cref="Astra62"/>): ultrafast costs 3× the standard card through 28 February 2027 and the
/// usual 6× from 1 March 2027 (Pacific time). The ultrafast models released before GPT-6.2
/// (<see cref="Sol61"/>, <see cref="Astra6"/>) are not part of it and stay at 6×.
/// </summary>
/// <remarks>
/// The factor follows the system clock and switches on its own, the same way the date-aware
/// Gemini rate cards do. OpenAI states the boundary as a Pacific-time date with no clock time, so
/// the switch is taken at midnight Pacific Standard Time (UTC−8; daylight saving time starts on
/// 14 March 2027).
/// </remarks>
internal static class Gpt62UltraFastPromotion
{
    /// <summary>First moment the list factor applies: 1 March 2027, 00:00 PST.</summary>
    internal static readonly DateTimeOffset ListPricingFrom = new(2027, 3, 1, 0, 0, 0, TimeSpan.FromHours(-8));

    /// <summary>Ultrafast factor during the launch promotion.</summary>
    internal const decimal PromotionMultiplier = 3m;

    /// <summary>Ultrafast factor at list price — OpenAI's usual 6×.</summary>
    internal const decimal ListMultiplier = 6m;

    /// <summary>The ultrafast factor in force at <paramref name="at"/>.</summary>
    internal static decimal MultiplierAt(DateTimeOffset at)
        => at < ListPricingFrom ? PromotionMultiplier : ListMultiplier;
}
