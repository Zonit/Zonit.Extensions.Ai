# Zonit.Extensions.Ai

A .NET library for integrating with AI models (OpenAI GPT, X Grok) in applications, providing a simple and flexible architecture for text and image generation.

## Main Features

- **OpenAI Model Support**: GPT-4, GPT-4 Turbo, reasoning models (o1, o3), DALL·E image generation  
- **X (Grok) Model Support**: Grok-3, Grok-3 Fast, Grok-3 Mini with advanced web search capabilities  
- **Flexible Architecture**: Interface-driven, based on Clean Architecture  
- **Type-safe Prompts**: Strongly-typed prompts with automatic JSON Schema validation  
- **AI Tools**: Web search, file search with configuration  
- **File Management**: Built-in support for images and documents  
- **Metadata Tracking**: Automatic logging of cost, token counts, and execution time  
- **Modern Resilience**: Built-in retry policies, circuit breakers, and configurable timeouts using .NET Resilience

## Installation

```powershell
Install-Package Zonit.Extensions.Ai
```

## Configuration

### 1. Register Services

```csharp
services.AddAi(options =>
{
    options.OpenAiKey = "your-openai-api-key";
    options.XKey = "your-x-api-key";
});
```

### 2. appsettings.json

```json
{
  "Ai": {
    "OpenAiKey": "your-openai-api-key",
    "XKey": "your-x-api-key",
    "Resilience": {
      "HttpClientTimeout": "00:30:00",
      "TotalRequestTimeout": "00:25:00", 
      "AttemptTimeout": "00:20:00",
      "Retry": {
        "MaxRetryAttempts": 3,
        "BaseDelay": "00:00:02",
        "MaxDelay": "00:00:30",
        "UseJitter": true
      },
      "CircuitBreaker": {
        "FailureRatio": 0.5,
        "MinimumThroughput": 10,
        "SamplingDuration": "00:02:00",
        "BreakDuration": "00:00:30"
      }
    }
```

The library uses **Microsoft.Extensions.Http.Resilience** (the modern replacement for Polly) to provide robust error handling and retry mechanisms for all AI providers.

### Unified Configuration

**All AI providers** (OpenAI, X AI, etc.) now use the **same resilience configuration**, eliminating the need for provider-specific settings. This simplifies configuration and ensures consistent behavior across all services.

### Improved Default Timeouts

- **HTTP Client Timeout**: 30 minutes (increased from 15 minutes)
- **Total Request Timeout**: 25 minutes (including all retries)  
- **Attempt Timeout**: 20 minutes (per individual request)

These generous timeouts accommodate AI model processing times, especially for complex reasoning models.

### Intelligent Retry Strategy

- **Max Retry Attempts**: 3 retries (4 total attempts)
- **Exponential Backoff**: Starts at 2 seconds, doubles each time (2s ? 4s ? 8s ? 16s ? 30s max)
- **Jitter**: Randomizes delays to prevent retry storms
- **Max Delay**: 30 seconds to prevent excessive wait times

### Circuit Breaker Protection

- **Failure Ratio**: 50% failure threshold to open circuit
- **Minimum Throughput**: 10 requests before activation
- **Sampling Duration**: 2 minutes for failure rate calculation
- **Break Duration**: 30 seconds before attempting to close

### Example Retry Behavior
Request 1: Fails ? Wait 2 seconds (+ jitter)
Request 2: Fails ? Wait 4 seconds (+ jitter) 
Request 3: Fails ? Wait 8 seconds (+ jitter)
Request 4: Success ?
If all attempts fail, the request is rejected with detailed error information.

## Basic Usage

### Text Generation

```csharp
public class TranslatePrompt : PromptBase<TranslateResponse>
{
    public string Content { get; set; }
    public string Language { get; set; }
    public string Culture { get; set; }

    public override string Prompt =>
        $"Translate the text '{Content}' into {Language} (culture: {Culture})";
}

public class TranslateResponse
{
    public string TranslatedText { get; set; }
    public string DetectedLanguage { get; set; }
}

// Usage:
var prompt = new TranslatePrompt
{
    Content = "Hello world!",
    Language = "pl",
    Culture = "pl-PL"
};

var result = await aiClient.GenerateAsync(prompt, new GPT4());
Console.WriteLine(result.Value.TranslatedText);
```

### Image Generation

```csharp
public class AnimalPrompt : PromptBase<byte[]>
{
    public string Animal { get; set; }

    public override string Prompt =>
        $"Generate an image depicting a {Animal}";
}

// Usage:
var prompt = new AnimalPrompt { Animal = "dog" };
var result = await aiClient.GenerateAsync(
    prompt,
    new GPTImage1
    {
        Quality = GPTImage1.QualityType.High,
        Size    = GPTImage1.SizeType.Square
    }
);

// Save the image:
await File.WriteAllBytesAsync("dog.png", result.Value.Data);
```

## AI Models

### Text Models

```csharp
// Basic GPT-4
var gpt4 = new GPT4();

// GPT-4 Turbo with custom settings
var gpt4Turbo = new GPT4Turbo
{
    Temperature = 0.7,
    TopP        = 0.9,
    MaxTokens   = 2000
};

// Reasoning model (o1)
var reasoning = new GPT4Reasoning
{
    Reason        = OpenAiReasoningBase.ReasonType.High,
    ReasonSummary = OpenAiReasoningBase.ReasonSummaryType.Detailed
};
```

// X AI (Grok) models - now with same resilience as OpenAI
var grok = new Grok4
{
    MaxTokens = 4000,
    WebSearch = new Search 
    { 
        Mode = ModeType.Auto,
        Citations = true 
    }
};
### Image Models

```csharp
var imageModel = new GPTImage1
{
    Quality = GPTImage1.QualityType.High,
    Size    = GPTImage1.SizeType.Landscape,
    Style   = GPTImage1.StyleType.Natural
};
```

## AI Tools

### Web Search

```csharp
public class SearchPrompt : PromptBase<SearchResponse>
{
    public string Query { get; set; }

    public override string Prompt => $"Search for information about: {Query}";

    public override IReadOnlyList<ITool> Tools =>
        new List<ITool>
        {
            new WebSearchTool
            {
                ContextSize = WebSearchTool.ContextSizeType.High
            }
        };

    public override ToolsType ToolChoice => ToolsType.WebSearch;
}
```

### File Search

```csharp
public override IReadOnlyList<ITool> Tools =>
    new List<ITool> { new FileSearchTool() };

public override ToolsType ToolChoice => ToolsType.FileSearch;
```

## File Management

### Create a File from Data

```csharp
var fileData = await File.ReadAllBytesAsync("document.pdf");
var file = new FileModel("document.pdf", "application/pdf", fileData);
```

### Create a File from Path

```csharp
var file = await FileModel.CreateFromFilePathAsync("path/to/image.jpg");
```

### Save a File

```csharp
await file.SaveToFileAsync("output/saved-file.jpg");
```

### Supported Formats

- Images: JPG, PNG, GIF, BMP, WebP  
- Documents: PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX  
- Text: TXT, JSON, CSV, XML  

## Metadata & Costs

```csharp
var result = await aiClient.GenerateAsync(prompt, model);

// Cost information
Console.WriteLine($"Input cost: ${result.MetaData.PriceInput:F6}");
Console.WriteLine($"Output cost: ${result.MetaData.PriceOutput:F6}");
Console.WriteLine($"Total cost: ${result.MetaData.PriceTotal:F6}");

// Token counts
Console.WriteLine($"Input tokens: {result.MetaData.InputTokenCount}");
Console.WriteLine($"Output tokens: {result.MetaData.OutputTokenCount}");

// Execution time
Console.WriteLine($"Duration: {result.MetaData.Duration.TotalSeconds:F2}s");
```

## Advanced Features

### Logging Responses

```csharp
var model = new GPT4
{
    StoreLogs = true
};
```

### Custom JSON Schema

The library automatically generates JSON Schema from response classes. You can add descriptions with attributes:

```csharp
[Description("Response containing the translated text")]
public class TranslateResponse
{
    [Description("The text translated into the target language")]
    public string TranslatedText { get; set; }

    [Description("The detected source language")]
    public string DetectedLanguage { get; set; }
}
```

## Error Handling

```csharp
try
{
    var result = await aiClient.GenerateAsync(prompt, model);
    // Use result.Value
}
catch (JsonException ex)
{
    // JSON parsing error
    Console.WriteLine($"Failed to parse response: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    // API communication error (after all retries exhausted)
    Console.WriteLine($"API request failed: {ex.Message}");
}
```

The library logs resilience events for monitoring:
[INFO] Resilience event occurred. EventName: 'OnRetry', Attempt: '1'
[WARN] Resilience event occurred. EventName: 'OnTimeout', Source: 'OpenAiRepository-standard'
[ERROR] Resilience event occurred. EventName: 'OnCircuitBreakerOpened'
## Architecture

The library follows Clean Architecture with layered separation:

- **Abstractions**: Interfaces and contracts  
- **Domain**: Domain models and business logic  
- **Application**: Application services and configuration  
- **Infrastructure**: Repository implementations (OpenAI, X AI)  
- **LLM**: Language model definitions  

## Complete Application Example

```csharp
public class AiService
{
    private readonly IAiClient _aiClient;

    public AiService(IAiClient aiClient)
    {
        _aiClient = aiClient;
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage)
    {
        var prompt = new TranslatePrompt
        {
            Content = text,
            Language = targetLanguage,
            Culture  = $"{targetLanguage}-{targetLanguage.ToUpper()}"
        };

        var result = await _aiClient.GenerateAsync(prompt, new GPT4Turbo());
        return result.Value.TranslatedText;
    }

    public async Task<byte[]> GenerateImageAsync(string description)
    {
        var prompt = new ImagePrompt { Description = description };

        var result = await _aiClient.GenerateAsync(
            prompt,
            new GPTImage1
            {
                Quality = GPTImage1.QualityType.High,
                Size    = GPTImage1.SizeType.Square
            }
        );

        return result.Value.Data;
    }

    public async Task<string> SearchWithGrokAsync(string query)
    {
        var prompt = new SearchPrompt { Query = query };

        // Grok with web search - now uses same resilience as OpenAI
        var result = await _aiClient.GenerateAsync(
            prompt, 
            new Grok4 
            { 
                WebSearch = new Search 
                { 
                    Mode = ModeType.Always,
                    Citations = true,
                    MaxResults = 10
                }
            }
        );

        return result.Value.Answer;
    }
}
```