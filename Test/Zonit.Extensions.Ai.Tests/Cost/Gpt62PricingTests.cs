using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai.OpenAi;

namespace Zonit.Extensions.Ai.Tests.Cost;

/// <summary>
/// Pins the rate cards of GPT-6.2 Sol and GPT-6.2 Astra (released 17 November 2026): standard,
/// long-context (past 272K), batch, fast, and the ultrafast launch promotion — 3× through
/// 28 February 2027, 6× from 1 March 2027 (Pacific time) — which the older ultrafast models
/// do not get.
/// </summary>
public class Gpt62PricingTests
{
    private const int Short = 100_000;
    private const int Long = 300_000;    // above OpenAI's 272K

    // ---- standard card, long-context tiering, batch ----

    [Theory]
    // model,          name,            in short/long, cached short/long, write short/long, out short/long
    [InlineData(typeof(Sol62), "gpt-6.2-sol", 2.00, 4.00, 0.10, 0.20, 2.50, 5.00, 10.00, 15.00)]
    [InlineData(typeof(Astra62), "gpt-6.2-astra", 8.00, 16.00, 0.40, 0.80, 10.00, 20.00, 40.00, 60.00)]
    public void StandardCard_TiersPast272K(
        Type modelType, string name,
        double inShort, double inLong,
        double cachedShort, double cachedLong,
        double writeShort, double writeLong,
        double outShort, double outLong)
    {
        var model = (ILlm)Activator.CreateInstance(modelType)!;

        model.Name.Should().Be(name);
        model.GetInputPrice(Short).Should().Be((decimal)inShort);
        model.GetInputPrice(Long).Should().Be((decimal)inLong);
        model.GetCachedInputPrice(Short).Should().Be((decimal)cachedShort);
        model.GetCachedInputPrice(Long).Should().Be((decimal)cachedLong);
        model.GetCachedInputWritePrice(Short).Should().Be((decimal)writeShort);
        model.GetCachedInputWritePrice(Long).Should().Be((decimal)writeLong);
        model.GetOutputPrice(Short, 1_000).Should().Be((decimal)outShort);
        model.GetOutputPrice(Long, 1_000).Should().Be((decimal)outLong);
        model.MaxInputTokens.Should().Be(1_050_000);
        model.MaxOutputTokens.Should().Be(128_000);
    }

    [Theory]
    // model,          batch in short/long, batch out short/long
    [InlineData(typeof(Sol62), 1.00, 2.00, 5.00, 7.50)]
    [InlineData(typeof(Astra62), 4.00, 8.00, 20.00, 30.00)]
    public void Batch_IsHalfTheApplicableRate(
        Type modelType, double inShort, double inLong, double outShort, double outLong)
    {
        var model = (ILlm)Activator.CreateInstance(modelType)!;

        model.GetBatchInputPrice(Short).Should().Be((decimal)inShort);
        model.GetBatchInputPrice(Long).Should().Be((decimal)inLong);
        model.GetBatchOutputPrice(Short, 1_000).Should().Be((decimal)outShort);
        model.GetBatchOutputPrice(Long, 1_000).Should().Be((decimal)outLong);
    }

    [Fact]
    public void Astra62_BillsEachCacheBucketAtItsOwnRate()
    {
        // InputTokens is the inclusive total: 1_000 uncached + 5_000 cache read + 2_000 cache write.
        var usage = new TokenUsage
        {
            InputTokens = 8_000,
            CachedTokens = 5_000,
            CacheWriteTokens = 2_000,
            OutputTokens = 500,
        };

        var (input, output) = AiCostCalculator.CalculateCosts(new Astra62(), usage);

        // 1_000 × $8 + 5_000 × $0.40 + 2_000 × $10, per 1M
        input.Value.Should().Be(0.03m);
        // 500 × $40 per 1M
        output.Value.Should().Be(0.02m);
    }

    // ---- fast mode: 2× on every rate, composing with long context ----

    [Theory]
    // model,          fast in short/long, fast out short/long, fast cached short/long
    [InlineData(typeof(Sol62), 4.00, 8.00, 20.00, 30.00, 0.20, 0.40)]
    [InlineData(typeof(Astra62), 16.00, 32.00, 80.00, 120.00, 0.80, 1.60)]
    public void FastCard_DoublesEveryRate(
        Type modelType,
        double inShort, double inLong,
        double outShort, double outLong,
        double cachedShort, double cachedLong)
    {
        var model = (IFast)Activator.CreateInstance(modelType)!;

        model.Speed.Should().Be(SpeedType.Standard);
        model.GetFastInputPrice(Short).Should().Be((decimal)inShort);
        model.GetFastInputPrice(Long).Should().Be((decimal)inLong);
        model.GetFastOutputPrice(Short, 1_000).Should().Be((decimal)outShort);
        model.GetFastOutputPrice(Long, 1_000).Should().Be((decimal)outLong);
        model.GetFastCachedInputPrice(Short).Should().Be((decimal)cachedShort);
        model.GetFastCachedInputPrice(Long).Should().Be((decimal)cachedLong);
    }

    // ---- ultrafast: the launch promotion follows the clock ----

    [Fact]
    public void UltraFastPromotion_Is3xThrough28February2027PacificTime()
    {
        var pacific = TimeSpan.FromHours(-8);

        Gpt62UltraFastPromotion.MultiplierAt(new DateTimeOffset(2026, 11, 17, 0, 0, 0, pacific)).Should().Be(3m);
        Gpt62UltraFastPromotion.MultiplierAt(new DateTimeOffset(2027, 2, 28, 23, 59, 59, pacific)).Should().Be(3m);
        Gpt62UltraFastPromotion.MultiplierAt(new DateTimeOffset(2027, 3, 1, 0, 0, 0, pacific)).Should().Be(6m);
    }

    [Fact]
    public void UltraFastPromotion_ComparesTheSwitchInPacificTime()
    {
        // 07:59 UTC on 1 March is still 28 February in California; 08:00 UTC is midnight PST.
        Gpt62UltraFastPromotion.MultiplierAt(new DateTimeOffset(2027, 3, 1, 7, 59, 59, TimeSpan.Zero)).Should().Be(3m);
        Gpt62UltraFastPromotion.MultiplierAt(new DateTimeOffset(2027, 3, 1, 8, 0, 0, TimeSpan.Zero)).Should().Be(6m);
    }

    [Theory]
    [InlineData(typeof(Sol62))]
    [InlineData(typeof(Astra62))]
    public void UltraFastCard_IsTheCurrentFactorTimesTheStandardCard(Type modelType)
    {
        var model = (IUltraFast)Activator.CreateInstance(modelType)!;
        var factor = Gpt62UltraFastPromotion.MultiplierAt(DateTimeOffset.UtcNow);

        model.UltraFastMultiplier.Should().Be(factor);
        foreach (var tokens in new[] { Short, Long })
        {
            model.GetUltraFastInputPrice(tokens).Should().Be(model.GetInputPrice(tokens) * factor);
            model.GetUltraFastCachedInputPrice(tokens).Should().Be(model.GetCachedInputPrice(tokens) * factor);
            model.GetUltraFastCachedInputWritePrice(tokens).Should().Be(model.GetCachedInputWritePrice(tokens) * factor);
            model.GetUltraFastOutputPrice(tokens, 1_000).Should().Be(model.GetOutputPrice(tokens, 1_000) * factor);
        }
    }

    [Theory]
    // OpenAI's published short-context ultrafast rates: input / cached / output, promotion then list.
    [InlineData(typeof(Sol62), 6.00, 0.30, 30.00, 12.00, 0.60, 60.00)]
    [InlineData(typeof(Astra62), 24.00, 1.20, 120.00, 48.00, 2.40, 240.00)]
    public void UltraFastFactors_ReproduceThePublishedRates(
        Type modelType,
        double promoIn, double promoCached, double promoOut,
        double listIn, double listCached, double listOut)
    {
        var model = (ILlm)Activator.CreateInstance(modelType)!;
        const decimal promo = Gpt62UltraFastPromotion.PromotionMultiplier;
        const decimal list = Gpt62UltraFastPromotion.ListMultiplier;

        (model.GetInputPrice(Short) * promo).Should().Be((decimal)promoIn);
        (model.GetCachedInputPrice(Short) * promo).Should().Be((decimal)promoCached);
        (model.GetOutputPrice(Short, 1_000) * promo).Should().Be((decimal)promoOut);
        (model.GetInputPrice(Short) * list).Should().Be((decimal)listIn);
        (model.GetCachedInputPrice(Short) * list).Should().Be((decimal)listCached);
        (model.GetOutputPrice(Short, 1_000) * list).Should().Be((decimal)listOut);
    }

    [Theory]
#pragma warning disable CS0618 // superseded by the 6.2 models, still priced
    [InlineData(typeof(Sol61))]
    [InlineData(typeof(Astra6))]
#pragma warning restore CS0618
    public void OlderUltraFastModels_StayAtSixTimes(Type modelType)
        => ((IUltraFast)Activator.CreateInstance(modelType)!).UltraFastMultiplier.Should().Be(6m);

    // ---- surface ----

    [Theory]
    [InlineData(typeof(Sol62))]
    [InlineData(typeof(Astra62))]
    public void Gpt62_RunsOnChatResponsesAndBatch_WithoutPredictedOutputs(Type modelType)
    {
        var model = (ILlm)Activator.CreateInstance(modelType)!;

        model.SupportedEndpoints.Should().Be(EndpointsType.Chat | EndpointsType.Response | EndpointsType.Batch);
        model.SupportedFeatures.HasFlag(FeaturesType.PredictedOutputs).Should().BeFalse();
        model.Input.Should().Be(ChannelType.Text | ChannelType.Image);
    }
}
