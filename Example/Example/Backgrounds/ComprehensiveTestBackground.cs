using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.Google;
using Zonit.Extensions.Ai.Mistral;
using Zonit.Extensions.Ai.DeepSeek;
using Zonit.Extensions.Ai.X;
using File = Zonit.Extensions.Ai.File;

namespace Example.Backgrounds;

/// <summary>
/// Comprehensive test background service for testing all AI providers.
/// </summary>
internal class ComprehensiveTestBackground(IAiProvider provider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       Zonit.Extensions.Ai - Comprehensive Test Suite          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");

        var tests = new (string Name, Func<CancellationToken, Task> Test)[]
        {
            // Text generation tests
            ("OpenAI GPT-4.1 - Text", async ct => await TestTextGeneration(new GPT41(), "OpenAI", ct)),
            ("OpenAI GPT-4.1 Mini - Text", async ct => await TestTextGeneration(new GPT41Mini(), "OpenAI Mini", ct)),
            ("Anthropic Claude 4 - Text", async ct => await TestTextGeneration(new Sonnet4(), "Anthropic", ct)),
            ("Google Gemini 2.5 - Text", async ct => await TestTextGeneration(new Gemini25Flash(), "Google", ct)),
            ("Mistral Large - Text", async ct => await TestTextGeneration(new MistralLarge(), "Mistral", ct)),
            ("DeepSeek V3 - Text", async ct => await TestTextGeneration(new DeepSeekV3(), "DeepSeek", ct)),
            ("X Grok 3 - Text", async ct => await TestTextGeneration(new Grok3(), "X/Grok", ct)),

            // Structured output (JSON Schema) tests
            ("OpenAI - Structured Output", async ct => await TestStructuredOutput(new GPT41Mini(), "OpenAI", ct)),
            ("Anthropic - Structured Output", async ct => await TestStructuredOutput(new Sonnet4(), "Anthropic", ct)),
            ("Google - Structured Output", async ct => await TestStructuredOutput(new Gemini25Flash(), "Google", ct)),

            // Image analysis tests (using URL instead of bytes for reliability)
            ("OpenAI - Image Analysis", async ct => await TestImageAnalysisUrl(new GPT41(), "OpenAI", ct)),
            ("Anthropic - Image Analysis", async ct => await TestImageAnalysisUrl(new Sonnet4(), "Anthropic", ct)),
            ("Google - Image Analysis", async ct => await TestImageAnalysisUrl(new Gemini25Flash(), "Google", ct)),

            // Streaming tests
            ("Anthropic - Streaming", async ct => await TestStreaming(new Sonnet4(), "Anthropic", ct)),

            // Embedding tests
            ("OpenAI - Embeddings", async ct => await TestEmbeddings(new TextEmbedding3Small(), "OpenAI", ct)),
            ("Google - Embeddings", async ct => await TestEmbeddings(new TextEmbedding004(), "Google", ct)),
            ("Mistral - Embeddings", async ct => await TestEmbeddings(new MistralEmbed(), "Mistral", ct)),

            // Web search tests
            ("OpenAI - Web Search", async ct => await TestWebSearch(new GPT41 { Tools = [new WebSearchTool()] }, "OpenAI", ct)),
            ("X Grok - Web Search", async ct => await TestGrokWebSearch(ct)),

            // Reasoning tests
            ("OpenAI o3-mini - Reasoning", async ct => await TestReasoning(new O3Mini(), "OpenAI o3", ct)),
            ("Anthropic Claude - Extended Thinking", async ct => await TestAnthropicThinking(ct)),
            ("DeepSeek R1 - Reasoning", async ct => await TestReasoning(new DeepSeekR1(), "DeepSeek R1", ct)),
        };

        var passed = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var (name, test) in tests)
        {
            Console.Write($"  [{passed + failed + skipped + 1:D2}/{tests.Length}] {name,-40} ");

            try
            {
                await test(stoppingToken);
                Console.WriteLine("✓ PASS");
                passed++;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("403") || ex.Message.Contains("Unauthorized"))
            {
                Console.WriteLine("○ SKIP (no API key)");
                skipped++;
            }
            catch (NotSupportedException)
            {
                Console.WriteLine("○ SKIP (not supported)");
                skipped++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAIL");
                Console.WriteLine($"      Error: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  Results: {passed} passed, {failed} failed, {skipped} skipped" + new string(' ', 28 - passed.ToString().Length - failed.ToString().Length - skipped.ToString().Length) + "║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");

        // Stop the host
        Environment.Exit(failed > 0 ? 1 : 0);
    }

    private async Task TestTextGeneration(ILlm model, string providerName, CancellationToken ct)
    {
        var prompt = new SimplePrompt<string>("Say 'Hello' in 3 words or less.");
        var result = await provider.GenerateAsync(model, prompt, ct);

        if (string.IsNullOrEmpty(result.Value))
            throw new Exception("Empty response");
    }

    private async Task TestStructuredOutput(ILlm model, string providerName, CancellationToken ct)
    {
        var prompt = new SimplePrompt<StructuredTestResponse>("Provide a test response with name='Test', count=42, active=true");
        var result = await provider.GenerateAsync(model, prompt, ct);

        if (result.Value.Name == null || result.Value.Count == 0)
            throw new Exception($"Invalid structured response: {result.Value.Name}, {result.Value.Count}");
    }

    private async Task TestImageAnalysisUrl(ILlm model, string providerName, CancellationToken ct)
    {
        // Use a reliable public test image URL
        var file = await File.FromUrlAsync("https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/PNG_transparency_demonstration_1.png/280px-PNG_transparency_demonstration_1.png", null, ct);

        var prompt = new SimplePrompt<string>("What objects do you see in this image? Answer briefly.")
        {
            Files = [file]
        };

        var result = await provider.GenerateAsync(model, prompt, ct);

        if (string.IsNullOrEmpty(result.Value))
            throw new Exception("Empty response");
    }

    private async Task TestStreaming(ILlm model, string providerName, CancellationToken ct)
    {
        var chunks = new List<string>();

        await foreach (var chunk in provider.StreamAsync(model, "Count: 1, 2, 3", ct))
        {
            chunks.Add(chunk);
        }

        if (chunks.Count == 0)
            throw new Exception("No chunks received");
    }

    private async Task TestEmbeddings(IEmbeddingLlm model, string providerName, CancellationToken ct)
    {
        var result = await provider.GenerateAsync(model, "Hello world", ct);

        if (result.Value.Length == 0)
            throw new Exception("Empty embedding");
    }

    private async Task TestWebSearch(ILlm model, string providerName, CancellationToken ct)
    {
        var prompt = new SimplePrompt<string>("What is today's date?");
        var result = await provider.GenerateAsync(model, prompt, ct);

        if (string.IsNullOrEmpty(result.Value))
            throw new Exception("Empty response");
    }

    private async Task TestGrokWebSearch(CancellationToken ct)
    {
        var model = new Grok3
        {
            WebSearch = new Search { Mode = ModeType.Auto }
        };

        var prompt = new SimplePrompt<string>("What is the current time in Warsaw?");
        var result = await provider.GenerateAsync(model, prompt, ct);

        if (string.IsNullOrEmpty(result.Value))
            throw new Exception("Empty response");
    }

    private async Task TestReasoning(ILlm model, string providerName, CancellationToken ct)
    {
        var prompt = new SimplePrompt<string>("What is 15 + 27? Just the number.");
        var result = await provider.GenerateAsync(model, prompt, ct);

        if (string.IsNullOrEmpty(result.Value) || !result.Value.Contains("42"))
            throw new Exception($"Expected 42, got: {result.Value}");
    }

    private async Task TestAnthropicThinking(CancellationToken ct)
    {
        // ThinkingBudget must be less than MaxOutputTokens (64000 for Sonnet4)
        var model = new Sonnet4
        {
            ThinkingBudget = 2048
        };

        var prompt = new SimplePrompt<string>("What is 100 / 4? Just the number.");
        var result = await provider.GenerateAsync(model, prompt, ct);

        if (string.IsNullOrEmpty(result.Value))
            throw new Exception("Empty response");
    }
}

/// <summary>
/// Test response for structured output tests.
/// </summary>
[Description("Structured test response")]
public class StructuredTestResponse
{
    [Description("Name of the test")]
    public string? Name { get; set; }

    [Description("Count value")]
    public int Count { get; set; }

    [Description("Whether active")]
    public bool Active { get; set; }
}
