using FluentAssertions;
using Xunit;
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
    [InlineData(typeof(Sol56), 5.00, 10.00, 30.00, 45.00, 0.50, 1.00)]
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

        // input : 200_000/1M × $10 + 100_000/1M × $1.00 = 2.00 + 0.10
        inputCost.Value.Should().BeApproximately(2.10m, 1e-9m);
        // output: 10_000/1M × $45 = 0.45  (was 0.30 when the tier was keyed on output tokens)
        outputCost.Value.Should().BeApproximately(0.45m, 1e-9m);
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

        // input : 50_000/1M × $5 + 50_000/1M × $0.50 = 0.25 + 0.025
        inputCost.Value.Should().BeApproximately(0.275m, 1e-9m);
        outputCost.Value.Should().BeApproximately(0.30m, 1e-9m);
    }

    [Fact]
    public void Sol56_BatchCost_TiersOnTheInputSizeToo()
    {
        var shortUsage = new TokenUsage { InputTokens = 100_000, OutputTokens = 10_000 };
        var longUsage = new TokenUsage { InputTokens = 300_000, OutputTokens = 10_000 };
        var model = new Sol56();

        // 100_000/1M × $2.50 + 10_000/1M × $15 = 0.25 + 0.15
        AiCostCalculator.CalculateBatchCost(model, shortUsage)
            .Value.Should().BeApproximately(0.40m, 1e-9m);

        // 300_000/1M × $5.00 + 10_000/1M × $22.50 = 1.50 + 0.225
        AiCostCalculator.CalculateBatchCost(model, longUsage)
            .Value.Should().BeApproximately(1.725m, 1e-9m);
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
}
