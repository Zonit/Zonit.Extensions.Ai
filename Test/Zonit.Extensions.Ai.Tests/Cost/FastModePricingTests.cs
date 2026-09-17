using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Cost;

/// <summary>
/// Pins fast-mode pricing. A model implementing <see cref="IFast"/> publishes <b>two rate cards</b>
/// — its standard prices and its fast ones — and never decides between them itself; the choice is
/// made when the cost is computed, from what the provider echoed back. These tests lock that
/// split, the 2× premium composing with (rather than replacing) long-context tiering, and the
/// escape hatch for a tier that is not a flat multiple.
/// </summary>
public class FastModePricingTests
{
    private const int Short = 100_000;   // below every threshold under test
    private const int Long = 300_000;    // above OpenAI's 272K and xAI's 200K

    // ---- the model keeps publishing the standard card, whatever Speed says ----

    [Theory]
    [InlineData(typeof(Astra6))]
    [InlineData(typeof(Sol56))]
    [InlineData(typeof(Terra56))]
    [InlineData(typeof(Luna56))]
    public void AModelsOwnGetters_AreAlwaysTheStandardRates_EvenAtFastSpeed(Type modelType)
    {
        // The regression this guards: folding the premium into the model's own getters. It makes
        // every consumer of ILlm (estimates, dashboards, the cost calculator) silently report the
        // fast rate even for a request the provider downgraded and billed as standard.
        var standard = (ILlm)Activator.CreateInstance(modelType)!;
        var fast = CreateFast(modelType);

        fast.GetInputPrice(Short).Should().Be(standard.GetInputPrice(Short));
        fast.GetOutputPrice(Short, 1_000).Should().Be(standard.GetOutputPrice(Short, 1_000));
        fast.GetCachedInputPrice(Short).Should().Be(standard.GetCachedInputPrice(Short));
        fast.GetInputPrice(Long).Should().Be(standard.GetInputPrice(Long));
    }

    [Theory]
    [InlineData(typeof(Astra6))]
    [InlineData(typeof(Sol56))]
    [InlineData(typeof(Terra56))]
    [InlineData(typeof(Luna56))]
    public void OpenAiModels_DefaultToStandardSpeed(Type modelType)
        => ((IFast)Activator.CreateInstance(modelType)!).Speed.Should().Be(SpeedType.Standard);

    // ---- OpenAI: the fast card is 2× on input, cached input and output ----
    // Rates from the OpenAI fast-mode pricing table (short / long context).

    [Theory]
    // model,          fast short in, fast long in, fast short out, fast long out, fast short cached, fast long cached
    [InlineData(typeof(Astra6), 20.00, 40.00, 100.00, 150.00, 2.00, 4.00)]
    [InlineData(typeof(Sol56), 8.00, 16.00, 40.00, 60.00, 0.80, 1.60)]
    [InlineData(typeof(Terra56), 4.00, 8.00, 24.00, 36.00, 0.40, 0.80)]
    [InlineData(typeof(Luna56), 0.40, 0.80, 2.40, 3.60, 0.04, 0.08)]
    public void OpenAiFastCard_DoublesEveryRate_AndStillTiersOnContext(
        Type modelType,
        double shortIn, double longIn,
        double shortOut, double longOut,
        double shortCached, double longCached)
    {
        var model = (IFast)CreateFast(modelType);

        model.GetFastInputPrice(Short).Should().Be((decimal)shortIn);
        model.GetFastInputPrice(Long).Should().Be((decimal)longIn);

        model.GetFastOutputPrice(Short, outputTokens: 1_000).Should().Be((decimal)shortOut);
        model.GetFastOutputPrice(Long, outputTokens: 1_000).Should().Be((decimal)longOut);

        model.GetFastCachedInputPrice(Short).Should().Be((decimal)shortCached);
        model.GetFastCachedInputPrice(Long).Should().Be((decimal)longCached);
    }

    [Fact]
    public void HeadlineFastPrices_MatchTheShortContextRates()
    {
        var sol = (IFast)new Sol56 { Speed = SpeedType.Fast };

        sol.FastPriceInput.Should().Be(8.00m);
        sol.FastPriceOutput.Should().Be(40.00m);
    }

    [Fact]
    public void FastMode_LeavesBatchRatesAlone()
    {
        // Fast mode is a synchronous-API tier; Batch keeps its own (halved) card.
        var standard = new Sol56();
        var fast = new Sol56 { Speed = SpeedType.Fast };

        fast.GetBatchInputPrice(Short).Should().Be(standard.GetBatchInputPrice(Short));
        fast.GetBatchOutputPrice(Short, 1_000).Should().Be(standard.GetBatchOutputPrice(Short, 1_000));
    }

    // ---- xAI: Priority Processing is a flat 2× on every token type ----

    [Fact]
    public void GrokPriorityCard_DoublesEveryRate_AndStillTiersOnContext()
    {
        var priority = (IFast)new Grok46 { Speed = SpeedType.Fast };

        // Standard card is $2 / $0.50 / $6, doubled past 200K prompt tokens.
        priority.GetFastInputPrice(Short).Should().Be(4.00m);
        priority.GetFastInputPrice(Long).Should().Be(8.00m);

        priority.GetFastOutputPrice(Short, outputTokens: 1_000).Should().Be(12.00m);
        priority.GetFastOutputPrice(Long, outputTokens: 1_000).Should().Be(24.00m);

        priority.GetFastCachedInputPrice(Short).Should().Be(1.00m);
        priority.GetFastCachedInputPrice(Long).Should().Be(2.00m);
    }

    [Fact]
    public void Grok46_DefaultsToStandardScheduling()
    {
        var model = new Grok46();

        model.Speed.Should().Be(SpeedType.Standard);
        model.GetInputPrice(Short).Should().Be(2.00m);
        model.GetOutputPrice(Short, 1_000).Should().Be(6.00m);
    }

    // ---- the calculator is what picks a card ----

    [Fact]
    public void Cost_AtFastSpeed_BillsEveryTokenTypeOnTheFastCard()
    {
        var usage = new TokenUsage
        {
            InputTokens = 1_000_000,
            CachedTokens = 400_000,
            OutputTokens = 100_000,
        };

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(new Luna56 { Speed = SpeedType.Fast }, usage);

        // Long context (1M > 272K): 600K uncached @ $0.80 + 400K cached @ $0.08.
        inputCost.Value.Should().Be(0.6m * 0.80m + 0.4m * 0.08m);
        // 100K output @ $3.60.
        outputCost.Value.Should().Be(0.1m * 3.60m);
    }

    [Fact]
    public void Cost_WhenFastWasNotGranted_FallsBackToTheStandardCard()
    {
        // OpenAI and xAI serve fast mode best-effort and only charge the premium when they echo
        // the tier back. Billing the requested tier instead would inflate every cost figure by 2×
        // for as long as their fast capacity is exhausted.
        var usage = new TokenUsage
        {
            InputTokens = 500_000,
            CachedTokens = 100_000,
            OutputTokens = 50_000,
        };

        var fastModel = new Luna56 { Speed = SpeedType.Fast };

        var granted = AiCostCalculator.CalculateCosts(fastModel, usage);
        var downgraded = AiCostCalculator.CalculateCosts(fastModel, usage, fastGranted: false);
        var standard = AiCostCalculator.CalculateCosts(new Luna56(), usage);

        downgraded.Should().Be(standard);
        granted.InputCost.Value.Should().Be(standard.InputCost.Value * 2);
        granted.OutputCost.Value.Should().Be(standard.OutputCost.Value * 2);
    }

    [Fact]
    public void FastGrantedFalse_OnAModelThatNeverAskedForFast_ChangesNothing()
    {
        var usage = new TokenUsage { InputTokens = 1_000, OutputTokens = 500 };

        var asked = AiCostCalculator.CalculateCosts(new Luna56(), usage);
        var notAsked = AiCostCalculator.CalculateCosts(new Luna56(), usage, fastGranted: false);

        notAsked.Should().Be(asked);
    }

    // ---- a fast tier that is not a flat multiple ----

    [Fact]
    public void AModelWithANonUniformFastCard_IsBilledPerRate()
    {
        // Not every provider has to price fast mode as "everything ×2". A model overrides only
        // the rates that differ; the rest keep the uniform default.
        var usage = new TokenUsage
        {
            InputTokens = 1_000_000,
            CachedTokens = 200_000,
            OutputTokens = 100_000,
        };

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(
            new LopsidedFastModel { Speed = SpeedType.Fast }, usage);

        // Output triples ($10 → $30); uncached input keeps the default 2× ($1 → $2);
        // cache reads are not surcharged at all ($0.10, overridden to stay flat).
        outputCost.Value.Should().Be(0.1m * 30m);
        inputCost.Value.Should().Be(0.8m * 2m + 0.2m * 0.10m);
    }

    [Fact]
    public void AModelWithANonUniformFastCard_StillFallsBackCleanlyWhenNotGranted()
    {
        var usage = new TokenUsage { InputTokens = 1_000_000, CachedTokens = 200_000, OutputTokens = 100_000 };

        var downgraded = AiCostCalculator.CalculateCosts(
            new LopsidedFastModel { Speed = SpeedType.Fast }, usage, fastGranted: false);
        var standard = AiCostCalculator.CalculateCosts(new LopsidedFastModel(), usage);

        downgraded.Should().Be(standard);
    }

    // ---- reading the tier back off the response ----

    [Theory]
    // Granted, in both spellings: OpenAI echoes `priority` for a `fast` request, and
    // `priority` is xAI's own value.
    [InlineData("fast", true)]
    [InlineData("priority", true)]
    // A provider that reports nothing gives us nothing to contradict the request with.
    [InlineData(null, true)]
    // Downgraded.
    [InlineData("default", false)]
    [InlineData("flex", false)]
    public void WasGranted_ReadsTheEchoedTier(string? echoedTier, bool expected)
        => AiFastTier.WasGranted(
                new Luna56 { Speed = SpeedType.Fast },
                echoedTier,
                NullLogger.Instance,
                "OpenAI",
                "GenerateAsync")
            .Should().Be(expected);

    [Fact]
    public void WasGranted_IsAlwaysTrue_ForAModelAtStandardSpeed()
        => AiFastTier.WasGranted(new Luna56(), "default", NullLogger.Instance, "OpenAI", "GenerateAsync")
            .Should().BeTrue();

    private static ILlm CreateFast(Type modelType) => modelType switch
    {
        _ when modelType == typeof(Astra6) => new Astra6 { Speed = SpeedType.Fast },
        _ when modelType == typeof(Sol56) => new Sol56 { Speed = SpeedType.Fast },
        _ when modelType == typeof(Terra56) => new Terra56 { Speed = SpeedType.Fast },
        _ when modelType == typeof(Luna56) => new Luna56 { Speed = SpeedType.Fast },
        _ => throw new ArgumentOutOfRangeException(nameof(modelType), modelType, "Unmapped fast-mode model."),
    };

    /// <summary>
    /// A hypothetical model whose fast tier triples output, doubles input (the default) and does
    /// not surcharge cache reads at all — the shape <see cref="IFast"/> has to support without the
    /// model reimplementing its whole rate card.
    /// </summary>
    private sealed class LopsidedFastModel : LlmBase, IFast
    {
        public override string Name => "lopsided-fast-1";
        public override decimal PriceInput => 1.00m;
        public override decimal PriceOutput => 10.00m;
        public override decimal GetCachedInputPrice(long inputTokens) => 0.10m;
        public override int MaxInputTokens => 100_000;
        public override int MaxOutputTokens => 10_000;
        public override ChannelType Input => ChannelType.Text;
        public override ChannelType Output => ChannelType.Text;

        public SpeedType Speed { get; init; } = SpeedType.Standard;

        public decimal GetFastOutputPrice(long inputTokens, long outputTokens) => 30.00m;
        public decimal GetFastCachedInputPrice(long inputTokens) => 0.10m;
    }
}
