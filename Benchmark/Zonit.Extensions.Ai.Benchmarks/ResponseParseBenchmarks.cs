using BenchmarkDotNet.Attributes;
using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.Benchmarks;

/// <summary>
/// Parsing a model's reply back into a typed value — the local work done once per
/// response.
/// <para>
/// Two distinct entry points are measured:
/// </para>
/// <list type="bullet">
///   <item><see cref="JsonResponseParser.DeserializeStructured{T}"/> — the typed
///   structured-output path (what <c>PromptBase&lt;T&gt;</c> requests use). Handles
///   string enums, the <c>{result:…}</c> envelope, and the AOT JsonTypeInfo.</item>
///   <item><see cref="JsonResponseParser.Parse{T}"/> — the resilient general parser,
///   primarily used for primitive replies (string/int/bool) and markdown extraction.</item>
/// </list>
/// </summary>
[MemoryDiagnoser]
public class ResponseParseBenchmarks
{
    private const string TextReply = "The capital of France is Paris.";
    private const string IntReply = "The answer is 42.";
    private const string BoolReply = "yes";

#pragma warning disable IL2026, IL3050 // Primitive Parse<T> overloads take fixed paths; no per-type reflection codegen.

    [Benchmark(Description = "Parse<string> (text extraction)")]
    public string Parse_String() => JsonResponseParser.Parse<string>(TextReply);

    [Benchmark(Description = "Parse<int> (numeric extraction)")]
    public int Parse_Int() => JsonResponseParser.Parse<int>(IntReply);

    [Benchmark(Description = "Parse<bool>")]
    public bool Parse_Bool() => JsonResponseParser.Parse<bool>(BoolReply);

    [Benchmark(Description = "Parse<T> complex object (string enums)")]
    public ArticleAnalysis Parse_Object()
        => JsonResponseParser.Parse<ArticleAnalysis>(Samples.PlainJson);

#pragma warning restore IL2026, IL3050

    [Benchmark(Description = "DeserializeStructured clean JSON")]
    public ArticleAnalysis DeserializeStructured_Plain()
        => JsonResponseParser.DeserializeStructured<ArticleAnalysis>(Samples.PlainJson);

    [Benchmark(Description = "DeserializeStructured {result:…} envelope")]
    public ArticleAnalysis DeserializeStructured_Enveloped()
        => JsonResponseParser.DeserializeStructured<ArticleAnalysis>(Samples.EnvelopedJson);

    [Benchmark(Description = "ExtractJson from markdown fence")]
    public string ExtractJson_Markdown()
        => JsonResponseParser.ExtractJson(Samples.MarkdownWrappedJson);
}
