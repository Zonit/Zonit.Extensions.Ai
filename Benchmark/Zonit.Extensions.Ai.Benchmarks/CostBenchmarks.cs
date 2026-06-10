using BenchmarkDotNet.Attributes;
using Zonit.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.OpenAi;

namespace Zonit.Extensions.Ai.Benchmarks;

/// <summary>
/// Cost accounting — pure decimal arithmetic over a model's price properties, run
/// once per completed request. Cheap by construction; measured to confirm it is not
/// secretly allocating or boxing on a hot accounting path.
/// </summary>
[MemoryDiagnoser]
public class CostBenchmarks
{
    private readonly GPT4oMini _model = new();

    private readonly TokenUsage _plain = new()
    {
        InputTokens = 4_000,
        OutputTokens = 1_200,
    };

    private readonly TokenUsage _cached = new()
    {
        InputTokens = 12_000,
        CachedTokens = 8_000,
        CacheWriteTokens = 2_000,
        OutputTokens = 1_500,
    };

    [Benchmark(Baseline = true, Description = "CalculateCost (no cache)")]
    public Price CalculateCost_Plain() => AiCostCalculator.CalculateCost(_model, _plain);

    [Benchmark(Description = "CalculateCost (cache read+write)")]
    public Price CalculateCost_Cached() => AiCostCalculator.CalculateCost(_model, _cached);

    [Benchmark(Description = "CalculateBatchCost")]
    public Price CalculateBatchCost() => AiCostCalculator.CalculateBatchCost(_model, _plain);

    [Benchmark(Description = "EstimateCost (from prompt text)")]
    public Price EstimateCost() => AiCostCalculator.EstimateCost(_model, Samples.Prompt.Prompt);
}
