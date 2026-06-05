# Configuration and registration

## Register providers

Extension methods live in namespace `Zonit.Extensions`. Each `AddAi{Provider}()` binds its
configuration section, wires the core services and a resilient `HttpClient`, and is idempotent,
so it is safe to call from multiple modules or plugins without duplicating registrations.

```csharp
using Zonit.Extensions;

builder.Services.AddAiOpenAi();     // binds "Ai:OpenAi"
builder.Services.AddAiAnthropic();  // binds "Ai:Anthropic"
```

See [`providers.md`](./providers.md) for the full provider-to-method list.

## Keys from configuration (recommended)

Keep keys in User Secrets, environment variables or a vault. Do not hardcode them.

```json
{
  "Ai": {
    "OpenAi":    { "ApiKey": "sk-...", "OrganizationId": "org-...", "Timeout": "00:15:00" },
    "Anthropic": { "ApiKey": "sk-ant-..." }
  }
}
```

Each provider extends a common `AiProviderOptions` (`ApiKey`, `BaseUrl`, `Timeout`) plus its own
fields, such as OpenAI's `OrganizationId`. A provider's `Timeout` overrides the global resilience
timeout.

## Keys and options in code

`appsettings.json` is bound first; code overrides are applied on top through `PostConfigure`, so
set only what you need.

```csharp
builder.Services.AddAiOpenAi("sk-...");                       // API key inline
builder.Services.AddAiOpenAi(o =>                             // or full options
{
    o.OrganizationId = "org-...";
    o.BaseUrl        = "https://my-azure-openai.openai.azure.com";  // e.g. Azure OpenAI
    o.Timeout        = TimeSpan.FromMinutes(15);
});
```

## Resilience

Retry, timeout and circuit breaker run through `Microsoft.Extensions.Http.Resilience`, tuned for
long AI calls. Configure them under `Ai:Resilience` or in code with `AddAi(o => o.Resilience...)`.

| Setting | Default | Meaning |
| :--- | :--- | :--- |
| `TotalRequestTimeout` | 40 min | Whole pipeline including retries |
| `AttemptTimeout` | 10 min | One attempt |
| `MaxRetryAttempts` | 3 | Retries on transient failures |
| `RetryBaseDelay` / `RetryMaxDelay` | 2 s / 30 s | Exponential backoff bounds |
| `UseJitter` | `true` | Randomise delays |
| `CircuitBreakerFailureRatio` | 0.5 | Failure ratio that opens the circuit |

```csharp
builder.Services.AddAi(o =>
{
    o.Resilience.MaxRetryAttempts = 5;
    o.Resilience.AttemptTimeout   = TimeSpan.FromMinutes(15);
});
```

Requests are retried on network errors, timeouts, HTTP 429 and HTTP 5xx.
