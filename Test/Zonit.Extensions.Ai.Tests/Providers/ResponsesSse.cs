using System.Text;
using System.Text.Json;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Renders a buffered Responses-API body (OpenAI or xAI) into the SSE frame sequence the API
/// actually sends, so tests can keep expressing an expectation as one readable JSON object.
/// </summary>
/// <remarks>
/// The single-shot paths (<c>GenerateAsync</c> / <c>ChatAsync</c>) and the agent loop
/// request <c>stream: true</c> and reassemble the frames — a buffered body would no
/// longer be parsed at all. Going through this factory also means every test doubles as
/// a check that assembling a stream reproduces the buffered shape exactly.
/// </remarks>
internal static class ResponsesSse
{
    /// <summary>
    /// Converts a non-streaming response body into the equivalent frame sequence:
    /// <c>response.created</c> → <c>response.output_text.delta</c> per text fragment →
    /// the terminal event carrying the complete Response object. The terminal event is
    /// picked from <c>status</c> (<c>failed</c> / <c>incomplete</c> / otherwise
    /// <c>completed</c>), which is how the API reports those outcomes on a stream.
    /// Text is split across two deltas, which is how it really arrives.
    /// </summary>
    public static string FromResponseJson(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : "resp_test";
        var status = root.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;

        var sb = new StringBuilder();
        void Frame(string json) => sb.Append("data: ").Append(json).Append("\n\n");
        static string Str(string? value) => JsonSerializer.Serialize(value);

        Frame("{\"type\":\"response.created\",\"response\":{\"id\":" + Str(id) + ",\"status\":\"in_progress\"}}");

        foreach (var text in EnumerateOutputText(root))
        {
            var half = text.Length / 2;
            Frame("{\"type\":\"response.output_text.delta\",\"delta\":" + Str(text[..half]) + "}");
            Frame("{\"type\":\"response.output_text.delta\",\"delta\":" + Str(text[half..]) + "}");
        }

        var terminal = status switch
        {
            "failed" => "response.failed",
            "incomplete" => "response.incomplete",
            _ => "response.completed",
        };

        // Compacted: an SSE frame is one line, so a pretty-printed body would break the stream —
        // and the API itself always sends the response object on a single line.
        Frame("{\"type\":\"" + terminal + "\",\"response\":" + Compact(root) + "}");

        return sb.ToString();
    }

    private static string Compact(JsonElement element) => JsonSerializer.Serialize(element);

    private static IEnumerable<string> EnumerateOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var t) && t.GetString() == "output_text"
                    && part.TryGetProperty("text", out var textEl)
                    && textEl.GetString() is { Length: > 0 } text)
                {
                    yield return text;
                }
            }
        }
    }
}
