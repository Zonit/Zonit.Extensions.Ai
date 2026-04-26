using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Anthropic;

namespace Example.Backgrounds;

/// <summary>
/// Test for ProductDescription prompt — exercises typed structured-output generation
/// via the registered <see cref="IModelProvider"/> chain.
/// </summary>
public class ProductDescriptionTestBackground : BackgroundService
{
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly ILogger<ProductDescriptionTestBackground> _logger;

    public ProductDescriptionTestBackground(
        IEnumerable<IModelProvider> providers,
        ILogger<ProductDescriptionTestBackground> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(500, ct);

        Console.WriteLine("=== ProductDescription via IAiProvider (DI) ===\n");
        await TestWithProvider(ct);

        Environment.Exit(0);
    }

    private async Task TestWithProvider(CancellationToken ct)
    {
        Console.WriteLine("Testing ProductDescription with AnthropicProvider (DI)...\n");

        try
        {
            var prompt = new ProductDescriptionPrompt
            {
                Culture = "pl-PL",
                DescriptionLength = CategoryLengthType.Standard,
                Specification = "Laptop 15.6\", Intel i7, 16GB RAM, 512GB SSD",
                Notes = "Focus on gaming capabilities"
            };

            var provider = _providers.OfType<AnthropicProvider>().FirstOrDefault()
                ?? throw new InvalidOperationException("AnthropicProvider not registered");
            Console.WriteLine($"Using provider: {provider.GetType().Name}");

            var llm = new Sonnet4();
            var response = await provider.GenerateAsync<ProductDescriptionResult>(llm, prompt, ct);

            var result = response.Value;
            Console.WriteLine("\n=== Provider SUCCESS ===");
            Console.WriteLine($"Title: {result!.Title}");
            Console.WriteLine($"ShortDescription: {result.ShortDescription}");
            Console.WriteLine($"Content length: {result.Content?.Length ?? 0} chars");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n=== Provider FAILED ===");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"\nFull exception:\n{ex}");
        }
    }
}

/// <summary>
/// Target description length
/// </summary>
public enum CategoryLengthType
{
    Compact,
    Standard,
    Detailed
}

/// <summary>
/// AI prompt for generating e-commerce product descriptions from product images.
/// </summary>
public class ProductDescriptionPrompt : PromptBase<ProductDescriptionResult>
{
    public required string Culture { get; init; }
    public string? Specification { get; init; }
    public string? Notes { get; init; }
    public CategoryLengthType DescriptionLength { get; init; } = CategoryLengthType.Standard;

    public override string Prompt => $"""
        Generate an e-commerce product description in {Culture} language.
        
        Technical specification: {Specification ?? "Not provided"}
        Additional notes: {Notes ?? "None"}
        Target length: {DescriptionLength}
        
        Create a compelling product description with SEO-optimized title, meta description, and detailed content.
        The content should be in Markdown format but WITHOUT any headings (no # symbols).
        Use bullet points and formatting to make it readable.
        """;
}

/// <summary>
/// Result of AI product description generation
/// </summary>
[Description("Product description result with SEO-optimized content")]
public class ProductDescriptionResult
{
    [Description("SEO product title (40-60 characters). Clear, descriptive, include key features. No store name.")]
    public required string Title { get; init; }

    [Description("SEO meta description (120-160 characters). Format: [Main benefit] + [Key feature] + [Soft CTA]. No store name.")]
    public required string ShortDescription { get; init; }

    [Description("Full Markdown product description. No headings (#), clean bullets, benefits over features, soft CTA closing.")]
    public required string Content { get; init; }
}
