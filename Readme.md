# Zonit.Extensions.Ai

A .NET library for integrating with multiple AI providers (OpenAI, Anthropic Claude, X Grok, Google Gemini) with Scriban templating, type-safe prompts, and built-in resilience.

## Installation

```powershell
Install-Package Zonit.Extensions.Ai
```

## Quick Start

```csharp
// 1. Register services
services.AddAi(options =>
{
    options.OpenAiKey = "your-openai-api-key";
    options.AnthropicKey = "your-anthropic-api-key";
    options.XKey = "your-x-api-key";
    options.GoogleKey = "your-google-api-key";
});

// 2. Create a prompt with Scriban templating
public class TranslatePrompt : PromptBase<TranslateResponse>
{
    public required string Content { get; set; }
    public required string Language { get; set; }

    public override string Prompt => @"
Translate the following text into {{ language }}:
{{ content }}
";
}

// 3. Generate response
var result = await aiClient.GenerateAsync(
    new TranslatePrompt { Content = "Hello!", Language = "Polish" }, 
    new GPT51()
);
Console.WriteLine(result.Value.TranslatedText);
```

## Supported Models

### OpenAI
| Model | Class | Features |
|-------|-------|----------|
| GPT-5, GPT-5.1 | `GPT5`, `GPT51`, `GPT51Chat` | Text, Vision, Image output |
| GPT-5 Mini/Nano | `GPT5Mini`, `GPT5Nano` | Cost-effective |
| GPT-4.1 | `GPT41`, `GPT41Mini`, `GPT41Nano` | Latest GPT-4 |
| O3, O4-mini | `O3`, `O4Mini` | Reasoning models |
| GPT-4o Search | `GPT4oSearch` | Web search |
| DALL•E | `GPTImage1`, `GPTImage1Mini` | Image generation |

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
| Grok-4.1 | `Grok41FastReasoning`, `Grok41FastNonReasoning` | Advanced reasoning |
| Grok-3 | `Grok3`, `Grok3Fast`, `Grok3Mini`, `Grok3MiniFast` | Previous gen |
| GrokCodeFast1 | `GrokCodeFast1` | Code specialized |

## Scriban Templating

Properties are automatically available in templates (PascalCase ? snake_case):

```csharp
public class MyPrompt : PromptBase<MyResponse>
{
    public string Name { get; set; }           // {{ name }}
    public List<string> Items { get; set; }    // {{ items }}
    public bool IsActive { get; set; }         // {{ is_active }}

    public override string Prompt => @"
Hello {{ name }}!

{{~ if is_active ~}}
Your items:
{{~ for item in items ~}}
- {{ item }}
{{~ end ~}}
{{~ end ~}}
";
}
```

**Whitespace control:** `{{~` removes whitespace before, `~}}` removes after.

**Excluded properties:** `Tools`, `ToolChoice`, `UserName`, `Files`, `ModelType`

## AI Tools

### Web Search (OpenAI)

```csharp
public class SearchPrompt : PromptBase<SearchResponse>
{
    public required string Query { get; set; }
    public override string Prompt => "Search for: {{ query }}";
    
    public override IReadOnlyList<ITool> Tools => 
        new[] { new WebSearchTool { ContextSize = WebSearchTool.ContextSizeType.High } };
    public override ToolsType ToolChoice => ToolsType.WebSearch;
}
```

### Advanced Grok Web Search

```csharp
var grok = new Grok4
{
    WebSearch = new Search
    {
        Mode = ModeType.Always,           // Always, Never, Auto
        Citations = true,
        MaxResults = 20,
        FromDate = DateTime.UtcNow.AddMonths(-6),
        ToDate = DateTime.UtcNow,
        Language = "en",                  // ISO 639-1
        Region = "US",                    // ISO 3166-1 alpha-2
        Sources = new ISearchSource[]
        {
            new WebSearchSource
            {
                AllowedWebsites = new[] { "wikipedia.org", "github.com" },
                SafeSearch = true
            },
            new XSearchSource
            {
                IncludedXHandles = new[] { "OpenAI", "anthropaborAI" }
            }
        }
    }
};
```

### File Search

```csharp
public class DocumentPrompt : PromptBase<AnalysisResult>
{
    public override IReadOnlyList<IFile> Files { get; set; }
    public override IReadOnlyList<ITool> Tools => new[] { new FileSearchTool() };
    public override ToolsType ToolChoice => ToolsType.FileSearch;
    
    public override string Prompt => "Analyze the documents";
}
```

## File Management

```csharp
// Create from path
var file = await FileModel.CreateFromFilePathAsync("image.jpg");

// Create from bytes
var file = new FileModel("doc.pdf", "application/pdf", bytes);

// Save
await file.SaveToFileAsync("output.jpg");
```

**Supported formats:** JPG, PNG, GIF, WebP, PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX, TXT, JSON, CSV, XML

## Image Generation

```csharp
public class ImagePrompt : PromptBase<IFile>
{
    public required string Description { get; set; }
    public override string Prompt => "Generate: {{ description }}";
}

var result = await aiClient.GenerateAsync(
    new ImagePrompt { Description = "A sunset over mountains" },
    new GPTImage1
    {
        Quality = GPTImage1.QualityType.High,    // Standard, High
        Size = GPTImage1.SizeType.Landscape,      // Square, Portrait, Landscape
        Style = GPTImage1.StyleType.Natural       // Natural, Vivid
    }
);
await result.Value.SaveToFileAsync("sunset.png");
```

## Model Configuration

```csharp
// Text models
var model = new GPT51
{
    MaxTokens = 4000,
    Temperature = 0.7,
    TopP = 0.9,
    StoreLogs = true
};

// Reasoning models
var reasoning = new O3
{
    Reason = OpenAiReasoningBase.ReasonType.High,
    ReasonSummary = OpenAiReasoningBase.ReasonSummaryType.Detailed
};

// Check capabilities
if (model.SupportedTools.HasFlag(ToolsType.WebSearch)) { /* ... */ }
if (model.SupportedFeatures.HasFlag(FeaturesType.Streaming)) { /* ... */ }
```

## Metadata & Costs

```csharp
var result = await aiClient.GenerateAsync(prompt, model);

Console.WriteLine($"Tokens: {result.MetaData.InputTokenCount} in / {result.MetaData.OutputTokenCount} out");
Console.WriteLine($"Cost: ${result.MetaData.PriceTotal:F6}");
Console.WriteLine($"Duration: {result.MetaData.Duration.TotalSeconds:F2}s");
```

## JSON Schema Responses

The library auto-generates JSON Schema from response classes:

```csharp
[Description("Translation result")]
public class TranslateResponse
{
    [Description("Translated text")]
    public string TranslatedText { get; set; }
    
    [Description("Detected source language")]
    public string DetectedLanguage { get; set; }
}
```

## Resilience Configuration

Uses **Microsoft.Extensions.Http.Resilience** with unified config for all providers:

```json
{
  "Ai": {
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
  }
}
```

## Error Handling

```csharp
try
{
    var result = await aiClient.GenerateAsync(prompt, model);
}
catch (JsonException ex)
{
    // JSON parsing error
}
catch (InvalidOperationException ex)
{
    // API error (after retries exhausted)
}
```

## Architecture

Clean Architecture with layered separation:

- **Abstractions** - Interfaces and contracts
- **Domain** - Models and business logic
- **Application** - Services, configuration, Scriban prompt service
- **Infrastructure** - Provider implementations (OpenAI, Anthropic, X, Google)
- **LLM** - Model definitions and tools
- **Prompts** - Example templates
