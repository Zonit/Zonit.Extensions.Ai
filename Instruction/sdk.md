# Anthropic over the Claude Code CLI (SDK transport)

The Anthropic provider can reach Claude two ways. This is **Anthropic-only**; every other
provider is HTTP-API only.

| Transport | How it runs | Auth | Notes |
| :--- | :--- | :--- | :--- |
| `Api` **(default)** | HTTP Messages API | `ApiKey` | Reproducible, server-side; always available. |
| `Sdk` | Local **Claude Code CLI** (`claude -p`) as a subprocess | the machine's `claude login` (no key) | Uses your Claude **subscription**. Requires `claude` installed. |
| `Auto` | CLI first, HTTP API for what the CLI can't do | `claude login` **+** `ApiKey` | Prefers the CLI when present, else the API. |

## Read this before choosing `Sdk` or `Auto`

The CLI is **not behaviourally identical to the API**, so the transport must be a
deliberate, explicit choice:

- **Different system prompt.** `claude -p` runs through *Claude Code*, which applies its own
  agent system prompt. The same prompt can therefore produce **different** output than a clean
  API call. For reproducible, API-defined behaviour, use `Api`.
- **Not always installed.** `Sdk` needs the `claude` binary present and a `claude login`
  session. On a host without it, `Sdk` **throws** (it never silently degrades). If you want
  "CLI on my machine, API in production", use `Auto` with an `ApiKey` — or select the transport
  per environment from configuration.
- **No silent surprises.** The default is `Api`; supplying an `ApiKey` keeps you on the API.
  The CLI is used **only** when you ask for `Sdk`/`Auto` explicitly. `Auto` prefers the CLI when
  it is present (so its different system prompt applies) — pick it knowingly.
- **Prompt caching** is automatic inside the CLI, so the model's `Cache` markers are ignored on
  the SDK transport.

## Registration — transport is an explicit argument

```csharp
using Zonit.Extensions;

builder.Services.AddAiAnthropic();                                  // Api (default), key from config
builder.Services.AddAiAnthropic("sk-ant-...");                      // Api, key inline
builder.Services.AddAiAnthropic(AnthropicTransport.Sdk);            // claude -p (subscription)
builder.Services.AddAiAnthropic(AnthropicTransport.Auto, o => o.ApiKey = "sk-ant-..."); // CLI, API fallback
```

The transport argument is **authoritative** — it wins over anything in the options lambda or
configuration, so the choice is unambiguous at the call site. Configuration can also select it
per environment without recompiling:

```json
{
  "Ai": {
    "Anthropic": {
      "Transport": "Auto",
      "ApiKey": "sk-ant-...",
      "Cli": { "ExecutablePath": "C:\\Users\\me\\.local\\bin\\claude.exe", "PermissionMode": "acceptEdits" }
    }
  }
}
```

> When code passes an explicit transport argument it overrides `"Ai:Anthropic:Transport"`. Use
> the no-argument `AddAiAnthropic()` (or the config value) when you want configuration to decide.

## CLI options (`AnthropicCliOptions`, bound from `Ai:Anthropic:Cli`)

The binary is auto-discovered per OS, so no path is normally needed. Resolution order:
`PATH` → npm global bin → the native installer's `~/.local/bin` → the **Claude Desktop**-bundled
CLI (newest version first). Concretely the Desktop bundle is probed at:

| OS | Claude Desktop bundle location |
| :--- | :--- |
| Windows | `%APPDATA%\Claude\claude-code\<version>\claude.exe` (and `%LOCALAPPDATA%\…`) |
| macOS | `~/Library/Application Support/Claude/claude-code/<version>/claude` |
| Linux | `~/.config/Claude/claude-code/<version>/claude` |

Set `ExecutablePath` only when the binary lives somewhere non-standard or off the service
account's `PATH`; a configured-but-missing path fails loudly rather than silently auto-discovering.

| Setting | Meaning |
| :--- | :--- |
| `ExecutablePath` | Absolute path to `claude`; unset = auto-discover |
| `PermissionMode` | Passed as `--permission-mode` (e.g. `acceptEdits`, `plan`, `bypassPermissions`) |
| `OAuthToken` | `claude setup-token` value → `CLAUDE_CODE_OAUTH_TOKEN` env var (CI / headless) |
| `AuthToken` | Bearer for an LLM gateway → `ANTHROPIC_AUTH_TOKEN` env var |
| `WorkingDirectory` | Process working directory |
| `Timeout` | Hard wall-clock cap per CLI run (else provider `Timeout`, then `Ai:Resilience.TotalRequestTimeout`) |
| `AdditionalArguments` / `AdditionalEnvironment` | Extra CLI args / environment variables |

Auth defaults to the machine's ambient `claude login`; set `OAuthToken` for non-interactive
(CI / headless) runs. Requests the CLI cannot represent (image/PDF attachments) fall back to the
HTTP API under `Auto`, or throw under `Sdk`.

## Tool-using agents over the CLI

On `Sdk`/`Auto` the **CLI owns the agent loop** and executes tools itself. To let it call **your**
C# tools (`ToolBase`, scoped tools, sub-agents, proxied MCP tools), install the opt-in bridge and
register it — it publishes your tool set as a secured **loopback (`127.0.0.1`) MCP server** with a
per-run bearer token (never reachable off the machine):

```bash
dotnet add package Zonit.Extensions.Ai.Sdk
```

```csharp
builder.Services.AddAiAnthropic(AnthropicTransport.Sdk);  // agents via claude -p
builder.Services.AddAiAgentToolBridge();                  // expose this app's tools to the CLI
```

The agent builder ([`agents.md`](./agents.md)) is unchanged:

```csharp
await ai.Agent(new Sonnet46(), prompt)
    .AddTool<SaveNoteTool>()     // executed in-process; the CLI calls it over the loopback bridge
    .RunAsync();
```

Routing: a tool-using agent uses the CLI + bridge. If the bridge is not registered (or the CLI is
not installed), `Auto` falls back to the HTTP API when an `ApiKey` is set, while `Sdk` throws a
clear error.

> **Loop ownership differs on the CLI path.** Because the CLI drives the loop, the framework-side
> gates (`MaxIterations`, `MaxParallelToolCalls`, `OnToolCall`, per-tool timeout) and nested-usage
> tracking do **not** apply; token usage is taken from the CLI's own report. Use the `Api`
> transport when you need those controls.
