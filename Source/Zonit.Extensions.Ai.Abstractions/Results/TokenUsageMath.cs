namespace Zonit.Extensions.Ai;

/// <summary>
/// Helpers for aggregating <see cref="TokenUsage"/> values (tokens and costs).
/// Used to roll up per-turn usage in the agent loop and to compute subtree
/// totals across the <see cref="AiUsageScope"/> call tree.
/// </summary>
public static class TokenUsageMath
{
    /// <summary>
    /// Component-wise sum of two usages (tokens and costs). <c>null</c> operands
    /// are treated as zero.
    /// </summary>
    public static TokenUsage Add(TokenUsage? a, TokenUsage? b)
    {
        if (a is null) return b ?? Zero;
        if (b is null) return a;

        return new TokenUsage
        {
            InputTokens = a.InputTokens + b.InputTokens,
            OutputTokens = a.OutputTokens + b.OutputTokens,
            CachedTokens = a.CachedTokens + b.CachedTokens,
            CacheWriteTokens = a.CacheWriteTokens + b.CacheWriteTokens,
            ReasoningTokens = a.ReasoningTokens + b.ReasoningTokens,
            InputCost = a.InputCost + b.InputCost,
            OutputCost = a.OutputCost + b.OutputCost,
        };
    }

    /// <summary>
    /// Component-wise sum of a sequence of usages. Returns <see cref="Zero"/> for an empty sequence.
    /// </summary>
    public static TokenUsage Sum(IEnumerable<TokenUsage?> usages)
    {
        ArgumentNullException.ThrowIfNull(usages);

        var total = Zero;
        foreach (var u in usages)
            total = Add(total, u);
        return total;
    }

    /// <summary>
    /// An all-zero usage. Reused so callers don't allocate a fresh empty instance.
    /// </summary>
    public static TokenUsage Zero { get; } = new();
}
