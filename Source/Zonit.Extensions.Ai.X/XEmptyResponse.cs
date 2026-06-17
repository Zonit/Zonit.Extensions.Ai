using System.Text.Json;

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// Shared "this X response carried no usable content" classification and exception
/// building, used by every X call path (single-shot, chat, stream, agent) so the
/// <see cref="AiResponseError"/> codes and messages stay uniform.
/// </summary>
internal static class XEmptyResponse
{
    /// <summary>
    /// Maps an empty X Responses-API body to a stable code: <c>incomplete</c> /
    /// <c>max_output_tokens</c> → <see cref="AiResponseError.Truncated"/>, a
    /// content filter / refusal → <see cref="AiResponseError.Refusal"/>, anything
    /// else → <see cref="AiResponseError.EmptyAfterRetries"/> (transient data loss).
    /// Only the last is worth retrying.
    /// </summary>
    public static (AiResponseError Code, string? Reason, bool Retryable) Classify(string body)
    {
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            string? status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
            string? reason = root.TryGetProperty("incomplete_details", out var inc)
                && inc.ValueKind == JsonValueKind.Object
                && inc.TryGetProperty("reason", out var r) ? r.GetString() : null;

            if (reason is "max_output_tokens")
                return (AiResponseError.Truncated, reason, false);
            if (reason is "content_filter" || HasRefusal(root))
                return (AiResponseError.Refusal, reason ?? "refusal", false);

            return (AiResponseError.EmptyAfterRetries, reason ?? status, true);
        }
        catch (JsonException)
        {
            // Unparseable body on a 200 — treat as transient data loss.
            return (AiResponseError.EmptyAfterRetries, null, true);
        }
    }

    /// <summary>Builds the typed exception with a code-appropriate, actionable message.</summary>
    public static AiEmptyResponseException Build(string operation, string model, AiResponseError code, string? reason, int attempts)
        => new(code, code switch
        {
            AiResponseError.Truncated =>
                $"X {operation} on '{model}' returned no usable content: the output token budget was exhausted "
                + "(incomplete / max_output_tokens). Raise MaxTokens or lower the reasoning effort.",
            AiResponseError.Refusal =>
                $"X {operation} on '{model}' was declined (refusal / content filter). The model will not answer "
                + "this input; revise the prompt / inputs.",
            _ =>
                $"X {operation} on '{model}' returned an empty response after {attempts} attempt(s) — server-side "
                + "data loss. Usually transient; re-run the operation. Tune Ai:Resilience MaxRetryAttempts / "
                + "RetryBaseDelay / RetryMaxDelay.",
        }, reason, attempts);

    private static bool HasRefusal(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var contentArr) || contentArr.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var part in contentArr.EnumerateArray())
                if (part.TryGetProperty("type", out var t) && t.GetString() == "refusal")
                    return true;
        }
        return false;
    }
}
