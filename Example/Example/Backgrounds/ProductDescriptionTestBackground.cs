using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.Converters;

namespace Example.Backgrounds;

/// <summary>
/// Test for ProductDescription prompt - reproduces the '#' JSON parsing error.
/// </summary>
public class ProductDescriptionTestBackground : BackgroundService
{
    private readonly IOptions<AnthropicOptions> _options;
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly ILogger<ProductDescriptionTestBackground> _logger;

    public ProductDescriptionTestBackground(
        IOptions<AnthropicOptions> options,
        IEnumerable<IModelProvider> providers,
        ILogger<ProductDescriptionTestBackground> logger)
    {
        _options = options;
        _providers = providers;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(500, ct);

        Console.WriteLine("=== Test 1: Direct HTTP (bypassing Polly) ===\n");
        await TestDirectHttp(ct);

        Console.WriteLine("\n\n=== Test 2: Using IAiProvider from DI ===\n");
        await TestWithProvider(ct);

        Environment.Exit(0);
    }

    private async Task TestDirectHttp(CancellationToken ct)
    {
        await Task.Delay(500, ct);

        Console.WriteLine("Testing ProductDescription with Anthropic (direct HTTP)...\n");

        try
        {
            var prompt = new ProductDescriptionPrompt
            {
                Culture = "pl-PL",
                DescriptionLength = CategoryLengthType.Standard,
                Specification = "Laptop 15.6\", Intel i7, 16GB RAM, 512GB SSD",
                Notes = "Focus on gaming capabilities"
            };

            // Build request directly
            var schema = JsonSchemaGenerator.Generate(typeof(ProductDescriptionResult));
            var schemaJson = schema.ToString();

            var systemPrompt = $"You must respond ONLY with valid JSON matching this schema (no markdown, no explanation, no code blocks):\n{schemaJson}\n\nRespond with raw JSON only.";

            var requestBody = new
            {
                model = "claude-sonnet-4-20250514",
                max_tokens = 4096,
                system = systemPrompt,
                messages = new object[]
                {
                    new { role = "user", content = prompt.Prompt },
                    new { role = "assistant", content = "{" }  // Prefill
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            Console.WriteLine("Request body:");
            Console.WriteLine(json[..Math.Min(500, json.Length)] + "...\n");

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            httpClient.DefaultRequestHeaders.Add("x-api-key", _options.Value.ApiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            Console.WriteLine("Sending request to Anthropic...");
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync("https://api.anthropic.com/v1/messages", content, ct);

            var responseJson = await response.Content.ReadAsStringAsync(ct);

            Console.WriteLine($"\nResponse status: {response.StatusCode}");
            Console.WriteLine($"Response body:\n{responseJson[..Math.Min(1000, responseJson.Length)]}...\n");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API error: {responseJson}");
            }

            // Parse response
            var anthropicResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
            var textContent = anthropicResponse
                .GetProperty("content")
                .EnumerateArray()
                .First(c => c.GetProperty("type").GetString() == "text")
                .GetProperty("text")
                .GetString();

            Console.WriteLine($"Raw text content:\n{textContent}\n");

            // Add back the prefill "{" 
            var jsonContent = "{" + textContent;
            Console.WriteLine($"JSON to parse:\n{jsonContent[..Math.Min(300, jsonContent.Length)]}...\n");

            // Parse to ProductDescriptionResult
            var result = JsonSerializer.Deserialize<ProductDescriptionResult>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                Converters = { new CaseInsensitiveEnumConverterFactory() }
            });

            Console.WriteLine("\n=== Direct HTTP SUCCESS ===");
            Console.WriteLine($"Title: {result!.Title}");
            Console.WriteLine($"ShortDescription: {result.ShortDescription}");
            Console.WriteLine($"Content length: {result.Content?.Length ?? 0} chars");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n=== Direct HTTP FAILED ===");
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"\nFull exception:\n{ex}");
        }
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
