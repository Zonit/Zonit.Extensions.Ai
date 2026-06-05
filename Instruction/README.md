# Instruction

Task-focused guides for **Zonit.Extensions.Ai**. These files are the single source of truth.
They are browsable here on GitHub, shipped inside the NuGet package, and compiled into your AI
coding assistant (GitHub Copilot, Claude Code, Cursor) when you install the package, so your
agent learns the library without being prompted. The [main README](../Readme.md#ai-assistant-ready)
explains how that works.

New here? Start with [usage.md](./usage.md).

| Guide | What it covers |
| :--- | :--- |
| [usage.md](./usage.md) | Getting started, the unified `GenerateAsync` API, prompt vs agent, each modality |
| [providers.md](./providers.md) | Which provider NuGet to install for a capability (text, images, audio, embeddings) |
| [configuration.md](./configuration.md) | DI registration, `appsettings.json`, resilience |
| [models.md](./models.md) | Capability interfaces, picking a model, reasoning and fast mode |
| [prompts.md](./prompts.md) | Writing a prompt class (`PromptBase`, Scriban, structured output) |
| [prompt-library.md](./prompt-library.md) | Ready-made prompts from `Zonit.Extensions.Ai.Prompts` |
| [tools.md](./tools.md) | Writing an agent tool (`ToolBase`) |
| [chat.md](./chat.md) | Multi-turn chat (`ChatAsync`, `ChatStreamAsync`) |
| [agents.md](./agents.md) | Agents: tool-calling, MCP, streaming events |
| [results.md](./results.md) | `Result`, `MetaData`, token usage and cost, `ResultAgent` roll-ups |

These files are the authored source. At consumer build time the package projects them into
`.zonit/extensions/ai/` plus editor-native rule files (`.cursor/rules/`, `.github/instructions/`,
`CLAUDE.md`). Edit a file here and every projection updates.
