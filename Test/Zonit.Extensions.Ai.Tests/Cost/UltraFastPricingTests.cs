using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Cost;

/// <summary>
/// Pins OpenAI ultrafast (<see cref="IUltraFast"/>): a flat 6× on every token type that composes
/// with long-context tiering, billed only for the tier the provider says it served, and refused
/// up front on a model that does not offer it.
/// </summary>
public class UltraFastPricingTests
{
    private const int Short = 100_000;
    private const int Long = 300_000;    // above OpenAI's 272K

    [Theory]
    // model,        input short/long,  cached short/long, write short/long,   output short/long
    [InlineData(typeof(Sol61), 12.00, 24.00, 0.60, 1.20, 15.00, 30.00, 60.00, 90.00)]
    [InlineData(typeof(Astra6), 60.00, 120.00, 6.00, 12.00, 75.00, 150.00, 300.00, 450.00)]
    public void UltraFastCard_IsSixTimesStandard_AndTiers(
        Type modelType,
        double inShort, double inLong,
        double cachedShort, double cachedLong,
        double writeShort, double writeLong,
        double outShort, double outLong)
    {
        var model = (IUltraFast)Activator.CreateInstance(modelType)!;

        model.GetUltraFastInputPrice(Short).Should().Be((decimal)inShort);
        model.GetUltraFastInputPrice(Long).Should().Be((decimal)inLong);
        model.GetUltraFastCachedInputPrice(Short).Should().Be((decimal)cachedShort);
        model.GetUltraFastCachedInputPrice(Long).Should().Be((decimal)cachedLong);
        model.GetUltraFastCachedInputWritePrice(Short).Should().Be((decimal)writeShort);
        model.GetUltraFastCachedInputWritePrice(Long).Should().Be((decimal)writeLong);
        model.GetUltraFastOutputPrice(Short, 1_000).Should().Be((decimal)outShort);
        model.GetUltraFastOutputPrice(Long, 1_000).Should().Be((decimal)outLong);
    }

    [Theory]
    [InlineData(SpeedType.UltraFast, 2.40, 6.00)]
    [InlineData(SpeedType.Fast, 0.80, 2.00)]          // downgraded to fast
    [InlineData(SpeedType.Standard, 0.40, 1.00)]      // downgraded to standard
    public void Cost_FollowsTheServedTier(SpeedType served, double input, double output)
    {
        // 200K input stays below the 272K long-context threshold.
        var usage = new TokenUsage { InputTokens = 200_000, OutputTokens = 100_000 };

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(
            new Sol61 { Speed = SpeedType.UltraFast }, usage, served);

        inputCost.Value.Should().Be((decimal)input);
        outputCost.Value.Should().Be((decimal)output);
    }

    [Fact]
    public void LegacyGrantedFlag_BillsTheRequestedUltraFastTier()
    {
        var usage = new TokenUsage { InputTokens = 200_000, OutputTokens = 0 };

        AiCostCalculator.CalculateCosts(new Sol61 { Speed = SpeedType.UltraFast }, usage).InputCost.Value
            .Should().Be(2.40m);
        AiCostCalculator.CalculateCosts(new Sol61 { Speed = SpeedType.UltraFast }, usage, fastGranted: false).InputCost.Value
            .Should().Be(0.40m);
    }

    [Theory]
    [InlineData(null, SpeedType.UltraFast)]           // no echo: bill what was asked for
    [InlineData("ultrafast", SpeedType.UltraFast)]
    [InlineData("fast", SpeedType.Fast)]
    [InlineData("priority", SpeedType.Fast)]
    [InlineData("default", SpeedType.Standard)]
    public void Served_MapsTheEchoedTier(string? echo, SpeedType expected)
        => AiFastTier.Served(new Sol61 { Speed = SpeedType.UltraFast }, echo, NullLogger.Instance, "OpenAI", "test")
            .Should().Be(expected);

    [Fact]
    public void Served_NeverBillsAboveTheRequestedTier()
        => AiFastTier.Served(new Sol61 { Speed = SpeedType.Fast }, "ultrafast", NullLogger.Instance, "OpenAI", "test")
            .Should().Be(SpeedType.Fast);

    [Fact]
    public void Requested_RejectsUltraFastOnAModelWithoutIt()
    {
        var act = () => AiFastTier.Requested(new Opus55 { Speed = SpeedType.UltraFast });

        act.Should().Throw<NotSupportedException>().WithMessage("*ultrafast*");
        AiFastTier.Requested(new Grok47 { Speed = SpeedType.Fast }).Should().Be(SpeedType.Fast);
    }

    [Fact]
    public void UltraFastOnAModelWithoutIt_BillsAsStandard_RatherThanThrowing()
    {
        // Billing runs after the fact and must not fail; the request itself was refused earlier.
        var usage = new TokenUsage { InputTokens = 200_000, OutputTokens = 0 };

        AiCostCalculator.CalculateCosts(new Opus55 { Speed = SpeedType.UltraFast }, usage).InputCost.Value
            .Should().Be(0.80m);
    }
}
