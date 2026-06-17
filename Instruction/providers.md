# Providers and packages: which NuGet to install

Use this to decide what to install. Each provider is a separate package; core
(`Zonit.Extensions.Ai`) arrives transitively. If the user asks for a capability the installed
packages do not cover, install the right package below, then implement.

For example, "generate images with GPT" needs an image-capable provider, so
`dotnet add package Zonit.Extensions.Ai.OpenAi`, then use `GPTImage15` (see
[`usage.md`](./usage.md)).

## By capability (install at least one package from the row)

| Capability | Packages that provide it |
| :--- | :--- |
| Text, chat, structured output, agents, tools | any provider package below |
| Image generation | `Zonit.Extensions.Ai.OpenAi` (GPT Image), `Zonit.Extensions.Ai.X` (Grok Imagine) |
| Video generation | `Zonit.Extensions.Ai.X` (Grok Imagine) |
| Embeddings | `Zonit.Extensions.Ai.OpenAi`, `Zonit.Extensions.Ai.Google`, `Zonit.Extensions.Ai.Mistral`, `Zonit.Extensions.Ai.Cohere` |
| Audio transcription | `Zonit.Extensions.Ai.OpenAi` (GPT-4o Transcribe / mini / diarize; `whisper-1` is legacy) |

## By provider

| Provider | Package | Register |
| :--- | :--- | :--- |
| OpenAI (GPT, o-series, GPT Image, embeddings, Transcribe) | `Zonit.Extensions.Ai.OpenAi` | `AddAiOpenAi()` |
| Anthropic (Claude) | `Zonit.Extensions.Ai.Anthropic` | `AddAiAnthropic()` |
| Google (Gemini) | `Zonit.Extensions.Ai.Google` | `AddAiGoogle()` |
| xAI (Grok, Grok Imagine image/video) | `Zonit.Extensions.Ai.X` | `AddAiX()` |
| DeepSeek | `Zonit.Extensions.Ai.DeepSeek` | `AddAiDeepSeek()` |
| Mistral | `Zonit.Extensions.Ai.Mistral` | `AddAiMistral()` |
| Groq | `Zonit.Extensions.Ai.Groq` | `AddAiGroq()` |
| Together AI | `Zonit.Extensions.Ai.Together` | `AddAiTogether()` |
| Fireworks | `Zonit.Extensions.Ai.Fireworks` | `AddAiFireworks()` |
| Cohere | `Zonit.Extensions.Ai.Cohere` | `AddAiCohere()` |
| Perplexity | `Zonit.Extensions.Ai.Perplexity` | `AddAiPerplexity()` |
| Alibaba (Qwen) | `Zonit.Extensions.Ai.Alibaba` | `AddAiAlibaba()` |
| Baidu (ERNIE) | `Zonit.Extensions.Ai.Baidu` | `AddAiBaidu()` |
| Zhipu (GLM) | `Zonit.Extensions.Ai.Zhipu` | `AddAiZhipu()` |
| Moonshot (Kimi) | `Zonit.Extensions.Ai.Moonshot` | `AddAiMoonshot()` |
| 01.AI (Yi) | `Zonit.Extensions.Ai.Yi` | `AddAiYi()` |

## Install and register

```bash
dotnet add package Zonit.Extensions.Ai.OpenAi
```

```csharp
builder.Services.AddAiOpenAi();                 // reads "Ai:OpenAi" (configuration.md)
```

Concrete model classes (`GPT5`, `GPTImage15`, `Sonnet46`) live in the chosen package under
`Llm/`. Pick them with IntelliSense; do not hardcode names from memory.

## Anthropic over the Claude Code CLI (subscription, no API key)

`AddAiAnthropic()` uses the HTTP API by default. The **same** provider can instead run through the
local **Claude Code CLI** (`claude -p`) — using your `claude login` subscription rather than an API
key — via `AddAiAnthropic(AnthropicTransport.Sdk)` (or `Auto`). Tool-using **agents** on that path
also need the opt-in `Zonit.Extensions.Ai.Sdk` package (`AddAiAgentToolBridge()`). Because the CLI is
not behaviourally identical to the API, the choice is explicit — full guide: [`sdk.md`](./sdk.md).
