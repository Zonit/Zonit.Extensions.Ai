using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions;
using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.Benchmarks;

/// <summary>
/// Prompt rendering — turning a <see cref="PromptBase{T}"/> template plus its
/// properties into final text via Scriban, done once per request. This is the
/// heaviest local step (the renderer calls <c>Template.Parse</c> on every render),
/// so it is the prime candidate for "does anything need optimization?". The
/// <see cref="SimplePrompt{T}"/> case is the no-templating baseline.
/// </summary>
[MemoryDiagnoser]
public class PromptRenderBenchmarks
{
    private IPromptRenderer _renderer = null!;
    private SimplePrompt<string> _simple = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Resolve the real, source-generator-backed renderer through DI exactly as
        // the application does. AddAi wires ScribanPromptRenderer + the AOT bindings.
        var services = new ServiceCollection();
        services.AddAi();
        var provider = services.BuildServiceProvider();
        _renderer = provider.GetRequiredService<IPromptRenderer>();

        _simple = new SimplePrompt<string>("Just return the capital of France.");

        // Sanity-check the binding path is actually populated (not silently empty).
        var rendered = _renderer.Render(Samples.Prompt);
        if (!rendered.Contains("Central bank holds rates steady"))
            throw new InvalidOperationException("Prompt binding did not populate template variables.");
    }

    [Benchmark(Baseline = true, Description = "Render typed PromptBase (Scriban)")]
    public string Render_TypedPrompt() => _renderer.Render(Samples.Prompt);

    [Benchmark(Description = "Render SimplePrompt (no templating)")]
    public string Render_SimplePrompt() => _renderer.Render(_simple);
}
