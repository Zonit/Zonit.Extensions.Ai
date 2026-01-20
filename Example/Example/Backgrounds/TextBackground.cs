using Microsoft.Extensions.Hosting;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.OpenAi;

namespace Example.Backgrounds;

/// <summary>
/// Example background service demonstrating the new AI API.
/// </summary>
internal class TextBackground(IAiProvider provider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("=== Zonit.Extensions.Ai Example ===\n");
        
        // ===== Example 1: Typed prompt with Scriban templating =====
        Console.WriteLine("1. Testing typed prompt with templating...\n");
        
        var testPrompt = new TestPrompt
        {
            TestString = "Hello from new API!",
            TestNumber = 100,
            IsEnabled = true
        };
        
        // No generic needed - type comes from prompt!
        var result = await provider.GenerateAsync(new GPT41(), testPrompt, stoppingToken);
        
        Console.WriteLine($"Summary: {result.Value.Summary}");
        Console.WriteLine($"Number: {result.Value.TestNumber}");
        Console.WriteLine($"Was enabled: {result.Value.WasEnabled}");
        Console.WriteLine($"Tokens: {result.Usage.InputTokens} in / {result.Usage.OutputTokens} out");
        Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds:F0}ms\n");
        
        // ===== Example 2: Simple prompt (quick usage) =====
        Console.WriteLine("2. Testing simple prompt...\n");
        
        var simplePrompt = new SimplePrompt<TranslationResponse>("Translate 'Hello World' to Polish and Spanish.");
        var translation = await provider.GenerateAsync(new GPT41Mini(), simplePrompt, stoppingToken);
        
        Console.WriteLine($"Polish: {translation.Value.Polish}");
        Console.WriteLine($"Spanish: {translation.Value.Spanish}\n");
        
        // ===== Example 3: Image analysis with files =====
        Console.WriteLine("3. Testing image generation...\n");
        
        try
        {
            var image = await provider.GenerateAsync(
                new GPTImage1 { Quality = ImageQuality.Standard, Size = ImageSize.Square }, 
                "A cute robot reading a book in a cozy library.", 
                stoppingToken);
            
            // Save image
            var path = Path.Combine(Path.GetTempPath(), $"ai_image_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            await image.Value.SaveAsync(path, stoppingToken);
            Console.WriteLine($"Image saved to: {path}");
            
            // Analyze the generated image
            Console.WriteLine("\n4. Analyzing generated image...\n");
            
            var analysisPrompt = new SimplePrompt<ImageAnalysisResponse>("Describe what you see in this image.")
            {
                Files = [image.Value]
            };
            
            var analysis = await provider.GenerateAsync(new GPT41(), analysisPrompt, stoppingToken);
            Console.WriteLine($"Description: {analysis.Value.Description}");
            Console.WriteLine($"Objects: {string.Join(", ", analysis.Value.MainObjects ?? [])}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Image test skipped: {ex.Message}");
        }
        
        // ===== Example 4: Streaming =====
        Console.WriteLine("\n5. Testing streaming...\n");
        
        Console.Write("Streaming: ");
        await foreach (var chunk in provider.StreamAsync(new GPT41Nano(), "Count from 1 to 5, one number per line.", stoppingToken))
        {
            Console.Write(chunk);
        }
        Console.WriteLine("\n");
        
        // ===== Example 5: Model with tools =====
        Console.WriteLine("6. Testing model with web search tool...\n");
        
        var searchModel = new GPT41
        {
            Tools = [new WebSearchTool { ContextSize = WebSearchTool.ContextSizeType.Medium }]
        };
        
        var newsPrompt = new SimplePrompt<NewsResponse>("What are the latest AI news today?");
        var news = await provider.GenerateAsync(searchModel, newsPrompt, stoppingToken);
        
        Console.WriteLine($"Headlines: {string.Join(", ", news.Value.Headlines?.Take(3) ?? [])}");
        
        Console.WriteLine("\n=== All tests completed! ===");
    }
}

// Response types for examples
public class TranslationResponse
{
    public string? Polish { get; set; }
    public string? Spanish { get; set; }
}

public class ImageAnalysisResponse
{
    public string? Description { get; set; }
    public List<string>? MainObjects { get; set; }
}

public class NewsResponse
{
    public List<string>? Headlines { get; set; }
    public string? Summary { get; set; }
}
