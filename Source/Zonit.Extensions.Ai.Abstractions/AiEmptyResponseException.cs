namespace Zonit.Extensions.Ai;

/// <summary>
/// Stable error codes for the cases where a model call finished but produced
/// nothing the caller can use. The numeric value is part of the public
/// contract — it is rendered into the exception message as <c>[AI-E&lt;code&gt;]</c>
/// and is safe to switch on / log / alert on. See <c>Instruction/errors.md</c>
/// for the full catalog.
/// </summary>
public enum AiResponseError
{
    /// <summary>
    /// <c>AI-E1001</c> — the model returned no usable content (only reasoning,
    /// or nothing) on an otherwise-successful response, or a stream truncated
    /// before any content arrived. Server-side data loss. Transient: a later
    /// re-run usually succeeds. On retrying call paths it is raised only after
    /// the retry budget is spent.
    /// </summary>
    EmptyAfterRetries = 1001,

    /// <summary>
    /// <c>AI-E1002</c> — the response terminated because the output token budget
    /// was exhausted before any content (typically spent entirely on reasoning).
    /// NOT transient: retrying re-truncates. Raise the model's <c>MaxTokens</c>
    /// or lower the reasoning effort.
    /// </summary>
    Truncated = 1002,

    /// <summary>
    /// <c>AI-E1003</c> — the model declined the request (refusal / content
    /// filter). NOT transient and NOT a technical fault: the input tripped a
    /// safety boundary. Revise the prompt / inputs.
    /// </summary>
    Refusal = 1003,
}

/// <summary>
/// Thrown by <b>any</b> model call — single-shot <c>GenerateAsync</c>,
/// <c>ChatAsync</c>, the streaming variants, and the agent loop — when it
/// completes without content the caller can act on. A model answers unless
/// something technical went wrong (server-side data loss, truncation, a
/// refusal), so "no usable content" is a fault, not data: an exception is the
/// correct signal. The calling code does not run past it, so no empty artifact
/// is ever produced or published, and no caller needs an
/// <c>if (string.IsNullOrWhiteSpace(result.Value))</c> guard.
/// </summary>
/// <remarks>
/// Deliberately a plain <see cref="Exception"/>, not tied to the agent loop:
/// the failure originates at the provider/transport level and can surface on
/// every call path, not only inside an agent. Inspect <see cref="Code"/> to
/// branch on the failure mode; the message always carries the stable
/// <c>[AI-E&lt;code&gt;]</c> tag. Only <see cref="AiResponseError.EmptyAfterRetries"/>
/// is transient (and, on retrying paths, is raised only after in-process retries
/// have already failed — recovery means re-running the whole operation later).
/// </remarks>
public sealed class AiEmptyResponseException : Exception
{
    /// <summary>The classified failure mode (and its stable numeric code).</summary>
    public AiResponseError Code { get; }

    /// <summary>The provider's terminal stop/finish reason, when known (e.g. <c>end_turn</c>, <c>max_tokens</c>, <c>refusal</c>, or <c>null</c> when a stream truncated).</summary>
    public string? StopReason { get; }

    /// <summary>How many attempts were made before giving up (1 = no retry budget / non-retryable code / a non-retrying call path).</summary>
    public int Attempts { get; }

    /// <inheritdoc/>
    public AiEmptyResponseException(
        AiResponseError code,
        string message,
        string? stopReason = null,
        int attempts = 1,
        Exception? innerException = null)
        : base($"[AI-E{(int)code}] {message}", innerException)
    {
        Code = code;
        StopReason = stopReason;
        Attempts = attempts;
    }
}
