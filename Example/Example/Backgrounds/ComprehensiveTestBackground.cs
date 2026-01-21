using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.Google;
using Zonit.Extensions.Ai.Mistral;
using Zonit.Extensions.Ai.DeepSeek;
using Zonit.Extensions.Ai.X;

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

            // Structured output with enums, decimals, guids
            ("OpenAI - Enum/Complex Types", async ct => await TestEnumStructuredOutput(new GPT41Mini(), "OpenAI", ct)),
            ("Anthropic - Enum/Complex Types", async ct => await TestEnumStructuredOutput(new Sonnet4(), "Anthropic", ct)),

            // Comprehensive value types test (DateTime, int, string, enum, double, decimal, Guid, bool, nullable, arrays)
            ("OpenAI - All Value Types", async ct => await TestAllValueTypes(new GPT41Mini(), "OpenAI", ct)),
            ("Anthropic - All Value Types", async ct => await TestAllValueTypesAnthropic(ct)),
            ("X Grok - All Value Types", async ct => await TestAllValueTypes(new Grok3(), "X/Grok", ct)),

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

            // Image generation tests
            ("OpenAI - Image Generation", async ct => await TestImageGeneration(new GPTImage1(), "OpenAI", ct)),
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

    private async Task TestImageGeneration(IImageLlm model, string providerName, CancellationToken ct)
    {
        var result = await provider.GenerateAsync(model, "A beautiful sunset over a calm ocean with silhouettes of palm trees", ct);

        if (!result.Value.HasValue)
            throw new Exception("Empty image response");

        if (!result.Value.IsImage)
            throw new Exception($"Generated file is not an image: {result.Value.ContentType}");

        // Save the generated image to verify it worked
        var outputPath = Path.Combine(Path.GetTempPath(), $"ai-generated-{Guid.NewGuid()}.png");
        await System.IO.File.WriteAllBytesAsync(outputPath, result.Value.Data, ct);
        Console.WriteLine($"\n      [OK] Image saved to: {outputPath} ({result.Value.Size})");
    }

    private async Task TestStructuredOutput(ILlm model, string providerName, CancellationToken ct)
    {
        var prompt = new SimplePrompt<StructuredTestResponse>("Provide a test response with name='Test', count=42, active=true");
        var result = await provider.GenerateAsync(model, prompt, ct);

        if (result.Value.Name == null || result.Value.Count == 0)
            throw new Exception($"Invalid structured response: {result.Value.Name}, {result.Value.Count}");
    }

    private async Task TestEnumStructuredOutput(ILlm model, string providerName, CancellationToken ct)
    {
        var prompt = new SimplePrompt<ProductDescription>(
            "Generate a product description for a laptop. " +
            "Use tone=Professional, category=Electronics, price=1299.99, " +
            "generate a random productId GUID, and set inStock=true, rating=4.5");

        var result = await provider.GenerateAsync(model, prompt, ct);

        Console.WriteLine($"\n      [DEBUG] Tone={result.Value.Tone}, Category={result.Value.Category}, Price={result.Value.Price}");

        if (result.Value.Tone == null)
            throw new Exception($"Enum Tone is null!");

        if (result.Value.Category == null)
            throw new Exception($"Enum Category is null!");

        if (result.Value.Price <= 0)
            throw new Exception($"Decimal Price is invalid: {result.Value.Price}");

        if (result.Value.ProductId == Guid.Empty)
            throw new Exception($"Guid ProductId is empty!");
    }

    private async Task TestImageAnalysisUrl(ILlm model, string providerName, CancellationToken ct)
    {
        // Use a reliable public test image URL - download and create Asset
        using var httpClient = new HttpClient();
        var imageBytes = await httpClient.GetByteArrayAsync("https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/PNG_transparency_demonstration_1.png/280px-PNG_transparency_demonstration_1.png", ct);
        var asset = new Asset(imageBytes, "test-image.png");

        var prompt = new SimplePrompt<string>("What objects do you see in this image? Answer briefly.")
        {
            Files = [asset]
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

    /// <summary>
    /// Anthropic-specific test - tests that Anthropic correctly parses JSON with all value types.
    /// Uses the same test as TestAllValueTypes but with error details.
    /// </summary>
    private async Task TestAllValueTypesAnthropic(CancellationToken ct)
    {
        try
        {
            await TestAllValueTypes(new Sonnet4(), "Anthropic", ct);
        }
        catch (JsonException ex)
        {
            // Re-throw with more context
            throw new Exception($"Anthropic JSON parsing failed. JsonException: {ex.Message}. Path: {ex.Path}");
        }
    }

    /// <summary>
    /// Comprehensive test for ALL value types: DateTime, int, string, enum, double, decimal, Guid, bool, nullable, arrays, etc.
    /// </summary>
    private async Task TestAllValueTypes(ILlm model, string providerName, CancellationToken ct)
    {
        var prompt = new SimplePrompt<AllValueTypesResult>(
            """
            Generate a response with ALL the following fields populated correctly:
            
            - stringValue: "Hello World"
            - intValue: 42
            - longValue: 9876543210
            - doubleValue: 3.14159
            - floatValue: 2.718
            - decimalValue: 1299.99
            - boolValue: true
            - boolFalseValue: false
            - guidValue: generate a random valid UUID/GUID
            - dateTimeValue: "2025-01-20T14:30:00" (ISO 8601 format)
            - dateOnlyValue: "2025-01-20" (date only)
            - timeOnlyValue: "14:30:00" (time only)
            - enumValue: Active (from StatusType enum)
            - enumNullable: Pending (from StatusType enum, can be null)
            - nullableInt: 100
            - nullableString: "Not null"
            - nullableDecimal: 99.99
            - emptyNullableInt: null (leave empty/null)
            - stringArray: ["apple", "banana", "cherry"]
            - intArray: [1, 2, 3, 4, 5]
            - priorityLevel: High (from PriorityLevel enum)
            - percentage: 75.5 (0-100)
            - negativeInt: -42
            - negativeDecimal: -123.45
            - zeroValue: 0
            - maxIntTest: 2147483647
            - minIntTest: -2147483648
            - unicodeString: "Zażółć gęślą jaźń 日本語 🎉"
            - emptyString: ""
            - whitespaceString: "   "
            """);

        var result = await provider.GenerateAsync(model, prompt, ct);
        var v = result.Value;

        var errors = new List<string>();

        // String validations
        if (v.StringValue != "Hello World") 
            errors.Add($"StringValue: expected 'Hello World', got '{v.StringValue}'");
        
        // Integer validations
        if (v.IntValue != 42) 
            errors.Add($"IntValue: expected 42, got {v.IntValue}");
        if (v.LongValue != 9876543210) 
            errors.Add($"LongValue: expected 9876543210, got {v.LongValue}");
        if (v.NegativeInt != -42) 
            errors.Add($"NegativeInt: expected -42, got {v.NegativeInt}");
        if (v.ZeroValue != 0) 
            errors.Add($"ZeroValue: expected 0, got {v.ZeroValue}");
        if (v.MaxIntTest != int.MaxValue) 
            errors.Add($"MaxIntTest: expected {int.MaxValue}, got {v.MaxIntTest}");
        if (v.MinIntTest != int.MinValue) 
            errors.Add($"MinIntTest: expected {int.MinValue}, got {v.MinIntTest}");

        // Floating point validations (with tolerance)
        if (Math.Abs(v.DoubleValue - 3.14159) > 0.001) 
            errors.Add($"DoubleValue: expected ~3.14159, got {v.DoubleValue}");
        if (Math.Abs(v.FloatValue - 2.718f) > 0.01) 
            errors.Add($"FloatValue: expected ~2.718, got {v.FloatValue}");
        if (Math.Abs(v.DecimalValue - 1299.99m) > 0.01m) 
            errors.Add($"DecimalValue: expected 1299.99, got {v.DecimalValue}");
        if (Math.Abs(v.NegativeDecimal - (-123.45m)) > 0.01m) 
            errors.Add($"NegativeDecimal: expected -123.45, got {v.NegativeDecimal}");
        if (Math.Abs(v.Percentage - 75.5) > 0.1) 
            errors.Add($"Percentage: expected 75.5, got {v.Percentage}");

        // Boolean validations
        if (!v.BoolValue) 
            errors.Add($"BoolValue: expected true, got false");
        if (v.BoolFalseValue) 
            errors.Add($"BoolFalseValue: expected false, got true");

        // Guid validation
        if (v.GuidValue == Guid.Empty) 
            errors.Add("GuidValue: is empty GUID");

        // DateTime validations
        if (v.DateTimeValue.Year != 2025 || v.DateTimeValue.Month != 1 || v.DateTimeValue.Day != 20)
            errors.Add($"DateTimeValue: expected 2025-01-20, got {v.DateTimeValue:yyyy-MM-dd}");
        if (v.DateOnlyValue.Year != 2025 || v.DateOnlyValue.Month != 1 || v.DateOnlyValue.Day != 20)
            errors.Add($"DateOnlyValue: expected 2025-01-20, got {v.DateOnlyValue}");
        if (v.TimeOnlyValue.Hour != 14 || v.TimeOnlyValue.Minute != 30)
            errors.Add($"TimeOnlyValue: expected 14:30:00, got {v.TimeOnlyValue}");

        // Enum validations
        if (v.EnumValue != StatusType.Active) 
            errors.Add($"EnumValue: expected Active, got {v.EnumValue}");
        if (v.EnumNullable != StatusType.Pending) 
            errors.Add($"EnumNullable: expected Pending, got {v.EnumNullable}");
        if (v.PriorityLevel != PriorityLevel.High) 
            errors.Add($"PriorityLevel: expected High, got {v.PriorityLevel}");

        // Nullable validations
        if (v.NullableInt != 100) 
            errors.Add($"NullableInt: expected 100, got {v.NullableInt}");
        if (v.NullableString != "Not null") 
            errors.Add($"NullableString: expected 'Not null', got '{v.NullableString}'");
        if (v.NullableDecimal != 99.99m) 
            errors.Add($"NullableDecimal: expected 99.99, got {v.NullableDecimal}");
        if (v.EmptyNullableInt != null) 
            errors.Add($"EmptyNullableInt: expected null, got {v.EmptyNullableInt}");

        // Array validations
        if (v.StringArray == null || v.StringArray.Length != 3 || 
            v.StringArray[0] != "apple" || v.StringArray[1] != "banana" || v.StringArray[2] != "cherry")
            errors.Add($"StringArray: expected [apple, banana, cherry], got [{string.Join(", ", v.StringArray ?? [])}]");
        if (v.IntArray == null || v.IntArray.Length != 5 || 
            !v.IntArray.SequenceEqual([1, 2, 3, 4, 5]))
            errors.Add($"IntArray: expected [1,2,3,4,5], got [{string.Join(", ", v.IntArray ?? [])}]");

        // Unicode validation
        if (string.IsNullOrEmpty(v.UnicodeString) || !v.UnicodeString.Contains("Zażółć"))
            errors.Add($"UnicodeString: expected Polish/Japanese/emoji text, got '{v.UnicodeString}'");

        // Empty/whitespace string validations
        if (v.EmptyString != "") 
            errors.Add($"EmptyString: expected empty string, got '{v.EmptyString}'");

        if (errors.Count > 0)
        {
            Console.WriteLine($"\n      [VALUE TYPE ERRORS] {errors.Count} errors:");
            foreach (var error in errors.Take(5))
                Console.WriteLine($"        - {error}");
            if (errors.Count > 5)
                Console.WriteLine($"        ... and {errors.Count - 5} more");
            throw new Exception($"Value type validation failed with {errors.Count} errors");
        }

        Console.WriteLine($"\n      [OK] All {27} value types validated successfully!");
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

/// <summary>
/// Product description with various complex types for testing.
/// </summary>
[Description("Product description with complex types")]
public class ProductDescription
{
    [Description("Product name")]
    public string? Name { get; set; }

    [Description("Product description text")]
    public string? Description { get; set; }

    [Description("Writing tone: Casual, Professional, or Friendly")]
    public ToneType? Tone { get; set; }

    [Description("Product category: Electronics, Clothing, Food, or Other")]
    public CategoryType? Category { get; set; }

    [Description("Product price in USD")]
    public decimal Price { get; set; }

    [Description("Unique product identifier")]
    public Guid ProductId { get; set; }

    [Description("Whether the product is in stock")]
    public bool InStock { get; set; }

    [Description("Product rating from 0 to 5")]
    public double Rating { get; set; }
}

/// <summary>
/// Tone type for product descriptions.
/// </summary>
public enum ToneType
{
    [Description("Casual and relaxed tone")]
    Casual,

    [Description("Professional and formal tone")]
    Professional,

    [Description("Friendly and approachable tone")]
    Friendly
}

/// <summary>
/// Product category type.
/// </summary>
public enum CategoryType
{
    [Description("Electronic devices and gadgets")]
    Electronics,

    [Description("Clothing and apparel")]
    Clothing,

    [Description("Food and beverages")]
    Food,

    [Description("Other category")]
    Other
}

/// <summary>
/// Status type for testing enum serialization.
/// </summary>
public enum StatusType
{
    [Description("Pending status - awaiting processing")]
    Pending,

    [Description("Active status - currently in progress")]
    Active,

    [Description("Completed status - finished")]
    Completed,

    [Description("Cancelled status - cancelled by user")]
    Cancelled
}

/// <summary>
/// Priority level for testing enum serialization.
/// </summary>
public enum PriorityLevel
{
    [Description("Low priority")]
    Low,

    [Description("Medium priority")]
    Medium,

    [Description("High priority")]
    High,

    [Description("Critical priority - requires immediate attention")]
    Critical
}

/// <summary>
/// Comprehensive result class for testing ALL value types.
/// This tests: string, int, long, double, float, decimal, bool, Guid, DateTime, DateOnly, TimeOnly,
/// enums, nullable types, arrays, unicode strings, empty strings, negative numbers, min/max values.
/// </summary>
[Description("Comprehensive test result with all possible value types for validating JSON schema generation and deserialization")]
public class AllValueTypesResult
{
    // Basic string
    [Description("A simple string value. Expected: 'Hello World'")]
    public string StringValue { get; set; } = "";

    // Integer types
    [Description("A 32-bit integer value. Expected: 42")]
    public int IntValue { get; set; }

    [Description("A 64-bit long integer value. Expected: 9876543210")]
    public long LongValue { get; set; }

    // Floating point types
    [Description("A double-precision floating point value. Expected: 3.14159")]
    public double DoubleValue { get; set; }

    [Description("A single-precision floating point value. Expected: 2.718")]
    public float FloatValue { get; set; }

    [Description("A decimal value for precise monetary calculations. Expected: 1299.99")]
    public decimal DecimalValue { get; set; }

    // Boolean types
    [Description("A boolean value that should be true")]
    public bool BoolValue { get; set; }

    [Description("A boolean value that should be false")]
    public bool BoolFalseValue { get; set; }

    // Guid
    [Description("A globally unique identifier (UUID). Generate a random valid GUID.")]
    public Guid GuidValue { get; set; }

    // Date/Time types
    [Description("A full date and time value in ISO 8601 format. Expected: 2025-01-20T14:30:00")]
    public DateTime DateTimeValue { get; set; }

    [Description("A date-only value without time component. Expected: 2025-01-20")]
    public DateOnly DateOnlyValue { get; set; }

    [Description("A time-only value without date component. Expected: 14:30:00")]
    public TimeOnly TimeOnlyValue { get; set; }

    // Enum types
    [Description("Status enum value. Expected: Active")]
    public StatusType EnumValue { get; set; }

    [Description("Nullable status enum value. Expected: Pending")]
    public StatusType? EnumNullable { get; set; }

    [Description("Priority level enum. Expected: High")]
    public PriorityLevel PriorityLevel { get; set; }

    // Nullable types
    [Description("A nullable integer with a value. Expected: 100")]
    public int? NullableInt { get; set; }

    [Description("A nullable string with a value. Expected: 'Not null'")]
    public string? NullableString { get; set; }

    [Description("A nullable decimal with a value. Expected: 99.99")]
    public decimal? NullableDecimal { get; set; }

    [Description("A nullable integer that should be null/empty")]
    public int? EmptyNullableInt { get; set; }

    // Array types
    [Description("An array of strings. Expected: ['apple', 'banana', 'cherry']")]
    public string[] StringArray { get; set; } = [];

    [Description("An array of integers. Expected: [1, 2, 3, 4, 5]")]
    public int[] IntArray { get; set; } = [];

    // Percentage/range values
    [Description("A percentage value between 0 and 100. Expected: 75.5")]
    public double Percentage { get; set; }

    // Negative values
    [Description("A negative integer. Expected: -42")]
    public int NegativeInt { get; set; }

    [Description("A negative decimal. Expected: -123.45")]
    public decimal NegativeDecimal { get; set; }

    // Edge cases
    [Description("A zero value. Expected: 0")]
    public int ZeroValue { get; set; }

    [Description("Maximum 32-bit integer value. Expected: 2147483647")]
    public int MaxIntTest { get; set; }

    [Description("Minimum 32-bit integer value. Expected: -2147483648")]
    public int MinIntTest { get; set; }

    // Unicode and special strings
    [Description("A string with Unicode characters including Polish, Japanese, and emoji. Expected: 'Zażółć gęślą jaźń 日本語 🎉'")]
    public string UnicodeString { get; set; } = "";

    [Description("An empty string. Expected: '' (empty)")]
    public string EmptyString { get; set; } = "";

    [Description("A string containing only whitespace. Expected: '   ' (three spaces)")]
    public string WhitespaceString { get; set; } = "";
}
