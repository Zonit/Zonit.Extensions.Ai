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

- **Isolated by default.** Each `claude -p` run sees only the request: Claude Code's own system
  prompt is replaced by the request's system text, and the run gets no built-in Claude Code tools,
  no claude.ai connectors or other MCP servers, no `CLAUDE.md`, memory or user/project settings,
  and a fresh empty working directory (see [Isolation](#isolation)). It is still Claude Code
  underneath (its sampling defaults and tool handling), so output can differ slightly from the API.
  For reproducible, API-defined behaviour, use `Api`.
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
| Windows | `%APPDATA%\Claude\claude-code\<version>\[<build>\]claude.exe` (and `%LOCALAPPDATA%\…`) |
| macOS | `~/Library/Application Support/Claude/claude-code/<version>/[<build>/]claude` |
| Linux | `~/.config/Claude/claude-code/<version>/[<build>/]claude` |

Newer Desktop builds put the binary one folder deeper, in a build folder under the version
(`…\claude-code\2.1.330\9424831ad51c\claude.exe`); both layouts are probed. On Windows, when
discovery finds the npm shim `claude.cmd`, the native `claude.exe` that npm installs next to it
(`node_modules\@anthropic-ai\claude-code\bin\claude.exe`) is used instead, so no argument passes
through `cmd.exe`.

Set `ExecutablePath` only when the binary lives somewhere non-standard or off the service
account's `PATH`; a configured-but-missing path fails loudly rather than silently auto-discovering.

| Setting | Meaning |
| :--- | :--- |
| `ExecutablePath` | Absolute path to `claude`; unset = auto-discover |
| `PermissionMode` | Passed as `--permission-mode` (e.g. `acceptEdits`, `plan`, `bypassPermissions`) |
| `OAuthToken` | `claude setup-token` value → `CLAUDE_CODE_OAUTH_TOKEN` env var (CI / headless) |
| `AuthToken` | Bearer for an LLM gateway → `ANTHROPIC_AUTH_TOKEN` env var |
| `Isolated` | `true` (default): the run sees only the request — see [Isolation](#isolation). `false`: the full Claude Code session |
| `WorkingDirectory` | Process working directory; unset = a fresh empty directory when isolated, else the host process directory |
| `Timeout` | Hard wall-clock cap per CLI run (else provider `Timeout`, then `Ai:Resilience.TotalRequestTimeout`) |
| `AdditionalArguments` / `AdditionalEnvironment` | Extra CLI args / environment variables |

Auth defaults to the machine's ambient `claude login`; set `OAuthToken` for non-interactive
(CI / headless) runs. Requests the CLI cannot represent (image/PDF attachments) fall back to the
HTTP API under `Auto`, or throw under `Sdk`.

**Prompt length.** The prompt is written to the CLI's **stdin** and the system prompt and MCP
config to temporary files, so no command-line limit applies (on Windows `cmd.exe` stops at 8,191
characters and `CreateProcess` at 32,767). The files live in a per-run temporary folder that is
deleted when the run ends.

## Isolation

With `Isolated = true` (the default) every invocation adds:

| Flag / change | Effect |
| :--- | :--- |
| `--tools ""` | No built-in Claude Code tools (Bash, Read, Write, Edit, Agent…). An agent sees only the tools it registered, through the bridge. |
| `--strict-mcp-config` | No MCP servers besides the bridge's `--mcp-config` — the signed-in claude.ai account's connectors and user/project MCP servers stay out. |
| `--setting-sources local`, `--disable-slash-commands`, `--no-session-persistence` | No user or project settings, `CLAUDE.md` (also not from parent folders), memory, skills or slash commands; no saved session. |
| `--system-prompt-file` | The request's system text **replaces** Claude Code's system prompt (empty when the request has none, as on the API). |
| fresh working directory | An empty temporary directory, unless `WorkingDirectory` is set. |
| environment | Removes `CLAUDECODE`, `CLAUDE_CODE_*`, `CLAUDE_AGENT_SDK_*` and `ANTHROPIC_API_KEY`, so a host started inside a Claude Code session behaves like any other and the subscription is billed. Kept: `CLAUDE_CODE_OAUTH_TOKEN`, `CLAUDE_CODE_USE_BEDROCK` / `_VERTEX` / `_FOUNDRY` and their `SKIP_*_AUTH`, `CLAUDE_CODE_GIT_BASH_PATH`, `CLAUDE_CODE_CLIENT_*`. `AdditionalEnvironment` is applied last and can re-add anything. |

Set `Isolated = false` to run the full Claude Code session instead: its built-in tools, the
account's connectors, `CLAUDE.md`, memory and its own system prompt (the request's system text is
then appended with `--append-system-prompt-file`). `AdditionalArguments` are appended after
everything above.

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

What the result reports on the CLI path:

- `MaxIterations` is passed as `--max-turns`, so the limit means the same on both transports; a
  run that reaches it fails with `AgentIterationLimitException`, whose `Partial` carries the
  iterations, tool calls and usage of the run.
- `ToolCalls` lists every tool call the CLI made (name, input, result or error, duration), read
  from the CLI's `stream-json` output; each is also emitted as an `AgentToolCallCompletedEvent`.
- `Iterations` counts the model round-trips inside the run.

> **Loop ownership still differs on the CLI path.** Because the CLI drives the loop,
> `MaxParallelToolCalls`, `OnToolCall`, the per-tool timeout and nested-usage tracking do **not**
> apply, and token usage is taken from the CLI's own report. Use the `Api` transport when you need
> those controls.
