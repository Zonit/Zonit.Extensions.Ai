using System.Text.Json;

namespace Zonit.Extensions.Ai.Anthropic;

// Wire models shared by AnthropicProvider, the transports (AnthropicApiTransport /
// AnthropicCliTransport) and AnthropicAgentSession. Kept project-internal — they are
// the canonical request/response shape; provider code builds requests and parses
// responses transport-agnostically, and each transport only moves bytes.

// Response models
internal sealed class AnthropicResponse
{
    public string? Id { get; set; }
    public AnthropicContent[]? Content { get; set; }
    public AnthropicUsage? Usage { get; set; }
    /// <summary>
    /// Why the model stopped: <c>end_turn</c> (normal completion),
    /// <c>max_tokens</c> (truncated), <c>tool_use</c> (model wants to call a
    /// client tool), <c>pause_turn</c> (server-side iteration limit reached
    /// for server tools — caller must re-issue with the same content array),
    /// <c>refusal</c> (model declined). Required for sensible diagnostics on
    /// the non-streaming path: a missing <c>text</c> block alone tells the
    /// caller nothing about whether the request needs more tokens, a retry,
    /// or has been refused.
    /// </summary>
    public string? StopReason { get; set; }
}

internal sealed class AnthropicContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    // tool_use blocks: the model's structured answer arrives as the tool's
    // `input` — Anthropic constrains it to the tool's input_schema and emits
    // well-formed JSON, so it parses safely even when free text would not.
    public string? Name { get; set; }
    public JsonElement? Input { get; set; }
}

internal sealed class AnthropicUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadInputTokens { get; set; }
    public int CacheCreationInputTokens { get; set; }
}

internal sealed class StreamEvent
{
    public string? Type { get; set; }
    public StreamDelta? Delta { get; set; }
}

internal sealed class StreamDelta
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    /// <summary>
    /// Terminal stop reason on the <c>message_delta</c> event. Surfaced for
    /// diagnostics in single-shot streaming paths so a silently-truncated
    /// response (max_tokens / pause_turn / refusal) is logged instead of
    /// just ending the IAsyncEnumerable.
    /// </summary>
    public string? StopReason { get; set; }
}

// Request models (AOT-safe DTO).
internal sealed class AnthropicMessagesRequest
{
    public string Model { get; set; } = "";
    public int MaxTokens { get; set; }
    /// <summary>Inference speed: <c>"fast"</c> opts into fast mode (requires the <c>fast-mode-2026-02-01</c> beta header). Null/omitted = standard speed.</summary>
    public string? Speed { get; set; }
    /// <summary>System prompt as content blocks. Array form is required to attach <c>cache_control</c>; Anthropic accepts it identically to the string form for non-cached requests.</summary>
    public List<AnthropicContentBlock>? System { get; set; }
    public List<AnthropicMessageItem> Messages { get; set; } = new();
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public bool? Stream { get; set; }
    public List<AnthropicTool>? Tools { get; set; }
    public AnthropicToolChoice? ToolChoice { get; set; }
    public AnthropicThinking? Thinking { get; set; }
    public AnthropicOutputConfig? OutputConfig { get; set; }
}

/// <summary>
/// Controls tool selection. <c>type</c>: <c>auto</c> (model decides — the only
/// value Anthropic allows while extended thinking is enabled), <c>tool</c>
/// (force the tool named by <see cref="Name"/>), <c>any</c>, or <c>none</c>.
/// </summary>
internal sealed class AnthropicToolChoice
{
    public string Type { get; set; } = "";
    public string? Name { get; set; }
}

internal sealed class AnthropicOutputConfig
{
    /// <summary>Adaptive-thinking effort hint: <c>low|medium|high|xhigh|max</c>.</summary>
    public string? Effort { get; set; }
}

internal sealed class AnthropicMessageItem
{
    public string Role { get; set; } = "";
    public List<AnthropicContentBlock> Content { get; set; } = new();
}

internal sealed class AnthropicContentBlock
{
    public string Type { get; set; } = "";
    public string? Text { get; set; }
    public AnthropicSource? Source { get; set; }
    public string? ToolUseId { get; set; }
    public string? Content { get; set; }
    public bool? IsError { get; set; }
    // tool_use blocks emitted by the assistant in agent sessions.
    public string? Id { get; set; }
    public string? Name { get; set; }
    public JsonElement? Input { get; set; }
    // thinking blocks (extended thinking).
    public string? Thinking { get; set; }
    public string? Signature { get; set; }
    /// <summary>
    /// Encrypted opaque payload for <c>redacted_thinking</c> blocks. Anthropic
    /// returns these when portions of the model's reasoning are safety-redacted
    /// (separate from the regular <c>thinking</c> stream). Per Anthropic docs
    /// the entire block — including this field — must be round-tripped
    /// unchanged on follow-up turns: filtering on
    /// <c>block.Type == "thinking"</c> alone silently drops them and breaks
    /// the multi-turn extended-thinking protocol, surfacing as agent turns
    /// that simply stop responding mid-task.
    /// </summary>
    public string? Data { get; set; }
    /// <summary>Optional cache breakpoint marking this block as the end of a cacheable prefix.</summary>
    public AnthropicCacheControl? CacheControl { get; set; }
}

internal sealed class AnthropicSource
{
    public string Type { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string Data { get; set; } = "";
}

internal sealed class AnthropicTool
{
    /// <summary>
    /// Optional discriminator for server-side tools (e.g. <c>web_search_20250305</c>,
    /// <c>code_execution_20250522</c>, <c>computer_20250124</c>). Function tools
    /// leave this null — Anthropic infers them by the presence of <c>input_schema</c>.
    /// </summary>
    public string? Type { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>
    /// JSON Schema describing the function arguments. Required for function
    /// tools; left <c>null</c> for server tools (web_search, code_execution,
    /// computer use) where Anthropic's wire shape forbids <c>input_schema</c>.
    /// Must stay nullable: <see cref="JsonElement"/> is a struct, so a non-
    /// nullable property in <c>default</c> state has <c>ValueKind == Undefined</c>
    /// and the source-generated converter throws
    /// <see cref="InvalidOperationException"/> from <c>JsonElementConverter.Write</c>
    /// on an "uninitialised JsonElement" — even with
    /// <c>DefaultIgnoreCondition = WhenWritingNull</c>, because a struct is
    /// never null.
    /// </summary>
    public JsonElement? InputSchema { get; set; }
    /// <summary>Marks a cache breakpoint covering this tool and everything before it (system + earlier tools).</summary>
    public AnthropicCacheControl? CacheControl { get; set; }

    // ---- Server-tool parameters (web_search_20250305) ----

    /// <summary>Maximum search invocations allowed in a single request.</summary>
    public int? MaxUses { get; set; }
    /// <summary>Optional allow-list filter; results outside this list are dropped.</summary>
    public List<string>? AllowedDomains { get; set; }
    /// <summary>Optional block-list filter; results from these domains are dropped.</summary>
    public List<string>? BlockedDomains { get; set; }
    /// <summary>Approximate user location passed through to the search engine.</summary>
    public AnthropicUserLocation? UserLocation { get; set; }
}

/// <summary>Anthropic <c>user_location</c> hint for server-side web search.</summary>
internal sealed class AnthropicUserLocation
{
    public string Type { get; set; } = "approximate";
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public string? Timezone { get; set; }
}

internal sealed class AnthropicCacheControl
{
    public string Type { get; set; } = "ephemeral";
    /// <summary><c>"1h"</c> for the 1-hour beta cache; null/omitted for the default 5-minute TTL.</summary>
    public string? Ttl { get; set; }
}

internal sealed class AnthropicThinking
{
    /// <summary>Either <c>"enabled"</c> (legacy + <see cref="BudgetTokens"/>) or <c>"adaptive"</c> (effort hint via <c>output_config</c>).</summary>
    public string Type { get; set; } = "";
    public int? BudgetTokens { get; set; }
}
