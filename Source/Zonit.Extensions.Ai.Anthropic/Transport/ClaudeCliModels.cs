namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Parsed shape of <c>claude -p --output-format json</c> (a single JSON object on
/// stdout). Property names map to the CLI's snake_case via
/// <c>AnthropicJsonContext</c>'s <c>SnakeCaseLower</c> policy.
/// </summary>
internal sealed class ClaudeCliResult
{
    /// <summary><c>"result"</c> on success, <c>"error"</c> on failure.</summary>
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    /// <summary>The assistant's final text — or, for structured output, the JSON object as text.</summary>
    public string? Result { get; set; }
    public string? SessionId { get; set; }
    public double? TotalCostUsd { get; set; }
    public int? NumTurns { get; set; }
    public bool IsError { get; set; }
    public string? Model { get; set; }
    public ClaudeCliUsage? Usage { get; set; }
}

/// <summary>Token usage block from the CLI JSON output (same field names as the API).</summary>
internal sealed class ClaudeCliUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadInputTokens { get; set; }
    public int CacheCreationInputTokens { get; set; }
}

/// <summary>
/// One newline-delimited line of <c>claude -p --output-format stream-json</c>. The CLI
/// emits objects of differing shapes (<c>system</c>/init, <c>assistant</c>, <c>user</c>,
/// <c>result</c>); we model the loose union and pick out assistant text plus the
/// terminal <c>result</c>.
/// </summary>
internal sealed class ClaudeCliStreamLine
{
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    /// <summary>Present on the terminal <c>"result"</c> line.</summary>
    public string? Result { get; set; }
    public bool IsError { get; set; }
    /// <summary>Wraps the message for <c>"assistant"</c>/<c>"user"</c> lines.</summary>
    public ClaudeCliMessage? Message { get; set; }
}

/// <summary>The <c>message</c> object inside an <c>assistant</c>/<c>user</c> stream line.</summary>
internal sealed class ClaudeCliMessage
{
    // Reuses AnthropicContent (Type/Text) — assistant content blocks carry the text.
    public AnthropicContent[]? Content { get; set; }
}
