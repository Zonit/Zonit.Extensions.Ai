# Zonit.Extensions.Ai

A .NET library for integrating with multiple AI providers (OpenAI, Anthropic Claude, Google Gemini, X Grok, DeepSeek, Mistral) with Scriban templating, type-safe prompts, and built-in resilience.

---

## NuGet Packages

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| **Zonit.Extensions.Ai** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.svg) | Core library with prompts and DI |
| **Zonit.Extensions.Ai.Abstractions** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Abstractions.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Abstractions.svg) | Interfaces and contracts |
| **Zonit.Extensions.Ai.OpenAi** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.OpenAi.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.OpenAi.svg) | OpenAI provider (GPT-5, O3/O4, DALL-E) |
| **Zonit.Extensions.Ai.Anthropic** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Anthropic.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Anthropic.svg) | Anthropic provider (Claude 4.5) |
| **Zonit.Extensions.Ai.Google** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Google.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Google.svg) | Google provider (Gemini 2.5/3) |
| **Zonit.Extensions.Ai.X** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.X.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.X.svg) | X provider (Grok 4) |
| **Zonit.Extensions.Ai.DeepSeek** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.DeepSeek.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.DeepSeek.svg) | DeepSeek provider (V3, R1) |
| **Zonit.Extensions.Ai.Mistral** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Mistral.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Mistral.svg) | Mistral provider (Large, Codestral) |
| **Zonit.Extensions.Ai.Prompts** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Prompts.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Prompts.svg) | Ready-to-use example prompts |

```powershell
# Core library
dotnet add package Zonit.Extensions.Ai

# Add providers you need
dotnet add package Zonit.Extensions.Ai.OpenAi
dotnet add package Zonit.Extensions.Ai.Anthropic
dotnet add package Zonit.Extensions.Ai.Google
dotnet add package Zonit.Extensions.Ai.X
dotnet add package Zonit.Extensions.Ai.DeepSeek
dotnet add package Zonit.Extensions.Ai.Mistral

# Or via NuGet Package Manager
Install-Package Zonit.Extensions.Ai
Install-Package Zonit.Extensions.Ai.OpenAi
```

---

## Features

- **Multi-provider** - OpenAI, Anthropic, Google, X, DeepSeek, Mistral with unified API
- **Type-safe prompts** - Strongly typed responses with JSON Schema
- **Scriban templating** - Dynamic prompts with variables and conditions
- **Cost calculation** - Estimate costs before calling API
- **Resilience** - Retry, circuit breaker, timeout with Microsoft.Extensions.Http.Resilience
- **Plugin architecture** - Auto-discovery of providers, idempotent registration with `TryAddEnumerable`
- **Clean architecture** - SOLID principles, each provider self-contained with own Options and DI
- **Best practices** - `BindConfiguration` + `PostConfigure` pattern for configuration
- **Separation of concerns** - Provider-specific options separated from global configuration

---

## Requirements

- .NET 8.0, 9.0, or 10.0

---

## Quick Start

### 1. Configuration-based (Recommended)

Register providers using `appsettings.json` configuration:

```json
// appsettings.json
{
  "Ai": {
    "Resilience": {
      "MaxRetryAttempts": 3,
      "HttpClientTimeout": "00:05:00"
    },
    "OpenAi": {
      "ApiKey": "sk-..."
    },
    "Anthropic": {
      "ApiKey": "sk-ant-..."
    },
    "Google": {
      "ApiKey": "AIza..."
    },
    "X": {
      "ApiKey": "xai-..."
    },
    "DeepSeek": {
      "ApiKey": "sk-..."
    },
    "Mistral": {
      "ApiKey": "..."
    }
  }
}
```

```csharp
// Program.cs - Configuration is automatically loaded via BindConfiguration
services.AddAiOpenAi();      // Loads from "Ai:OpenAi"
services.AddAiAnthropic();   // Loads from "Ai:Anthropic"
services.AddAiGoogle();      // Loads from "Ai:Google"
services.AddAiX();           // Loads from "Ai:X"
services.AddAiDeepSeek();    // Loads from "Ai:DeepSeek"
services.AddAiMistral();     // Loads from "Ai:Mistral"
```

### 2. Code-based Configuration

Override or supplement configuration in code:

```csharp
// With API key only
services.AddAiOpenAi("sk-...");

// With full configuration (applied after appsettings.json via PostConfigure)
services.AddAiOpenAi(options =>
{
    options.ApiKey = "sk-...";
    options.OrganizationId = "org-...";
    options.BaseUrl = "https://custom-endpoint.com";
});

// Multiple providers
services.AddAiOpenAi("sk-...");
services.AddAiAnthropic("sk-ant-...");
services.AddAiGoogle("AIza...");
```

### 3. Global Resilience Configuration

Configure retry/timeout behavior for all providers:

```csharp
services.AddAi(options =>
{
    options.Resilience.MaxRetryAttempts = 5;
    options.Resilience.HttpClientTimeout = TimeSpan.FromMinutes(10);
    options.Resilience.RetryBaseDelay = TimeSpan.FromSeconds(3);
});

// Then add providers
services.AddAiOpenAi();
services.AddAiAnthropic();
```

### 4. Plugin Architecture

Each provider registration is idempotent and can be called from multiple plugins:

```csharp
// Plugin A
services.AddAiOpenAi();  // Registers OpenAI + core services

// Plugin B (safe - uses TryAddEnumerable internally)
services.AddAiAnthropic();  // Only adds Anthropic, doesn't duplicate core

// Plugin C
services.AddAi(options =>   // Safe - configures existing registration
{
    options.Resilience.MaxRetryAttempts = 5;
});
```

### Configuration Best Practices

✅ **Recommended:**
```csharp
// appsettings.json for sensitive data (use User Secrets in dev)
services.AddAiOpenAi();

// Or override specific values
services.AddAiOpenAi(options => 
{
    options.BaseUrl = "https://azure-openai.com";  // Override only what's needed
});
```

❌ **Avoid:**
```csharp
// Hardcoding API keys
services.AddAiOpenAi("sk-hardcoded-key");
```

---

## Creating Prompts

```csharp
public class TranslatePrompt : PromptBase<TranslateResponse>
{
    public required string Content { get; set; }
    public required string Language { get; set; }

    public override string Prompt => @"
Translate the following text into {{ language }}:
{{ content }}
";
}

[Description("Translation result")]
public class TranslateResponse
{
    [Description("Translated text")]
    public required string TranslatedText { get; set; }
    
    [Description("Detected source language")]
    public string? DetectedLanguage { get; set; }
}

// Usage
var result = await aiClient.GenerateAsync(
    new GPT51(),
    new TranslatePrompt { Content = "Hello!", Language = "Polish" }
);
Console.WriteLine(result.Value.TranslatedText); // "Cześć!"
Console.WriteLine(result.MetaData.PromptName); // "Translate" (auto-generated)
```

---

## Cost Calculation

All results include a `MetaData` object with calculated costs using the `Price` value object:

```csharp
var result = await aiClient.GenerateAsync(new GPT51(), prompt);

// Token usage (via MetaData)
Console.WriteLine($"Tokens: {result.MetaData.InputTokens} in / {result.MetaData.OutputTokens} out");
Console.WriteLine($"Total tokens: {result.MetaData.TotalTokens}");
Console.WriteLine($"Cached tokens: {result.MetaData.Usage.CachedTokens}");

// Cost breakdown (Price value object from Zonit.Extensions)
Console.WriteLine($"Input cost: {result.MetaData.InputCost}");      // e.g. 0.01
Console.WriteLine($"Output cost: {result.MetaData.OutputCost}");    // e.g. 0.03
Console.WriteLine($"Total cost: {result.MetaData.TotalCost}");      // e.g. 0.04

// Duration and request info
Console.WriteLine($"Duration: {result.MetaData.Duration.TotalSeconds:F2}s");
Console.WriteLine($"Model: {result.MetaData.Model}");
Console.WriteLine($"Provider: {result.MetaData.Provider}");
Console.WriteLine($"Request ID: {result.MetaData.RequestId}");
```

### Result<T> Structure

```csharp
// Result contains the value and metadata
public class Result<T>
{
    public required T Value { get; init; }
    public required MetaData MetaData { get; init; }
}

// MetaData contains all operation details
public class MetaData
{
    public required ILlm Model { get; init; }       // The model instance used
    public required string Provider { get; init; }   // "OpenAI", "Anthropic", etc.
    public required string PromptName { get; init; } // Auto-generated from prompt class
    public required TokenUsage Usage { get; init; }
    public TimeSpan Duration { get; init; }
    public string? RequestId { get; init; }
    
    // Computed properties (shortcuts)
    public int InputTokens => Usage.InputTokens;
    public int OutputTokens => Usage.OutputTokens;
    public int TotalTokens => Usage.TotalTokens;
    public Price InputCost => Usage.InputCost;
    public Price OutputCost => Usage.OutputCost;
    public Price TotalCost => Usage.TotalCost;
}
```

### Estimate costs before calling

Use `IAiProvider` methods to calculate or estimate costs:

```csharp
// Calculate text generation cost
var cost = aiClient.CalculateCost(new GPT51(), inputTokens: 1000, outputTokens: 500);
Console.WriteLine($"Cost: {cost}");

// Calculate embedding cost
var embeddingCost = aiClient.CalculateCost(new TextEmbedding3Large(), inputTokens: 1000);

// Calculate image cost
var imageCost = aiClient.CalculateCost(new GPTImage1(), imageCount: 2);

// Calculate audio transcription cost (per minute)
var audioCost = aiClient.CalculateCost(new Whisper1(), durationSeconds: 180);

// Estimate cost from prompt text (estimates tokens automatically)
var estimated = aiClient.EstimateCost(new GPT51(), "Your prompt here...", estimatedOutputTokens: 500);
```

---

## Simple API

The API is designed to be simple and intuitive. The same `GenerateAsync` method is used for all model types - the compiler resolves the correct overload based on the model interface:

```csharp
// Text generation (ILlm)
var result = await aiClient.GenerateAsync(new GPT51(), "What is 2+2?");
Console.WriteLine(result.Value); // "4"

// Typed response with prompt class
var result = await aiClient.GenerateAsync(
    new GPT51(),
    new TranslatePrompt { Content = "Hello!", Language = "Polish" }
);
Console.WriteLine(result.Value.TranslatedText); // "Cześć!"

// Image generation (IImageLlm) - same method name, different model type
var image = await aiClient.GenerateAsync(new GPTImage1(), "A sunset over mountains");
await image.Value.SaveAsync("sunset.png");

// Embeddings (IEmbeddingLlm)
var embedding = await aiClient.GenerateAsync(new TextEmbedding3Large(), "Hello world");
float[] vector = embedding.Value;

// Audio transcription (IAudioLlm)  
var audio = await AiFile.CreateFromFilePathAsync("speech.mp3");
var transcription = await aiClient.GenerateAsync(new Whisper1(), audio, language: "en");
Console.WriteLine(transcription.Value);

// Streaming
await foreach (var chunk in aiClient.StreamAsync(new GPT51(), "Tell me a story"))
{
    Console.Write(chunk);
}
```

### Interface-based Type Detection

The model interface determines the operation:

| Interface | Method | Returns |
|-----------|--------|---------|
| `ILlm` | `GenerateAsync(model, string)` | `Result<string>` |
| `ILlm` | `GenerateAsync(model, IPrompt<T>)` | `Result<T>` |
| `IImageLlm` | `GenerateAsync(model, string)` | `Result<AiFile>` |
| `IEmbeddingLlm` | `GenerateAsync(model, string)` | `Result<float[]>` |
| `IAudioLlm` | `GenerateAsync(model, AiFile)` | `Result<string>` |

---

## Supported Models

### OpenAI

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| GPT-5.2 | `GPT52` | $1.75 / $14.00 | Latest flagship, vision, tools |
| GPT-5.2 Pro | `GPT52Pro` | $21.00 / $168.00 | Maximum quality |
| GPT-5.2 Codex | `GPT52Codex` | $1.75 / $14.00 | Optimized for coding |
| GPT-5.1 | `GPT51` | $1.25 / $10.00 | Previous flagship |
| GPT-5.1 Pro | `GPT51Pro` | $15.00 / $120.00 | Premium GPT-5.1 |
| GPT-5.1 Codex | `GPT51Codex` | $1.25 / $10.00 | Coding agent |
| GPT-5 | `GPT5` | $1.25 / $10.00 | Base GPT-5 |
| GPT-5 Pro | `GPT5Pro` | $15.00 / $120.00 | Premium GPT-5 |
| GPT-5 Mini | `GPT5Mini` | $0.25 / $2.00 | Cost-effective |
| GPT-5 Nano | `GPT5Nano` | $0.05 / $0.40 | Ultra-cheap |
| GPT-4.1 | `GPT41` | $2.00 / $8.00 | Latest GPT-4 |
| GPT-4.1 Mini | `GPT41Mini` | $0.40 / $1.60 | Fast, affordable |
| GPT-4.1 Nano | `GPT41Nano` | $0.10 / $0.40 | Cheapest GPT-4 |
| GPT-4o | `GPT4o` | $2.50 / $10.00 | Multimodal |
| GPT-4o Mini | `GPT4oMini` | $0.15 / $0.60 | Fast multimodal |
| O3 | `O3` | $2.00 / $8.00 | Reasoning model |
| O3 Pro | `O3Pro` | $20.00 / $80.00 | Premium reasoning |
| O3 Mini | `O3Mini` | $1.10 / $4.40 | Cost-effective reasoning |
| O4 Mini | `O4Mini` | $1.10 / $4.40 | Latest mini reasoning |
| O1 | `O1` | $15.00 / $60.00 | Previous O-series |
| O3 Deep Research | `O3DeepResearch` | $10.00 / $40.00 | Advanced research |
| GPT Image 1.5 | `GPTImage15` | Per image | Image generation |
| GPT Image 1 | `GPTImage1` | Per image | Image generation |
| GPT Image 1 Mini | `GPTImage1Mini` | Per image | Cost-effective images |
| Text Embedding 3 Large | `TextEmbedding3Large` | $0.13 / - | 3072 dimensions |
| Text Embedding 3 Small | `TextEmbedding3Small` | $0.02 / - | 1536 dimensions |
| Whisper | `Whisper1` | $0.006/min | Audio transcription |
| GPT-4o Transcribe | `GPT4oTranscribe` | $0.006/min | Audio transcription |

### Anthropic (Claude)

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Claude Sonnet 4.5 | `Sonnet45` | $3.00 / $15.00 | Best balance, agents, coding |
| Claude Opus 4.5 | `Opus45` | $5.00 / $25.00 | Maximum intelligence |
| Claude Haiku 4.5 | `Haiku45` | $1.00 / $5.00 | Fastest, cost-effective |
| Claude Sonnet 4 | `Sonnet4` | $3.00 / $15.00 | Previous Sonnet |
| Claude Opus 4 | `Opus4` | $5.00 / $25.00 | Previous Opus |
| Claude Sonnet 3.5 | `Sonnet35` | $3.00 / $15.00 | Legacy Sonnet |
| Claude Haiku 3.5 | `Haiku35` | $0.80 / $4.00 | Legacy Haiku |

### Google (Gemini)

| Model | Class | Features |
|-------|-------|----------|
| Gemini 2.5 Pro | `Gemini25Pro` | Most capable thinking model |
| Gemini 2.5 Flash | `Gemini25Flash` | Best price-to-performance |
| Gemini 2.5 Flash Lite | `Gemini25FlashLite` | Ultra fast, low cost |
| Gemini 2.0 Flash | `Gemini20Flash` | Second-gen flash |
| Gemini 2.0 Flash Lite | `Gemini20FlashLite` | Cost-effective |
| Text Embedding 004 | `TextEmbedding004` | Embeddings |

### X (Grok)

| Model | Class | Features |
|-------|-------|----------|
| Grok-4 | `Grok4` | Latest Grok, web search |
| Grok-4.1 Fast | `Grok41Fast` | Advanced reasoning |
| Grok-4.1 Fast Reasoning | `Grok41FastReasoning` | Full reasoning enabled |
| Grok-4.1 Fast Non-Reasoning | `Grok41FastNonReasoning` | High-speed, no reasoning overhead |
| Grok-3 | `Grok3` | Previous generation |
| Grok-3 Fast | `Grok3Fast` | Fast Grok-3 |
| Grok-3 Mini | `Grok3Mini` | Cost-effective |

### DeepSeek

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| DeepSeek V3 | `DeepSeekV3` | $0.28 / $0.42 | General-purpose, 128K context |
| DeepSeek R1 | `DeepSeekR1` | $0.28 / $0.42 | Reasoning with thinking mode |
| DeepSeek Coder V3 | `DeepSeekCoderV3` | $0.28 / $0.42 | Optimized for coding |

### Mistral

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Mistral Large | `MistralLarge` | $2.00 / $6.00 | Most capable, multimodal |
| Mistral Medium | `MistralMedium` | $0.40 / $2.00 | Balanced |
| Mistral Small | `MistralSmall` | $0.10 / $0.30 | Fast, cost-effective |
| Codestral | `Codestral` | $0.30 / $0.90 | Optimized for code |
| Mistral Embed | `MistralEmbed` | $0.10 / - | Embeddings |

---

## Scriban Templating

Properties are automatically available as snake_case:

```csharp
public class EmailPrompt : PromptBase<EmailResponse>
{
    public string RecipientName { get; set; }   // {{ recipient_name }}
    public List<string> Topics { get; set; }    // {{ topics }}
    public bool IsFormal { get; set; }          // {{ is_formal }}

    public override string Prompt => @"
Write an email to {{ recipient_name }}.

{{~ if is_formal ~}}
Use formal tone.
{{~ end ~}}

Topics:
{{~ for topic in topics ~}}
- {{ topic }}
{{~ end ~}}
";
}
```

---

## Files and Images

```csharp
public class AnalyzePrompt : PromptBase<AnalysisResult>
{
    public override string Prompt => "Analyze the documents";
}

var file = await AiFile.CreateFromFilePathAsync("image.jpg");
var result = await aiClient.GenerateAsync(
    new GPT51(),
    new AnalyzePrompt { Files = [file] }
);
```

---

## Image Generation

### Using ImagePromptBase

`ImagePromptBase` is the recommended base class for image generation prompts:

```csharp
// Simple usage with ImagePromptBase
var result = await aiClient.GenerateAsync(
    new GPTImage1 { Quality = ImageQuality.High, Size = ImageSize.Landscape },
    new ImagePromptBase("A sunset over mountains with dramatic clouds")
);
await result.Value.SaveAsync("sunset.png");

// Custom image prompt with additional context
public class ProductImagePrompt : ImagePromptBase
{
    public ProductImagePrompt(string productName, string style) 
        : base($"Professional product photo of {productName} in {style} style, white background, studio lighting")
    {
    }
}

var productImage = await aiClient.GenerateAsync(
    new GPTImage1 { Quality = ImageQuality.High },
    new ProductImagePrompt("wireless headphones", "minimalist")
);
```

### Image Quality and Size Options

```csharp
// Using global enums (recommended)
var result = await aiClient.GenerateAsync(
    new GPTImage1
    {
        Quality = ImageQuality.High,      // Standard, High, Ultra
        Size = ImageSize.Landscape        // Square, Portrait, Landscape, Small, Large
    },
    "A beautiful landscape"
);

// Legacy nested types (deprecated but supported for backward compatibility)
var result = await aiClient.GenerateAsync(
    new GPTImage1
    {
        Quality = (ImageQuality)GPTImage1.QualityType.High,
        Size = (ImageSize)GPTImage1.SizeType.Landscape
    },
    "A beautiful landscape"
);
```

### Different Image Models

```csharp
// GPT Image 1 - Full featured
var image1 = await aiClient.GenerateAsync(
    new GPTImage1 { Quality = ImageQuality.Ultra, Size = ImageSize.Large },
    "A detailed architectural rendering"
);

// GPT Image 1 Mini - Cost-effective
var imageMini = await aiClient.GenerateAsync(
    new GPTImage1Mini { Size = ImageSize.Square },
    "A simple icon design"
);

// GPT Image 1.5 - Latest model
var image15 = await aiClient.GenerateAsync(
    new GPTImage15 { Quality = ImageQuality.High },
    "A photorealistic portrait"
);
```

---

## Reasoning Models

GPT-5 series and O-series models support reasoning with configurable effort:

```csharp
// Configure reasoning effort
var result = await aiClient.GenerateAsync(
    new GPT52
    {
        Reason = ReasoningEffort.High,           // None, Low, Medium, High
        ReasonSummary = ReasoningSummary.Auto,   // None, Auto, Detailed
        OutputVerbosity = Verbosity.Medium       // Low, Medium, High
    },
    new ComplexAnalysisPrompt { Data = complexData }
);

// O-series models (always reasoning)
var o3Result = await aiClient.GenerateAsync(
    new O3 { Reason = ReasoningEffort.High },
    "Solve this complex mathematical proof..."
);

// Legacy nested types (deprecated but supported)
var legacyResult = await aiClient.GenerateAsync(
    new GPT51
    {
        Reason = (ReasoningEffort)OpenAiReasoningBase.ReasonType.High,
        OutputVerbosity = (Verbosity)OpenAiReasoningBase.VerbosityType.Medium
    },
    prompt
);
```

---

## Backward Compatibility

### MetaData Constructor

For legacy code using the old constructor syntax:

```csharp
// Old syntax (deprecated but still works)
#pragma warning disable CS0618
var metadata = new MetaData(new GPT51(), new Usage { Input = 500, Output = 30 });
#pragma warning restore CS0618

// New recommended syntax
var metadata = new MetaData
{
    Model = new GPT51(),
    Usage = new TokenUsage { InputTokens = 500, OutputTokens = 30 },
    Provider = "OpenAI",
    PromptName = "MyPrompt"
};
```

### Usage to TokenUsage

```csharp
// Old Usage class (deprecated)
#pragma warning disable CS0618
var usage = new Usage { Input = 1000, Output = 500 };
TokenUsage tokenUsage = usage;  // Implicit conversion
#pragma warning restore CS0618

// New TokenUsage class (recommended)
var tokenUsage = new TokenUsage
{
    InputTokens = 1000,
    OutputTokens = 500,
    CachedTokens = 200,
    ReasoningTokens = 100
};
```

### ImagePrompt to ImagePromptBase

```csharp
// Old ImagePrompt (deprecated)
#pragma warning disable CS0618
var oldPrompt = new ImagePrompt("A sunset");
#pragma warning restore CS0618

// New ImagePromptBase (recommended)
var newPrompt = new ImagePromptBase("A sunset");
```

---

## Resilience Configuration

Configure retry, timeout, and circuit breaker behavior globally:

```json
{
  "Ai": {
    "Resilience": {
      "HttpClientTimeout": "00:05:00",      // 5 minutes
      "MaxRetryAttempts": 3,                // Retry up to 3 times
      "RetryBaseDelay": "00:00:02",         // Start with 2s delay
      "RetryMaxDelay": "00:00:30",          // Max 30s delay
      "UseJitter": true                     // Add random jitter to prevent thundering herd
    }
  }
}
```

Or configure in code:

```csharp
services.AddAi(options =>
{
    options.Resilience.MaxRetryAttempts = 5;
    options.Resilience.HttpClientTimeout = TimeSpan.FromMinutes(10);
    options.Resilience.RetryBaseDelay = TimeSpan.FromSeconds(3);
    options.Resilience.RetryMaxDelay = TimeSpan.FromMinutes(1);
    options.Resilience.UseJitter = true;
});
```

### Per-Provider Timeout Override

Each provider can override the global timeout:

```json
{
  "Ai": {
    "Resilience": {
      "HttpClientTimeout": "00:05:00"  // Default for all
    },
    "OpenAi": {
      "Timeout": "00:10:00"            // Override for OpenAI only
    }
  }
}
```

```csharp
services.AddAiOpenAi(options =>
{
    options.Timeout = TimeSpan.FromMinutes(10);  // OpenAI-specific
});
```

---

## Example Prompts

The `Zonit.Extensions.Ai.Prompts` package includes ready-to-use prompts:

```csharp
// Translation
var result = await ai.GenerateAsync(new GPT51(), 
    new TranslatePrompt { Content = "Hello", Language = "Polish" });

// Sentiment analysis
var result = await ai.GenerateAsync(new GPT51(),
    new SentimentPrompt { Content = "I love this product!" });

// Summarization
var result = await ai.GenerateAsync(new GPT51(),
    new SummarizePrompt { Content = longText, MaxWords = 100 });

// Code generation
var result = await ai.GenerateAsync(new GPT51(),
    new CodePrompt { Description = "Fibonacci function", Language = "C#" });

// Classification
var result = await ai.GenerateAsync(new GPT51(),
    new ClassifyPrompt { Content = text, Categories = ["Tech", "Sports", "News"] });
```

---

## Architecture

The library follows **SOLID principles** and **Clean Architecture**:

### Project Structure

```
Zonit.Extensions.Ai/
├── Source/
│   ├── Zonit.Extensions.Ai/              # Core library
│   │   ├── AiOptions.cs                   # Global options ("Ai" section)
│   │   ├── AiServiceCollectionExtensions  # AddAi() registration
│   │   └── AiProvider.cs                  # Main provider orchestrator
│   │
│   ├── Zonit.Extensions.Ai.Abstractions/  # Interfaces only
│   │   ├── IPrompt.cs, ILlm.cs           # Contracts
│   │   └── IAiProvider.cs                 # Provider interface
│   │
│   ├── Zonit.Extensions.Ai.OpenAi/        # OpenAI provider
│   ├── Zonit.Extensions.Ai.Anthropic/     # Anthropic provider
│   ├── Zonit.Extensions.Ai.Google/        # Google provider
│   ├── Zonit.Extensions.Ai.X/             # X provider
│   ├── Zonit.Extensions.Ai.DeepSeek/      # DeepSeek provider
│   ├── Zonit.Extensions.Ai.Mistral/       # Mistral provider
│   │
│   └── Zonit.Extensions.Ai.Prompts/       # Ready-to-use prompts
```

### Key Design Principles

1. **Separation of Concerns**
   - Each provider is a separate NuGet package
   - Provider-specific options are in the provider package
   - Global options (`AiOptions`) contain only shared configuration

2. **Dependency Inversion**
   - Providers implement `IModelProvider` from Abstractions
   - Core library depends only on abstractions
   - Providers discovered via auto-discovery or explicit registration

3. **Open/Closed Principle**
   - Add new providers without modifying core library
   - Extend via `IModelProvider` interface

4. **Configuration Pattern**
   ```csharp
   // Best practice: BindConfiguration + PostConfigure
   services.AddOptions<OpenAiOptions>()
       .BindConfiguration("Ai:OpenAi");     // Load from appsettings.json
   
   if (options is not null)
       services.PostConfigure(options);      // Override with code
   ```

5. **Idempotent Registration**
   - Uses `TryAddSingleton` and `TryAddEnumerable`
   - Safe to call from multiple plugins
   - No duplicate registrations

### Configuration Hierarchy

```json
{
  "Ai": {                          // Global (AiOptions)
    "Resilience": { ... },          // Shared by all providers
    
    "OpenAi": { ... },              // OpenAiOptions (provider-specific)
    "Anthropic": { ... },           // AnthropicOptions
    "Google": { ... },              // GoogleOptions
    "X": { ... },                   // XOptions
    "DeepSeek": { ... },            // DeepSeekOptions
    "Mistral": { ... }              // MistralOptions
  }
}
```

### Provider Options Inheritance

```csharp
// Base class for all provider options
public abstract class AiProviderOptions
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public TimeSpan? Timeout { get; set; }
}

// Each provider extends the base
public sealed class OpenAiOptions : AiProviderOptions
{
    public const string SectionName = "Ai:OpenAi";
    public string? OrganizationId { get; set; }  // OpenAI-specific
}

public sealed class AnthropicOptions : AiProviderOptions
{
    public const string SectionName = "Ai:Anthropic";
    // Anthropic-specific properties can be added here
}
```

### Dependency Injection Flow

```
User Code
  ↓
services.AddAiOpenAi()
  ↓
├─ services.AddAi()                    # Core services (idempotent)
│  ├─ Register IAiProvider
│  ├─ Configure AiOptions
│  └─ Auto-discover providers
│
├─ Configure OpenAiOptions             # Provider-specific
│  └─ BindConfiguration("Ai:OpenAi")
│
├─ Register OpenAiProvider             # Provider implementation
│  └─ TryAddEnumerable (no duplicates)
│
└─ AddHttpClient<OpenAiProvider>()     # Resilience
   └─ AddStandardResilienceHandler()
```

---

## AOT and Trimming

This library uses reflection for:
- Scriban templating (property discovery)
- JSON serialization (response parsing)
- Provider auto-discovery (assembly scanning)

For AOT projects, use Source Generators:
- `AiJsonSerializerGenerator` - Generates JsonSerializerContext
- `AiProviderRegistrationGenerator` - Generates provider registration

---

## License

MIT License
