using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai.Google;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Cost;

/// <summary>
/// Pins long-context pricing. Providers tier their rates on the size of the
/// CONTEXT (input tokens), and that tier raises the output and cache rates too —
/// not just the input rate. These tests lock two regressions:
/// <list type="bullet">
///   <item><description>keying the output tier on the output token count, which can
///   never fire because <c>MaxOutputTokens</c> sits below every provider threshold;</description></item>
///   <item><description>a model's price override being shadowed by the default
///   interface implementation on <see cref="ITextLlm"/> instead of dispatching
///   virtually (the reason the cache getters live on <see cref="LlmBase"/>).</description></item>
/// </list>
/// </summary>
public class LongContextPricingTests
{
    private const int Short = 100_000;   // below every threshold under test
    private const int Long = 300_000;    // above OpenAI's 272K and xAI's 200K

    // ---- OpenAI GPT-5.6: input ×2, cache ×2, output ×1.5 beyond 272K ----

    [Theory]
    // model,             short in, long in, short out, long out, short cached, long cached
    [InlineData(typeof(Astra6), 10.00, 20.00, 50.00, 75.00, 1.00, 2.00)]
    [InlineData(typeof(Sol6), 2.00, 4.00, 10.00, 15.00, 0.20, 0.40)]
    [InlineData(typeof(Luna6), 0.10, 0.20, 0.50, 0.75, 0.01, 0.02)]
    [InlineData(typeof(Sol56), 4.00, 8.00, 20.00, 30.00, 0.40, 0.80)]
    [InlineData(typeof(Terra56), 2.00, 4.00, 12.00, 18.00, 0.20, 0.40)]
    [InlineData(typeof(Luna56), 0.20, 0.40, 1.20, 1.80, 0.02, 0.04)]
    public void Gpt56_TiersEveryRateOnTheInputSize(
        Type modelType,
        double shortIn, double longIn,
        double shortOut, double longOut,
        double shortCached, double longCached)
    {
        // ILlm, not ITextLlm: the GPT-5.6 tiers are IReasoningLlm, which is exactly
        // the family the old ITextLlm-gated cache path skipped.
        var model = (ILlm)Activator.CreateInstance(modelType)!;

        model.GetInputPrice(Short).Should().Be((decimal)shortIn);
        model.GetInputPrice(Long).Should().Be((decimal)longIn);

        // The output rate must follow the INPUT size. A tiny output on a huge
        // prompt is still billed at the long-context rate.
        model.GetOutputPrice(Short, outputTokens: 1_000).Should().Be((decimal)shortOut);
        model.GetOutputPrice(Long, outputTokens: 1_000).Should().Be((decimal)longOut);

        // Called through the interface: proves the model's override dispatches
        // virtually rather than being shadowed by the base-class default.
        model.GetCachedInputPrice(Short).Should().Be((decimal)shortCached);
        model.GetCachedInputPrice(Long).Should().Be((decimal)longCached);
    }

    // ---- OpenAI GPT-5.6+: cache writes cost 1.25× input and tier with it ----

    [Theory]
    // model,              short write, long write
    [InlineData(typeof(Astra6), 12.50, 25.00)]
    [InlineData(typeof(Sol6), 2.50, 5.00)]
    [InlineData(typeof(Luna6), 0.125, 0.25)]
#pragma warning disable CS0618 // superseded, still billed
    [InlineData(typeof(Sol56), 5.00, 10.00)]
    [InlineData(typeof(Luna56), 0.25, 0.50)]
#pragma warning restore CS0618
    [InlineData(typeof(Terra56), 2.50, 5.00)]
    [InlineData(typeof(Cyber56), 15.625, 15.625)]
    public void Gpt56Plus_CacheWriteRate_Is125PercentOfInput_AndTiers(Type modelType, double shortWrite, double longWrite)
    {
        var model = (ILlm)Activator.CreateInstance(modelType)!;

        model.GetCachedInputWritePrice(Short).Should().Be((decimal)shortWrite);
        model.GetCachedInputWritePrice(Long).Should().Be((decimal)longWrite);
    }

    [Fact]
    public void Gpt55_HasNoCacheWritePremium()
    {
#pragma warning disable CS0618
        var model = new GPT55();
#pragma warning restore CS0618

        model.PriceCachedInputWrite.Should().BeNull();
        model.GetCachedInputWritePrice(Short).Should().Be(model.GetInputPrice(Short));
    }

    [Fact]
    public void Sol6_FastCacheWrite_IsDoubled()
    {
        var fast = (IFast)new Sol6 { Speed = SpeedType.Fast };

        fast.GetFastCachedInputWritePrice(Short).Should().Be(5.00m);
        fast.GetFastCachedInputWritePrice(Long).Should().Be(10.00m);
    }

    [Fact]
    public void Sol56_LongContextCost_BillsCacheAndOutputAtTheRaisedRates()
    {
        var usage = new TokenUsage
        {
            InputTokens = 300_000,   // 200K uncached + 100K cache reads
            CachedTokens = 100_000,
            OutputTokens = 10_000,
        };

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(new Sol56(), usage);

        // input : 200_000/1M × $8 + 100_000/1M × $0.80 = 1.60 + 0.08
        inputCost.Value.Should().BeApproximately(1.68m, 1e-9m);
        // output: 10_000/1M × $30 = 0.30  (the tier is keyed on input, not output, tokens)
        outputCost.Value.Should().BeApproximately(0.30m, 1e-9m);
    }

    [Fact]
    public void Sol56_ShortContextCost_StaysOnTheBaseRates()
    {
        var usage = new TokenUsage
        {
            InputTokens = 100_000,
            CachedTokens = 50_000,
            OutputTokens = 10_000,
        };

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(new Sol56(), usage);

        // input : 50_000/1M × $4 + 50_000/1M × $0.40 = 0.20 + 0.02
        inputCost.Value.Should().BeApproximately(0.22m, 1e-9m);
        // output: 10_000/1M × $20 = 0.20
        outputCost.Value.Should().BeApproximately(0.20m, 1e-9m);
    }

    [Fact]
    public void Sol56_BatchCost_TiersOnTheInputSizeToo()
    {
        var shortUsage = new TokenUsage { InputTokens = 100_000, OutputTokens = 10_000 };
        var longUsage = new TokenUsage { InputTokens = 300_000, OutputTokens = 10_000 };
        var model = new Sol56();

        // 100_000/1M × $2.00 + 10_000/1M × $10 = 0.20 + 0.10
        AiCostCalculator.CalculateBatchCost(model, shortUsage)
            .Value.Should().BeApproximately(0.30m, 1e-9m);

        // 300_000/1M × $4.00 + 10_000/1M × $15.00 = 1.20 + 0.15
        AiCostCalculator.CalculateBatchCost(model, longUsage)
            .Value.Should().BeApproximately(1.35m, 1e-9m);
    }

    // ---- xAI: the 200K tier used to be dead code on the output side ----

    [Fact]
    public void Grok420_OutputTier_FiresOnTheInputSize()
    {
        var model = new Grok420Reasoning();

        // MaxOutputTokens is 131_072, so an output-keyed threshold of 200K could
        // never be reached — the doubled rate was unreachable before the fix.
        model.GetOutputPrice(inputTokens: Long, outputTokens: 1_000).Should().Be(5.00m);
        model.GetOutputPrice(inputTokens: Short, outputTokens: 131_072).Should().Be(2.50m);
    }

    [Theory]
    // model,            short in, long in, short out, long out, short cached, long cached
    [InlineData(typeof(Grok47), 2.00, 4.00, 6.00, 12.00, 0.50, 1.00)]
    [InlineData(typeof(Grok46), 2.00, 4.00, 6.00, 12.00, 0.50, 1.00)]
    [InlineData(typeof(Grok45), 2.00, 4.00, 6.00, 12.00, 0.30, 0.60)]
    public void Grok4x_TiersEveryRateOnTheInputSize(
        Type modelType,
        double shortIn, double longIn,
        double shortOut, double longOut,
        double shortCached, double longCached)
    {
        var model = (ILlm)Activator.CreateInstance(modelType)!;

        model.GetInputPrice(Short).Should().Be((decimal)shortIn);
        model.GetInputPrice(Long).Should().Be((decimal)longIn);

        model.GetOutputPrice(Short, outputTokens: 1_000).Should().Be((decimal)shortOut);
        model.GetOutputPrice(Long, outputTokens: 1_000).Should().Be((decimal)longOut);

        // Cache reads tier too — the rate xAI publishes for ≥200K prompts is
        // double the short-context one, and long agent loops are exactly where
        // cache reads dominate the bill.
        model.GetCachedInputPrice(Short).Should().Be((decimal)shortCached);
        model.GetCachedInputPrice(Long).Should().Be((decimal)longCached);
    }

    [Fact]
    public void Grok46_LongContextThreshold_IsInclusive()
    {
        var model = new Grok46();

        // xAI words it as "≥ 200k", so exactly 200,000 prompt tokens already
        // bills at the higher card — not one token later.
        model.GetInputPrice(199_999).Should().Be(2.00m);
        model.GetInputPrice(200_000).Should().Be(4.00m);
        model.GetCachedInputPrice(200_000).Should().Be(1.00m);
        model.GetOutputPrice(200_000, outputTokens: 1_000).Should().Be(12.00m);
    }

    // ---- Google: input ×2, cache ×2, output ×1.5 above 200K prompt tokens ----

    [Theory]
    // model,             short in, long in, short out, long out, short cached, long cached
    [InlineData(typeof(Gemini31Pro), 2.00, 4.00, 12.00, 18.00, 0.20, 0.40)]
#pragma warning disable CS0618 // Gemini25Pro is limited to existing users but still priced.
    [InlineData(typeof(Gemini25Pro), 1.25, 2.50, 10.00, 15.00, 0.125, 0.25)]
#pragma warning restore CS0618
    public void GeminiPro_TiersEveryRateOnTheInputSize(
        Type modelType,
        double shortIn, double longIn,
        double shortOut, double longOut,
        double shortCached, double longCached)
    {
        var model = (ILlm)Activator.CreateInstance(modelType)!;

        model.GetInputPrice(Short).Should().Be((decimal)shortIn);
        model.GetInputPrice(Long).Should().Be((decimal)longIn);
        model.GetOutputPrice(Short, outputTokens: 1_000).Should().Be((decimal)shortOut);
        model.GetOutputPrice(Long, outputTokens: 1_000).Should().Be((decimal)longOut);
        model.GetCachedInputPrice(Short).Should().Be((decimal)shortCached);
        model.GetCachedInputPrice(Long).Should().Be((decimal)longCached);
    }

    [Fact]
    public void GeminiFlash_IsFlatAcrossContextSizes()
    {
        var model = new Gemini38Flash { PricingDate = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero) };

        model.GetInputPrice(Long).Should().Be(0.75m);
        model.GetOutputPrice(Long, outputTokens: 1_000).Should().Be(3.75m);
        model.GetCachedInputPrice(Long).Should().Be(0.075m);
    }

    // ---- Google: Gemini 3.8 Flash leaves its launch pricing on 1 January 2027 ----

    [Fact]
    public void Gemini38Flash_UsesLaunchRatesThrough31December2026()
    {
        var model = new Gemini38Flash { PricingDate = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero) };

        model.IsLaunchPricing.Should().BeTrue();
        model.PriceInput.Should().Be(0.75m);
        model.PriceOutput.Should().Be(3.75m);
        model.PriceCachedInput.Should().Be(0.075m);
    }

    [Fact]
    public void Gemini38Flash_DoublesEveryRateFrom1January2027()
    {
        var model = new Gemini38Flash { PricingDate = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero) };

        model.IsLaunchPricing.Should().BeFalse();
        model.GetInputPrice(Short).Should().Be(1.50m);
        model.GetOutputPrice(Short, outputTokens: 1_000).Should().Be(7.50m);
        model.GetCachedInputPrice(Short).Should().Be(0.15m);
        model.GetBatchInputPrice(Short).Should().Be(0.75m);
    }

    [Fact]
    public void Gemini38Flash_ComparesTheSwitchInUtc()
    {
        // 00:30 on 1 January in Warsaw is still 31 December in UTC.
        var model = new Gemini38Flash { PricingDate = new DateTimeOffset(2027, 1, 1, 0, 30, 0, TimeSpan.FromHours(1)) };

        model.IsLaunchPricing.Should().BeTrue();
    }

    [Fact]
    public void Gemini38Flash_Cost_FollowsThePricingDate()
    {
        var usage = new TokenUsage { InputTokens = 1_000_000, OutputTokens = 100_000 };

        var launch = AiCostCalculator.CalculateCosts(
            new Gemini38Flash { PricingDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) }, usage);
        var standard = AiCostCalculator.CalculateCosts(
            new Gemini38Flash { PricingDate = new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero) }, usage);

        launch.InputCost.Value.Should().BeApproximately(0.75m, 1e-9m);
        launch.OutputCost.Value.Should().BeApproximately(0.375m, 1e-9m);
        standard.InputCost.Value.Should().BeApproximately(1.50m, 1e-9m);
        standard.OutputCost.Value.Should().BeApproximately(0.75m, 1e-9m);
    }
}
