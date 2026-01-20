# Zonit.Extensions.Ai

A .NET library for integrating with multiple AI providers (OpenAI, Anthropic Claude, X Grok, Google Gemini) with Scriban templating, type-safe prompts, and built-in resilience.

---

## NuGet Packages

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| **Zonit.Extensions.Ai** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.svg) | Core library with prompts and DI |
| **Zonit.Extensions.Ai.Abstractions** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Abstractions.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Abstractions.svg) | Interfaces and contracts |
| **Zonit.Extensions.Ai.OpenAi** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.OpenAi.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.OpenAi.svg) | OpenAI provider (GPT-5, O3, DALL-E) |
| **Zonit.Extensions.Ai.Anthropic** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Anthropic.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Anthropic.svg) | Anthropic provider (Claude 4) |
| **Zonit.Extensions.Ai.Google** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Google.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Google.svg) | Google provider (Gemini) |
| **Zonit.Extensions.Ai.X** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.X.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.X.svg) | X provider (Grok) |
| **Zonit.Extensions.Ai.Prompts** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Prompts.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Prompts.svg) | Ready-to-use example prompts |

```powershell
# Core library
dotnet add package Zonit.Extensions.Ai

# Add providers you need
dotnet add package Zonit.Extensions.Ai.OpenAi
dotnet add package Zonit.Extensions.Ai.Anthropic
dotnet add package Zonit.Extensions.Ai.Google
dotnet add package Zonit.Extensions.Ai.X

# Or via NuGet Package Manager
Install-Package Zonit.Extensions.Ai
Install-Package Zonit.Extensions.Ai.OpenAi
```

---

## Features

- **Multi-provider** - OpenAI, Anthropic, Google, X with unified API
- **Type-safe prompts** - Strongly typed responses with JSON Schema
- **Scriban templating** - Dynamic prompts with variables and conditions
- **Cost calculation** - Estimate costs before calling API
- **Resilience** - Retry, circuit breaker, timeout with Microsoft.Extensions.Http.Resilience
- **Plugin architecture** - Auto-discovery of providers, safe to call AddAi multiple times

---

## Requirements

- .NET 8.0, 9.0, or 10.0

---

## Quick Start

### Simple Registration

```csharp
// In Program.cs
services.AddAi();  // Reads from appsettings.json automatically
```

```json
// appsettings.json
{
  "Ai": {
    "OpenAi": { "ApiKey": "sk-..." },
    "Anthropic": { "ApiKey": "sk-ant-..." },
    "Google": { "ApiKey": "..." },
    "X": { "ApiKey": "..." }
  }
}
```

### Fluent Configuration

```csharp
services.AddAi()
    .WithOpenAi(o => o.ApiKey = "sk-...")
    .WithAnthropic(o => o.ApiKey = "sk-ant-...")
    .WithGoogle(o => o.ApiKey = "...")
    .WithX(o => o.ApiKey = "...")
    .WithResilience(r => 
    {
        r.MaxRetryAttempts = 5;
        r.HttpClientTimeout = TimeSpan.FromMinutes(10);
    });
```

### Combined Configuration

```csharp
services.AddAi(options =>
{
    options.OpenAi.ApiKey = "sk-...";
    options.Resilience.MaxRetryAttempts = 3;
})
.WithAnthropic(o => o.ApiKey = config["Anthropic:ApiKey"]!);
```

### Plugin Architecture

Safe to call multiple times from different plugins:

```csharp
// Plugin A
services.AddAi().WithOpenAi(o => o.ApiKey = "...");

// Plugin B (does not duplicate, only adds configuration)
services.AddAi().WithAnthropic(o => o.ApiKey = "...");
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
Console.WriteLine(result.PromptName); // "Translate" (auto-generated)
```

---

## Cost Calculation

All results include calculated costs using the `Price` value object:

```csharp
var result = await aiClient.GenerateAsync(new GPT51(), prompt);

// Token usage
Console.WriteLine($"Tokens: {result.Usage.InputTokens} in / {result.Usage.OutputTokens} out");
Console.WriteLine($"Total tokens: {result.Usage.TotalTokens}");
Console.WriteLine($"Cached tokens: {result.Usage.CachedTokens}");

// Cost breakdown (Price value object from Zonit.Extensions)
Console.WriteLine($"Input cost: {result.InputCost}");      // e.g. 0.01
Console.WriteLine($"Output cost: {result.OutputCost}");    // e.g. 0.03
Console.WriteLine($"Total cost: {result.TotalCost}");      // e.g. 0.04

// Duration
Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2}s");
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
| `ILlm` | `GenerateAsync(model, string)` | `AiResult<string>` |
| `ILlm` | `GenerateAsync(model, IPrompt<T>)` | `AiResult<T>` |
| `IImageLlm` | `GenerateAsync(model, string)` | `AiResult<AiFile>` |
| `IEmbeddingLlm` | `GenerateAsync(model, string)` | `AiResult<float[]>` |
| `IAudioLlm` | `GenerateAsync(model, AiFile)` | `AiResult<string>` |

---

## Supported Models

### OpenAI

| Model | Class | Features |
|-------|-------|----------|
| GPT-5, GPT-5.1, GPT-5.2 | `GPT5`, `GPT51`, `GPT52`, `GPT52Chat` | Text, Vision, Image output |
| GPT-5 Mini/Nano | `GPT5Mini`, `GPT5Nano` | Cost-effective |
| GPT-4.1 | `GPT41`, `GPT41Mini`, `GPT41Nano` | Latest GPT-4 |
| O3, O3-Pro, O4-mini | `O3`, `O3Pro`, `O4Mini` | Reasoning models |
| GPT-4o Search | `GPT4oSearch` | Web search |
| DALL·E | `GPTImage1`, `GPTImage1Mini` | Image generation |

### Anthropic (Claude)

| Model | Class | Features |
|-------|-------|----------|
| Sonnet 4.5 | `Sonnet45` | Balanced, prompt caching |
| Opus 4, 4.1 | `Opus4`, `Opus41` | Most capable |
| Haiku 4.5 | `Haiku45` | Fast, cost-effective |

### X (Grok)

| Model | Class | Features |
|-------|-------|----------|
| Grok-4 | `Grok4` | Web search native |
| Grok-4.1 Fast | `Grok41Fast` | Advanced reasoning |
| Grok-3 | `Grok3`, `Grok3Fast`, `Grok3Mini` | Previous gen |

### Google (Gemini)

| Model | Class | Features |
|-------|-------|----------|
| Gemini 2.5 Pro | `Gemini25Pro` | Most capable |
| Gemini 2.5 Flash | `Gemini25Flash` | Balanced |
| Gemini 2.0 Flash | `Gemini20Flash`, `Gemini20FlashLite` | Cost-effective |

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

```csharp
var result = await aiClient.GenerateAsync(
    new GPTImage1
    {
        Quality = GPTImage1.QualityType.High,
        Size = GPTImage1.SizeType.Landscape,
        Style = GPTImage1.StyleType.Natural
    },
    "A sunset over mountains"
);
await result.Value.SaveAsync("sunset.png");
```

---

## Resilience Configuration

```json
{
  "Ai": {
    "Resilience": {
      "HttpClientTimeout": "00:05:00",
      "MaxRetryAttempts": 3,
      "RetryBaseDelay": "00:00:02",
      "RetryMaxDelay": "00:00:30",
      "UseJitter": true
    }
  }
}
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

```
Zonit.Extensions.Ai
├── Abstractions     - Interfaces (IPrompt, IAiProvider, ILlm)
├── Core             - PromptBase, AiBuilder, JsonParsers
├── Providers        - OpenAI, Anthropic, Google, X
├── Prompts          - Ready-to-use examples
└── SourceGenerators - AOT support
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
