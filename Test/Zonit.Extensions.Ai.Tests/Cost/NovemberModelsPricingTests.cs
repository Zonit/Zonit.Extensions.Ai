using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.Google;
using Zonit.Extensions.Ai.OpenAi;

namespace Zonit.Extensions.Ai.Tests.Cost;

/// <summary>
/// Pins the rate cards of the models added in 10.20: Claude Fable 5.2 / Mythos 5.2,
/// GPT-6.1 Terra, GPT-6 Cyber and Gemini 4 Argon / Carbon — with the cache buckets
/// billed through <see cref="AiCostCalculator"/>, the path that once dropped the
/// uncached remainder.
/// </summary>
public class NovemberModelsPricingTests
{
    // ---- Anthropic: Fable 5.2 / Mythos 5.2 ----

    [Theory]
    [InlineData(typeof(Fable52), "claude-fable-5-2")]
    [InlineData(typeof(Mythos52), "claude-mythos-5-2")]
    public void Fable52_PublishesTheAnthropicCard(Type modelType, string name)
    {
        var model = (AnthropicBase)Activator.CreateInstance(modelType)!;

        model.Name.Should().Be(name);
        model.GetInputPrice(500_000).Should().Be(8.00m, "1M context has no long-context surcharge");
        model.GetOutputPrice(500_000, 1_000).Should().Be(40.00m);
        model.GetCachedInputPrice(500_000).Should().Be(0.20m);
        model.GetCachedInputWritePrice(500_000).Should().Be(10.00m);
        model.GetBatchInputPrice(1_000).Should().Be(4.00m);
        model.GetBatchOutputPrice(1_000, 1_000).Should().Be(20.00m);
        model.MaxInputTokens.Should().Be(1_000_000);
        model.MaxOutputTokens.Should().Be(128_000);
    }

    [Fact]
    public void Fable52_BillsEachCacheBucketAtItsOwnRate()
    {
        // InputTokens is the inclusive total, as the Anthropic provider normalizes it:
        // 1_000 uncached + 5_000 cache read + 2_000 cache write.
        var usage = new TokenUsage
        {
            InputTokens = 8_000,
            CachedTokens = 5_000,
            CacheWriteTokens = 2_000,
            OutputTokens = 500,
        };

        var (input, output) = AiCostCalculator.CalculateCosts(new Fable52(), usage);

        // 1_000 × $8 + 5_000 × $0.20 + 2_000 × $10, per 1M
        input.Value.Should().Be(0.029m);
        // 500 × $40 per 1M
        output.Value.Should().Be(0.02m);
    }

    [Fact]
    public void Fable52_OneHourCacheWrite_IsTwiceTheInputRate()
    {
        var model = new Fable52 { Cache = Anthropic.Cache.OneHour };

        model.GetCachedInputWritePrice(1_000).Should().Be(16.00m);
        AiCostCalculator.CalculateInputCost(model, inputTokens: 3_000, cachedTokens: 0, cacheWriteTokens: 2_000)
            .Value.Should().Be(0.04m); // 1_000 × $8 + 2_000 × $16, per 1M
    }

    // ---- OpenAI: GPT-6.1 Terra / GPT-6 Cyber cache writes follow the long-context tier ----

    [Theory]
    // model,          short write, long write
    [InlineData(typeof(Terra61), 1.25, 2.50)]
    [InlineData(typeof(Cyber6), 25.00, 50.00)]
    public void Gpt6CacheWrites_DoublePast272K(Type modelType, double shortWrite, double longWrite)
    {
        var model = (ILlm)Activator.CreateInstance(modelType)!;

        model.GetCachedInputWritePrice(100_000).Should().Be((decimal)shortWrite);
        model.GetCachedInputWritePrice(300_000).Should().Be((decimal)longWrite);
    }

    [Fact]
    public void Terra61_BatchIsHalfTheApplicableRate()
    {
        var model = new Terra61();

        model.GetBatchInputPrice(100_000).Should().Be(0.50m);
        model.GetBatchOutputPrice(100_000, 1_000).Should().Be(2.50m);
        model.GetBatchInputPrice(300_000).Should().Be(1.00m);
        model.GetBatchOutputPrice(300_000, 1_000).Should().Be(3.75m);
    }

    [Fact]
    public void Cyber6_IsResponsesAndChatOnly()
        => new Cyber6().SupportedEndpoints.Should().Be(EndpointsType.Chat | EndpointsType.Response);

    // ---- Google: Gemini 4 leaves its introductory pricing on 1 December 2026 ----

    [Fact]
    public void Gemini4_UsesIntroRatesThrough30November2026()
        => Gemini4Base.RateCard(new DateTimeOffset(2026, 11, 30, 23, 59, 59, TimeSpan.Zero))
            .Should().Be((2.00m, 0.10m, 10.00m));

    [Fact]
    public void Gemini4_DoublesEveryRateFrom1December2026()
        => Gemini4Base.RateCard(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero))
            .Should().Be((4.00m, 0.20m, 20.00m));

    [Fact]
    public void Gemini4_ComparesTheSwitchInUtc()
        // 00:30 on 1 December in Warsaw is still 30 November in UTC.
        => Gemini4Base.RateCard(new DateTimeOffset(2026, 12, 1, 0, 30, 0, TimeSpan.FromHours(1)))
            .Input.Should().Be(2.00m);

    [Theory]
    [InlineData(typeof(Gemini4Argon), "gemini-4-argon")]
    [InlineData(typeof(Gemini4Carbon), "gemini-4-carbon")]
    public void Gemini4_PricesFollowTheSystemClock(Type modelType, string name)
    {
        var model = (Gemini4Base)Activator.CreateInstance(modelType)!;
        var card = Gemini4Base.RateCard(DateTimeOffset.UtcNow);

        model.Name.Should().Be(name);
        model.PriceInput.Should().Be(card.Input);
        model.PriceCachedInput.Should().Be(card.CachedInput);
        model.PriceOutput.Should().Be(card.Output);
        model.GetCachedInputPrice(500_000).Should().Be(card.CachedInput, "no long-context tier");
        model.GetBatchInputPrice(1_000).Should().Be(card.Input * 0.5m);
        model.MaxOutputTokens.Should().Be(65_536);
    }

    [Fact]
    public void Gemini4_HasNoMinimalThinkingLevel()
        => Enum.GetNames<Gemini4Base.ReasonType>().Should().Equal("Low", "Medium", "High");
}
