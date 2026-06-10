# Zonit.Extensions.Ai — Benchmarks

Micro-benchmarks for the **local CPU work** the library does around an AI call.
The network round-trip to the model dominates real-world latency, so these do
**not** call any provider. They measure the per-request glue that runs on your
machine and would only matter at high request volume or in tight loops:

| Class | What it measures | Why it matters |
|-------|------------------|----------------|
| `SchemaBenchmarks` | JSON-Schema for a structured-output type: reflection vs. the source-generated + cached registry | Runs once per structured request to tell the model the expected shape |
| `PromptRenderBenchmarks` | Scriban rendering of a `PromptBase<T>` template vs. a `SimplePrompt` | Heaviest local step — the renderer parses the template on every call |
| `ResponseParseBenchmarks` | Parsing a reply back into a typed object (clean JSON, markdown-fenced, `{result:…}` envelope) | Runs once per response |
| `CostBenchmarks` | Token-cost arithmetic | Runs once per completed request |

The project references the **Zonit source generators** (like the test project), so
the "source-gen" paths exercise the real generated artifacts (build-time schema,
AOT-safe `JsonTypeInfo`, Scriban bindings) — not the reflection fallbacks.

## Running

```bash
# everything (full statistical run — a few minutes)
dotnet run -c Release

# a single class
dotnet run -c Release -- --filter *SchemaBenchmarks*

# faster, lower-fidelity pass (good for a quick look)
dotnet run -c Release -- --filter * --job short

# list available benchmarks
dotnet run -c Release -- --list flat
```

Results (and BenchmarkDotNet's full environment report) are written under
`BenchmarkDotNet.Artifacts/`.

## Interpreting

All workloads here are sub-microsecond to low-microsecond. Against a model call
measured in **hundreds to thousands of milliseconds**, every operation here is
effectively free per request. Use these to catch *regressions* (e.g. an
accidental allocation on the parse path, or losing the schema cache) rather than
to chase wall-clock wins.

See [`RESULTS.md`](RESULTS.md) for a captured run and the findings.
