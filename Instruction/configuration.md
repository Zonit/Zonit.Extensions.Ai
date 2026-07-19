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
    "Anthropic": { "ApiKey": "sk-ant-..." },
    "ElevenLabs":{ "ApiKey": "sk_..." }
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

## Proxy

Route provider traffic through an outbound **HTTP or SOCKS proxy** — set it once under `Ai:Proxy`
and **every** provider uses it. Handy to reach a region-locked model (for example Grok 4.5 is
EU-blocked) through an exit node in an allowed region.

```json
{
  "Ai": {
    "Proxy": {
      "Address": "http://us-proxy.example.com:8080",   // or "socks5://host:1080"
      "Username": "user",                                // optional (authenticated proxies)
      "Password": "pass"                                 // optional
    }
  }
}
```

No `Address` means no proxy — the default, behaviour unchanged. Set `"Enabled": false` to keep the
address on file but switch the proxy off globally.

Opt a single provider **out** with `UseProxy` (default `true`) — every provider tunnels except the
ones you exclude:

```csharp
builder.Services.AddAiX();                                 // Grok  → through the proxy
builder.Services.AddAiAnthropic(o => o.UseProxy = false);  // Claude → connects directly
```

## Resilience

One section, `Ai:Resilience`, governs retry, timeout and circuit-breaker behaviour for **every
provider** — set it once and it applies everywhere. Configure it under `Ai:Resilience` or in code
with `AddAi(o => o.Resilience...)`.

| Setting | Default | Meaning |
| :--- | :--- | :--- |
| `TotalRequestTimeout` | 90 min | Whole pipeline including retries |
| `AttemptTimeout` | 30 min | One non-streaming attempt |
| `MaxRetryAttempts` | 6 | Retry budget for both HTTP-layer and stream-layer retries |
| `RetryBaseDelay` / `RetryMaxDelay` | 5 s / 60 s | Exponential backoff: first delay → steady cap |
| `InterEventTimeout` | 30 min | Max gap between two stream frames before the stream is declared dead (streaming providers) |
| `UseJitter` | `true` | Randomise HTTP-layer delays |
| `CircuitBreakerFailureRatio` | 0.5 | Failure ratio that opens the circuit |

```csharp
builder.Services.AddAi(o =>
{
    o.Resilience.MaxRetryAttempts = 30;   // ride out a longer outage (~28 min at the 60 s cap)
    o.Resilience.AttemptTimeout   = TimeSpan.FromMinutes(15);
});
```

### Two layers, one schedule

A retry is a retry whether the connection failed before or after a response started, so both layers
read the **same three knobs** (`MaxRetryAttempts`, `RetryBaseDelay`, `RetryMaxDelay`):

- **HTTP layer** — network errors, timeouts, HTTP 429 and HTTP 5xx (before any response arrives).
- **Stream / agent-turn layer** — failures the HTTP layer cannot see: a stalled or dropped stream,
  or a `200 OK` turn that carries **no usable content** (a server-side "empty turn").

The backoff ramps from `RetryBaseDelay`, doubling up to `RetryMaxDelay`, then holds at that cap as a
steady cadence (≈ 5 → 10 → 20 → 40 → 60 → 60 s with the defaults). It steps over the typical 30–90 s
provider incident window instead of firing every attempt inside it; raise `MaxRetryAttempts` to cover
longer outages. When the budget is spent on a still-empty turn the agent **throws** rather than
returning an empty value — see [`errors.md`](./errors.md).

## Anthropic: HTTP API or the Claude Code CLI

The Anthropic provider can run through the local Claude Code CLI (`claude -p`) instead of the HTTP
API. The transport defaults to `Api` and is chosen explicitly — `AddAiAnthropic(AnthropicTransport.Sdk)`
or `"Ai:Anthropic:Transport"` in config. That is its own topic (transport modes, the API-vs-CLI
behaviour difference, `Ai:Anthropic:Cli` options, auth, and tool-using agents over the CLI): see
[`sdk.md`](./sdk.md).
