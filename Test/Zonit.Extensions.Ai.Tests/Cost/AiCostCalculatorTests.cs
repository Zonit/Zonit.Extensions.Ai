using FluentAssertions;
using Xunit;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions.Ai.Tests.Cost;

/// <summary>
/// Pins the cost contract around cached / cache-write tokens. The calculator
/// treats <see cref="TokenUsage.InputTokens"/> as the INCLUSIVE grand total and
/// prices the uncached remainder as <c>InputTokens - CachedTokens - CacheWriteTokens</c>.
/// Providers whose API reports input exclusive of cache (Anthropic) must normalize
/// to the inclusive total before calling in — these tests lock that convention so a
/// regression (the pre-fix bug where the uncached delta was billed at zero) fails loudly.
/// </summary>
public class AiCostCalculatorTests
{
    // Opus 4.8: input $5, output $25, cached-read $0.50, cached-write $6.25 (per 1M).
    private static Opus48 Model => new();

    [Fact]
    public void CalculateCost_NoCache_ChargesAllInputAtRegularRate()
    {
        var usage = new TokenUsage { InputTokens = 8000, OutputTokens = 500 };

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(Model, usage);

        // 8000/1M × $5 = 0.04 ; 500/1M × $25 = 0.0125
        inputCost.Value.Should().BeApproximately(0.040m, 1e-9m);
        outputCost.Value.Should().BeApproximately(0.0125m, 1e-9m);
    }

    [Fact]
    public void CalculateCost_WithCacheReadAndWrite_PricesEachBucketSeparately()
    {
        // InputTokens is the inclusive total (1000 uncached + 5000 cache-read + 2000 cache-write).
        var usage = new TokenUsage
        {
            InputTokens = 8000,
            OutputTokens = 500,
            CachedTokens = 5000,
            CacheWriteTokens = 2000,
        };

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(Model, usage);

        // regular 1000×$5 + cached 5000×$0.50 + write 2000×$6.25, all per 1M
        //   = 0.005 + 0.0025 + 0.0125 = 0.020
        inputCost.Value.Should().BeApproximately(0.020m, 1e-9m);
        outputCost.Value.Should().BeApproximately(0.0125m, 1e-9m);
    }

    [Fact]
    public void CalculateCost_OneHourCache_PricesWriteAtDoubleBaseInput()
    {
        // Anthropic bills 1-hour cache writes at 2× base input (vs 1.25× for 5-min).
        // Cache.OneHour must therefore price cache-write tokens higher than the default.
        var model = new Opus48 { Cache = Cache.OneHour };
        var usage = new TokenUsage
        {
            InputTokens = 8000,
            OutputTokens = 500,
            CachedTokens = 5000,
            CacheWriteTokens = 2000,
        };

        var inputCost = AiCostCalculator.CalculateInputCost(
            model, usage.InputTokens, usage.CachedTokens, usage.CacheWriteTokens);

        // regular 1000×$5 + cached 5000×$0.50 + write 2000×($5×2=$10), per 1M
        //   = 0.005 + 0.0025 + 0.020 = 0.0275  (5-min would be 0.020)
        inputCost.Value.Should().BeApproximately(0.0275m, 1e-9m);
    }

    [Fact]
    public void CalculateInputCost_AllInputCached_StillChargesCacheReadRate()
    {
        // Degenerate case: the whole prompt was served from cache. The uncached
        // remainder is zero, but the cache reads must still be billed.
        var cost = AiCostCalculator.CalculateInputCost(Model, inputTokens: 5000, cachedTokens: 5000, cacheWriteTokens: 0);

        // 0 regular + 5000/1M × $0.50 = 0.0025
        cost.Value.Should().BeApproximately(0.0025m, 1e-9m);
    }
}
