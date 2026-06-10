using System.ComponentModel;
using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.Benchmarks;

// ---------------------------------------------------------------------------
// Shared workload types.
//
// ArticleAnalysis is the structured-output response type. Because it is the
// generic argument of a PromptBase<T> below, the Zonit source generators emit:
//   * a build-time JSON Schema registered into AiSchemaRegistry
//   * an AOT-safe JsonTypeInfo registered into AiJsonTypeInfoResolver
//   * a Scriban binding for the prompt's properties
// so the benchmarks measure the same artifacts a real consumer would hit, not
// the reflection fallbacks.
// ---------------------------------------------------------------------------

[Description("Structured analysis of a news article.")]
public sealed class ArticleAnalysis
{
    [Description("One-sentence summary of the article.")]
    public string Summary { get; init; } = "";

    [Description("Overall sentiment.")]
    public Sentiment Sentiment { get; init; }

    [Description("Confidence in the analysis, 0.0 - 1.0.")]
    public double Confidence { get; init; }

    [Description("Up to five salient topics mentioned.")]
    public List<string> Topics { get; init; } = [];

    [Description("Key named entities found in the text.")]
    public List<Entity> Entities { get; init; } = [];

    [Description("Whether the article appears to be opinion rather than reporting.")]
    public bool IsOpinion { get; init; }

    [Description("Estimated reading time in minutes.")]
    public int ReadingTimeMinutes { get; init; }

    [Description("When the analysis was produced.")]
    public DateTime AnalyzedAt { get; init; }
}

public sealed class Entity
{
    [Description("Entity surface form.")]
    public string Name { get; init; } = "";

    [Description("Entity category.")]
    public EntityKind Kind { get; init; }

    [Description("Relevance to the article, 0.0 - 1.0.")]
    public double Relevance { get; init; }
}

public enum Sentiment { Negative, Neutral, Positive }

public enum EntityKind { Person, Organization, Location, Product, Other }

/// <summary>
/// Typed prompt whose response is <see cref="ArticleAnalysis"/>. Its properties
/// drive the Scriban template (snake_case mapping) — exercised by the render
/// benchmarks. Declaring it makes the source generators emit bindings/schema/
/// JsonTypeInfo for <see cref="ArticleAnalysis"/>.
/// </summary>
public sealed class AnalyzeArticlePrompt : PromptBase<ArticleAnalysis>
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string Language { get; init; }
    public int MaxTopics { get; init; } = 5;

    public override string Prompt =>
        """
        You are an expert analyst. Analyze the following article written in {{ language }}.

        Title: {{ title }}

        Body:
        {{ body }}

        Return at most {{ max_topics }} topics. Be concise and factual.
        """;
}

internal static class Samples
{
    public static AnalyzeArticlePrompt Prompt { get; } = new()
    {
        Title = "Central bank holds rates steady amid cooling inflation",
        Body =
            "Policymakers voted to keep the benchmark interest rate unchanged on Thursday, " +
            "citing easing price pressures and a resilient labour market. Analysts had widely " +
            "expected the decision, though several flagged risks from energy markets and a " +
            "softening export sector heading into the next quarter.",
        Language = "English",
        MaxTopics = 5,
    };

    /// <summary>A clean JSON object exactly matching <see cref="ArticleAnalysis"/>.</summary>
    public const string PlainJson =
        """
        {
          "summary": "The central bank left rates unchanged as inflation cools.",
          "sentiment": "neutral",
          "confidence": 0.82,
          "topics": ["monetary policy", "inflation", "interest rates", "labour market"],
          "entities": [
            { "name": "Central bank", "kind": "organization", "relevance": 0.95 },
            { "name": "Thursday", "kind": "other", "relevance": 0.2 }
          ],
          "isOpinion": false,
          "readingTimeMinutes": 2,
          "analyzedAt": "2026-06-10T09:30:00"
        }
        """;

    /// <summary>The same payload as a real model often returns it: wrapped in a
    /// markdown ```json fence with leading prose. Exercises the extraction path.</summary>
    public const string MarkdownWrappedJson =
        """
        Here is the structured analysis you requested:

        ```json
        {
          "summary": "The central bank left rates unchanged as inflation cools.",
          "sentiment": "neutral",
          "confidence": 0.82,
          "topics": ["monetary policy", "inflation", "interest rates", "labour market"],
          "entities": [
            { "name": "Central bank", "kind": "organization", "relevance": 0.95 },
            { "name": "Thursday", "kind": "other", "relevance": 0.2 }
          ],
          "isOpinion": false,
          "readingTimeMinutes": 2,
          "analyzedAt": "2026-06-10T09:30:00"
        }
        ```
        """;

    /// <summary>A provider that wraps the object in a {"result": ...} envelope.</summary>
    public const string EnvelopedJson =
        """
        {"result":{"summary":"The central bank left rates unchanged as inflation cools.","sentiment":"neutral","confidence":0.82,"topics":["monetary policy","inflation"],"entities":[{"name":"Central bank","kind":"organization","relevance":0.95}],"isOpinion":false,"readingTimeMinutes":2,"analyzedAt":"2026-06-10T09:30:00"}}
        """;
}
