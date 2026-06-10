using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.Benchmarks;

/// <summary>
/// Structured-output schema generation. Every request that asks a model for a
/// typed response needs the JSON Schema for that type. There are two paths:
///   * <see cref="JsonSchemaGenerator.Generate{T}"/> — reflection, walks the type
///     graph on every call (the fallback path / what you pay without the generator).
///   * <see cref="AiSchemaRegistry.GetSchema"/> — returns the build-time schema the
///     source generator precomputed, parsed once and cached as a JsonElement.
/// The gap between them is the value the source generator buys per request.
/// </summary>
[MemoryDiagnoser]
public class SchemaBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        // Force the module initializers (which Register the generated schemas) to
        // have run, and warm the registry's parse-and-clone cache for a fair
        // steady-state measurement.
        _ = AiSchemaRegistry.IsRegistered(typeof(ArticleAnalysis));
        _ = AiSchemaRegistry.GetSchema(typeof(ArticleAnalysis));
    }

    [Benchmark(Baseline = true, Description = "Reflection (JsonSchemaGenerator)")]
    public JsonElement Reflection() => JsonSchemaGenerator.Generate<ArticleAnalysis>();

    [Benchmark(Description = "Source-gen + cache (AiSchemaRegistry)")]
    public JsonElement Registry() => AiSchemaRegistry.GetSchema(typeof(ArticleAnalysis));
}
