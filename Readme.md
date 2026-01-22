# Zonit.Extensions.Ai

A .NET library for integrating with multiple AI providers with Scriban templating, type-safe prompts, and built-in resilience.

**Supported Providers:**
- **Commercial:** OpenAI, Anthropic, Google, X (Grok), DeepSeek, Mistral
- **Open-Source Inference:** Groq, Together, Fireworks, Cohere
- **Search-Augmented:** Perplexity
- **Chinese Providers:** Alibaba (Qwen), Baidu (ERNIE), Zhipu (GLM), Moonshot, Yi (01.AI)

---

## NuGet Packages

### Core Packages

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| **Zonit.Extensions.Ai** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.svg) | Core library with prompts and DI |
| **Zonit.Extensions.Ai.Abstractions** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Abstractions.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Abstractions.svg) | Interfaces and contracts |
| **Zonit.Extensions.Ai.Prompts** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Prompts.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Prompts.svg) | Ready-to-use example prompts |

### Commercial Providers

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| **Zonit.Extensions.Ai.OpenAi** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.OpenAi.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.OpenAi.svg) | OpenAI (GPT-5, O3/O4, DALL-E) |
| **Zonit.Extensions.Ai.Anthropic** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Anthropic.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Anthropic.svg) | Anthropic (Claude 4.5) |
| **Zonit.Extensions.Ai.Google** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Google.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Google.svg) | Google (Gemini 2.5/3) |
| **Zonit.Extensions.Ai.X** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.X.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.X.svg) | X (Grok 4) |
| **Zonit.Extensions.Ai.DeepSeek** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.DeepSeek.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.DeepSeek.svg) | DeepSeek (V3, R1) |
| **Zonit.Extensions.Ai.Mistral** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Mistral.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Mistral.svg) | Mistral (Large, Codestral) |

### Open-Source Inference Providers

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| **Zonit.Extensions.Ai.Groq** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Groq.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Groq.svg) | Groq (Llama 4, Qwen3, ultra-fast inference) |
| **Zonit.Extensions.Ai.Together** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Together.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Together.svg) | Together AI (Llama 4, DeepSeek, Qwen3) |
| **Zonit.Extensions.Ai.Fireworks** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Fireworks.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Fireworks.svg) | Fireworks (Llama, Mixtral, fast inference) |
| **Zonit.Extensions.Ai.Cohere** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Cohere.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Cohere.svg) | Cohere (Command A, Aya, embeddings) |

### Search-Augmented Providers

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| **Zonit.Extensions.Ai.Perplexity** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Perplexity.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Perplexity.svg) | Perplexity (Sonar, deep research) |

### Chinese Providers

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| **Zonit.Extensions.Ai.Alibaba** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Alibaba.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Alibaba.svg) | Alibaba Cloud (Qwen Max/Plus/Turbo) |
| **Zonit.Extensions.Ai.Baidu** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Baidu.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Baidu.svg) | Baidu (ERNIE 4.0/3.5) |
| **Zonit.Extensions.Ai.Zhipu** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Zhipu.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Zhipu.svg) | Zhipu AI (GLM-4 Plus/Air/Flash) |
| **Zonit.Extensions.Ai.Moonshot** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Moonshot.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Moonshot.svg) | Moonshot (Kimi, 128K context) |
| **Zonit.Extensions.Ai.Yi** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Extensions.Ai.Yi.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Extensions.Ai.Yi.svg) | 01.AI (Yi Large/Medium) |

```powershell
# Core library
dotnet add package Zonit.Extensions.Ai

# Commercial providers
dotnet add package Zonit.Extensions.Ai.OpenAi
dotnet add package Zonit.Extensions.Ai.Anthropic
dotnet add package Zonit.Extensions.Ai.Google
dotnet add package Zonit.Extensions.Ai.X
dotnet add package Zonit.Extensions.Ai.DeepSeek
dotnet add package Zonit.Extensions.Ai.Mistral

# Open-source inference (fastest & cheapest)
dotnet add package Zonit.Extensions.Ai.Groq
dotnet add package Zonit.Extensions.Ai.Together
dotnet add package Zonit.Extensions.Ai.Fireworks
dotnet add package Zonit.Extensions.Ai.Cohere

# Search-augmented
dotnet add package Zonit.Extensions.Ai.Perplexity

# Chinese providers
dotnet add package Zonit.Extensions.Ai.Alibaba
dotnet add package Zonit.Extensions.Ai.Baidu
dotnet add package Zonit.Extensions.Ai.Zhipu
dotnet add package Zonit.Extensions.Ai.Moonshot
dotnet add package Zonit.Extensions.Ai.Yi
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

// Commercial Providers
services.AddAiOpenAi();      // Loads from "Ai:OpenAi"
services.AddAiAnthropic();   // Loads from "Ai:Anthropic"
services.AddAiGoogle();      // Loads from "Ai:Google"
services.AddAiX();           // Loads from "Ai:X"
services.AddAiDeepSeek();    // Loads from "Ai:DeepSeek"
services.AddAiMistral();     // Loads from "Ai:Mistral"

// Open-Source Inference (fastest & cheapest)
services.AddAiGroq();        // Loads from "Ai:Groq"
services.AddAiTogether();    // Loads from "Ai:Together"
services.AddAiFireworks();   // Loads from "Ai:Fireworks"
services.AddAiCohere();      // Loads from "Ai:Cohere"

// Search-Augmented
services.AddAiPerplexity();  // Loads from "Ai:Perplexity"

// Chinese Providers
services.AddAiAlibaba();     // Loads from "Ai:Alibaba"
services.AddAiBaidu();       // Loads from "Ai:Baidu"
services.AddAiZhipu();       // Loads from "Ai:Zhipu"
services.AddAiMoonshot();    // Loads from "Ai:Moonshot"
services.AddAiYi();          // Loads from "Ai:Yi"
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
if (image.IsSuccess)
    await File.WriteAllBytesAsync("sunset.png", image.Value.Data);

// Embeddings (IEmbeddingLlm)
var embedding = await aiClient.GenerateAsync(new TextEmbedding3Large(), "Hello world");
float[] vector = embedding.Value;

// Audio transcription (IAudioLlm)  
var audioBytes = await File.ReadAllBytesAsync("speech.mp3");
var audio = new Asset(audioBytes, "speech.mp3");
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
| `IImageLlm` | `GenerateAsync(model, string)` | `Result<Asset>` |
| `IEmbeddingLlm` | `GenerateAsync(model, string)` | `Result<float[]>` |
| `IAudioLlm` | `GenerateAsync(model, Asset)` | `Result<string>` |

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

### Groq (Ultra-Fast Inference)

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Llama 4 Scout 17B | `Llama4Scout17B` | $0.11 / $0.34 | Vision, 131K context |
| Llama 4 Maverick 17B | `Llama4Maverick17B` | $0.20 / $0.60 | Vision, advanced reasoning |
| Llama 3.3 70B | `Llama3_3_70B` | $0.59 / $0.79 | 128K context, versatile |
| Qwen3 32B | `Qwen3_32B` | $0.29 / $0.59 | Reasoning, 131K context |
| Llama 3.1 8B | `Llama3_1_8B` | $0.05 / $0.08 | Fast, cost-effective |
| DeepSeek R1 Distill 70B | `DeepSeekR1DistillLlama70B` | $0.75 / $0.99 | Reasoning |
| Mixtral 8x7B | `Mixtral8x7B` | $0.24 / $0.24 | MoE, 32K context |
| Gemma2 9B | `Gemma2_9B` | $0.20 / $0.20 | Google open model |
| LlamaGuard 4 12B | `LlamaGuard4_12B` | $0.20 / $0.20 | Safety classifier |

### Together AI

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Llama 4 Scout 17B | `Llama4Scout17B` | $0.18 / $0.59 | 512K context |
| Llama 4 Maverick 17B | `Llama4Maverick17B` | $0.27 / $0.85 | 1M context |
| Llama 3.3 70B Instruct | `MetaLlama3_3_70BInstruct` | $0.88 / $0.88 | 128K context |
| DeepSeek R1 0528 | `DeepSeekR1_0528` | $1.65 / $7.20 | Latest reasoning |
| DeepSeek V3.1 | `DeepSeekV3_1` | $0.49 / $0.89 | Latest base |
| DeepSeek R1 | `DeepSeekR1` | $3.00 / $7.20 | Reasoning model |
| DeepSeek V3 | `DeepSeekV3` | $0.35 / $0.90 | Base model |
| Qwen3 235B | `Qwen3_235B` | $0.50 / $0.50 | Largest Qwen MoE |
| Qwen3 VL 32B | `Qwen3VL32B` | $0.18 / $0.18 | Vision-language |
| Qwen 2.5 72B Instruct | `Qwen2_5_72BInstruct` | $1.20 / $1.20 | Strong reasoning |
| Qwen 2.5 Coder 32B | `Qwen2_5_Coder32BInstruct` | $0.80 / $0.80 | Coding specialist |

### Fireworks

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Llama 3.3 70B Instruct | `Llama3_3_70BInstruct` | $0.90 / $0.90 | 128K context |
| DeepSeek V3 | `DeepSeekV3` | $0.90 / $0.90 | General purpose |
| Qwen 2.5 72B Instruct | `Qwen2_5_72BInstruct` | $0.90 / $0.90 | Strong reasoning |
| Mixtral 8x22B Instruct | `MixtralMoe8x22BInstruct` | $0.90 / $0.90 | MoE, 65K context |

### Cohere

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Command A | `CommandA` | $2.50 / $10.00 | Flagship, 256K context |
| Command A Reasoning | `CommandAReasoning` | $2.50 / $10.00 | Extended reasoning, 32K output |
| Command A Vision | `CommandAVision` | $2.50 / $10.00 | Multimodal, 128K context |
| Command R+ | `CommandRPlus` | $2.50 / $10.00 | Previous flagship |
| Command R | `CommandR` | $0.15 / $0.60 | Cost-effective |
| Command R 7B | `CommandR7B` | $0.0375 / $0.15 | Lightweight |
| Aya Expanse 32B | `AyaExpanse32B` | $0.50 / $1.00 | Multilingual |
| Aya Expanse 8B | `AyaExpanse8B` | $0.10 / $0.20 | Lightweight multilingual |
| Embed v4 | `EmbedV4` | $0.10 / - | 128K context embeddings |
| Embed English v3 | `EmbedEnglishV3` | $0.10 / - | English embeddings |
| Embed Multilingual v3 | `EmbedMultilingualV3` | $0.10 / - | 100+ languages |

### Perplexity (Search-Augmented)

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Sonar | `Sonar` | $1.00 / $1.00 | Standard search, 128K |
| Sonar Pro | `SonarPro` | $3.00 / $15.00 | Enhanced search |
| Sonar Reasoning Pro | `SonarReasoningPro` | $2.00 / $8.00 | Reasoning + search |
| Sonar Deep Research | `SonarDeepResearch` | $5.00 / $20.00 | Deep research |

### Alibaba Cloud (Qwen)

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Qwen Max | `QwenMax` | $2.40 / $9.60 | Flagship, 32K context |
| Qwen Plus | `QwenPlus` | $0.80 / $3.20 | Balanced |
| Qwen Turbo | `QwenTurbo` | $0.30 / $0.60 | Fast |
| Qwen Long | `QwenLong` | $0.60 / $2.40 | Extended context |
| Qwen Coder Plus | `QwenCoderPlus` | $2.40 / $9.60 | Coding specialist |

### Baidu (ERNIE)

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| ERNIE 4.0 | `Ernie4` | $12.00 / $12.00 | Flagship, Chinese |
| ERNIE 4.0 Turbo | `Ernie4Turbo` | $6.00 / $6.00 | Faster flagship |
| ERNIE 3.5 | `Ernie3_5` | $1.20 / $1.20 | Balanced |
| ERNIE Speed | `ErnieSpeed` | $0.40 / $0.80 | Fast |
| ERNIE Lite | `ErnieLite` | $0.10 / $0.10 | Cost-effective |

### Zhipu AI (GLM)

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| GLM-4 Plus | `Glm4Plus` | $7.00 / $7.00 | Flagship, 128K context |
| GLM-4 Long | `Glm4Long` | $0.14 / $0.14 | 1M context |
| GLM-4 Air | `Glm4Air` | $0.14 / $0.14 | Balanced |
| GLM-4 Flash | `Glm4Flash` | $0.01 / $0.01 | Ultra-fast |

### Moonshot (Kimi)

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Moonshot V1 128K | `MoonshotV1_128K` | $6.00 / $6.00 | Maximum context |
| Moonshot V1 32K | `MoonshotV1_32K` | $1.70 / $1.70 | Standard context |
| Moonshot V1 8K | `MoonshotV1_8K` | $0.85 / $0.85 | Fast, affordable |

### 01.AI (Yi)

| Model | Class | Price (in/out per 1M) | Features |
|-------|-------|----------------------|----------|
| Yi Large | `YiLarge` | $3.00 / $3.00 | Flagship, bilingual |
| Yi Large Turbo | `YiLargeTurbo` | $1.50 / $1.50 | Fast flagship |
| Yi Medium | `YiMedium` | $0.36 / $0.36 | Balanced |
| Yi Medium 200K | `YiMedium200K` | $0.85 / $0.85 | Extended 200K context |

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

The library uses `Asset` from `Zonit.Extensions` for file handling. Asset is a versatile value object that:
- Auto-detects MIME type from binary data
- Provides `IsImage`, `IsDocument`, `IsAudio` properties
- Includes `ToBase64()`, `ToDataUrl()` for encoding
- Has implicit conversions from `byte[]` and `Stream`

```csharp
public class AnalyzePrompt : PromptBase<AnalysisResult>
{
    public override string Prompt => "Analyze the documents";
}

// From file path
using var httpClient = new HttpClient();
var imageBytes = await httpClient.GetByteArrayAsync("https://example.com/image.jpg");
var asset = new Asset(imageBytes, "image.jpg");

// Or from local file
var localBytes = await File.ReadAllBytesAsync("document.pdf");
var pdfAsset = new Asset(localBytes, "document.pdf");

var result = await aiClient.GenerateAsync(
    new GPT51(),
    new AnalyzePrompt { Files = [asset] }
);
```

---

## Image Generation

### Using ImagePromptBase

Each image model (GPTImage1, GPTImage1Mini, GPTImage15) defines its own `QualityType` and `SizeType` enums with `[EnumValue]` attributes that map directly to API values. This ensures correct API parameters per model.

```csharp
// Simple usage with model-specific enums (required)
var result = await aiClient.GenerateAsync(
    new GPTImage1 { Quality = GPTImage1.QualityType.High, Size = GPTImage1.SizeType.Landscape },
    new ImagePromptBase("A sunset over mountains with dramatic clouds")
);
if (result.IsSuccess)
    await File.WriteAllBytesAsync("sunset.png", result.Value.Data);

// Custom image prompt with additional context
public class ProductImagePrompt : ImagePromptBase
{
    public ProductImagePrompt(string productName, string style) 
        : base($"Professional product photo of {productName} in {style} style, white background, studio lighting")
    {
    }
}

var productImage = await aiClient.GenerateAsync(
    new GPTImage1 { Quality = GPTImage1.QualityType.High, Size = GPTImage1.SizeType.Square },
    new ProductImagePrompt("wireless headphones", "minimalist")
);
```

### Image Quality and Size Options

Each model has its own QualityType and SizeType enums. Quality and Size are **required** parameters.

```csharp
// GPT Image 1 - supports Auto, Low, Medium, High quality and Auto, Square, Landscape, Portrait sizes
var result = await aiClient.GenerateAsync(
    new GPTImage1
    {
        Quality = GPTImage1.QualityType.High,      // Auto, Low, Medium, High
        Size = GPTImage1.SizeType.Landscape        // Auto, Square, Landscape, Portrait
    },
    "A beautiful landscape"
);

// GPT Image 1 Mini - no Auto options (must be explicit)
var miniResult = await aiClient.GenerateAsync(
    new GPTImage1Mini
    {
        Quality = GPTImage1Mini.QualityType.Medium,  // Low, Medium, High (no Auto)
        Size = GPTImage1Mini.SizeType.Square         // Square, Portrait, Landscape (no Auto)
    },
    "A simple icon"
);

// Get image generation price based on quality and size
var model = new GPTImage1 { Quality = GPTImage1.QualityType.High, Size = GPTImage1.SizeType.Landscape };
var pricePerImage = model.GetImageGenerationPrice(); // Returns $0.25
```

### Different Image Models

```csharp
// GPT Image 1 - Full featured with Auto support
var image1 = await aiClient.GenerateAsync(
    new GPTImage1 { Quality = GPTImage1.QualityType.High, Size = GPTImage1.SizeType.Landscape },
    "A detailed architectural rendering"
);

// GPT Image 1 Mini - Cost-effective (cheapest option)
var imageMini = await aiClient.GenerateAsync(
    new GPTImage1Mini { Quality = GPTImage1Mini.QualityType.Low, Size = GPTImage1Mini.SizeType.Square },
    "A simple icon design"
);

// GPT Image 1.5 - Latest model with best quality
var image15 = await aiClient.GenerateAsync(
    new GPTImage15 { Quality = GPTImage15.QualityType.High, Size = GPTImage15.SizeType.Auto },
    "A photorealistic portrait"
);
```

### Image Pricing (per image)

| Model          | Quality | Square (1024x1024) | Landscape/Portrait |
|----------------|---------|-------------------:|-------------------:|
| GPT Image 1    | Low     | $0.011             | $0.016             |
| GPT Image 1    | Medium  | $0.042             | $0.063             |
| GPT Image 1    | High    | $0.167             | $0.250             |
| GPT Image 1 Mini | Low   | $0.005             | $0.006             |
| GPT Image 1 Mini | Medium | $0.011            | $0.015             |
| GPT Image 1 Mini | High  | $0.036             | $0.052             |

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

The library uses **Microsoft.Extensions.Http.Resilience** with optimized settings for AI providers. Configure retry, timeout, and circuit breaker behavior globally:

```json
{
  "Ai": {
    "Resilience": {
      "TotalRequestTimeout": "00:40:00",    // 40 minutes total (including retries)
      "AttemptTimeout": "00:10:00",          // 10 minutes per attempt
      "MaxRetryAttempts": 3,                 // Retry up to 3 times
      "RetryBaseDelay": "00:00:02",          // Start with 2s delay
      "RetryMaxDelay": "00:00:30",           // Max 30s delay
      "UseJitter": true,                     // Add random jitter to prevent thundering herd
      "CircuitBreakerSamplingDuration": "00:25:00", // Must be >= 2x AttemptTimeout
      "CircuitBreakerFailureRatio": 0.5,
      "CircuitBreakerMinimumThroughput": 5,
      "CircuitBreakerBreakDuration": "00:00:30"
    }
  }
}
```

Or configure in code:

```csharp
services.AddAi(options =>
{
    options.Resilience.TotalRequestTimeout = TimeSpan.FromMinutes(40);
    options.Resilience.AttemptTimeout = TimeSpan.FromMinutes(10);
    options.Resilience.MaxRetryAttempts = 5;
    options.Resilience.RetryBaseDelay = TimeSpan.FromSeconds(3);
    options.Resilience.RetryMaxDelay = TimeSpan.FromMinutes(1);
    options.Resilience.UseJitter = true;
    // Note: CircuitBreakerSamplingDuration is auto-corrected to be >= 2.5x AttemptTimeout
});
```

### Default Resilience Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `TotalRequestTimeout` | 40 min | Maximum time for entire request pipeline (including retries) |
| `AttemptTimeout` | 10 min | Timeout for single request attempt |
| `MaxRetryAttempts` | 3 | Number of retry attempts on transient failures |
| `RetryBaseDelay` | 2s | Initial delay between retries (exponential backoff) |
| `RetryMaxDelay` | 30s | Maximum delay between retries |
| `UseJitter` | true | Add randomness to delays to prevent thundering herd |
| `CircuitBreakerSamplingDuration` | 25 min | Must be >= 2x AttemptTimeout (auto-corrected) |
| `CircuitBreakerFailureRatio` | 0.5 | 50% failure rate opens circuit |
| `CircuitBreakerBreakDuration` | 30s | Time circuit stays open before test |

### Retry Policy

Requests are automatically retried on:
- Network errors (`HttpRequestException`)
- Timeouts (`TaskCanceledException`, `TimeoutException`)
- Rate limiting (HTTP 429)
- Server errors (HTTP 500, 502, 503, 504)

### Per-Provider Timeout Override

Each provider can override the global timeout:

```json
{
  "Ai": {
    "Resilience": {
      "TotalRequestTimeout": "00:40:00"  // Default for all
    },
    "OpenAi": {
      "Timeout": "01:00:00"              // Override for OpenAI only
    }
  }
}
```

```csharp
services.AddAiOpenAi(options =>
{
    options.Timeout = TimeSpan.FromMinutes(15);  // OpenAI-specific
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
│   │  # Commercial Providers
│   ├── Zonit.Extensions.Ai.OpenAi/        # OpenAI (GPT-5, O3/O4, DALL-E)
│   ├── Zonit.Extensions.Ai.Anthropic/     # Anthropic (Claude 4.5)
│   ├── Zonit.Extensions.Ai.Google/        # Google (Gemini)
│   ├── Zonit.Extensions.Ai.X/             # X (Grok 4)
│   ├── Zonit.Extensions.Ai.DeepSeek/      # DeepSeek (V3, R1)
│   ├── Zonit.Extensions.Ai.Mistral/       # Mistral (Large, Codestral)
│   │
│   │  # Open-Source Inference
│   ├── Zonit.Extensions.Ai.Groq/          # Groq (Llama 4, ultra-fast)
│   ├── Zonit.Extensions.Ai.Together/      # Together AI (Llama, Qwen, DeepSeek)
│   ├── Zonit.Extensions.Ai.Fireworks/     # Fireworks (fast inference)
│   ├── Zonit.Extensions.Ai.Cohere/        # Cohere (Command A, embeddings)
│   │
│   │  # Search-Augmented
│   ├── Zonit.Extensions.Ai.Perplexity/    # Perplexity (Sonar, deep research)
│   │
│   │  # Chinese Providers
│   ├── Zonit.Extensions.Ai.Alibaba/       # Alibaba Cloud (Qwen)
│   ├── Zonit.Extensions.Ai.Baidu/         # Baidu (ERNIE)
│   ├── Zonit.Extensions.Ai.Zhipu/         # Zhipu AI (GLM-4)
│   ├── Zonit.Extensions.Ai.Moonshot/      # Moonshot (Kimi)
│   ├── Zonit.Extensions.Ai.Yi/            # 01.AI (Yi)
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
    
    // Commercial Providers
    "OpenAi": { ... },              // OpenAiOptions
    "Anthropic": { ... },           // AnthropicOptions
    "Google": { ... },              // GoogleOptions
    "X": { ... },                   // XOptions
    "DeepSeek": { ... },            // DeepSeekOptions
    "Mistral": { ... },             // MistralOptions
    
    // Open-Source Inference
    "Groq": { ... },                // GroqOptions
    "Together": { ... },            // TogetherOptions
    "Fireworks": { ... },           // FireworksOptions
    "Cohere": { ... },              // CohereOptions
    
    // Search-Augmented
    "Perplexity": { ... },          // PerplexityOptions
    
    // Chinese Providers
    "Alibaba": { ... },             // AlibabaOptions
    "Baidu": { ... },               // BaiduOptions
    "Zhipu": { ... },               // ZhipuOptions
    "Moonshot": { ... },            // MoonshotOptions
    "Yi": { ... }                   // YiOptions
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
