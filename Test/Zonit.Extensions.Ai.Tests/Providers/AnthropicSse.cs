using System.Text;
using System.Text.Json;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// Renders a buffered Anthropic response into the SSE frame sequence the API actually
/// sends, so tests can keep expressing an expectation as one readable JSON object.
/// </summary>
/// <remarks>
/// The single-shot paths (<c>GenerateAsync</c> / <c>ChatAsync</c>) request
/// <c>stream: true</c> and reassemble the frames — a buffered body would no longer be
/// parsed at all. Going through this factory also means every test doubles as a check
/// that assembling a stream reproduces the buffered shape exactly.
/// </remarks>
internal static class AnthropicSse
{
    /// <summary>
    /// Converts a non-streaming response body into the equivalent frame sequence:
    /// <c>message_start</c> → per-block <c>content_block_start</c>/<c>_delta</c>/<c>_stop</c>
    /// → <c>message_delta</c> (carrying <c>stop_reason</c>) → <c>message_stop</c>.
    /// Tool inputs are split across two <c>input_json_delta</c> fragments, which is how
    /// structured output really arrives and exercises the concatenation path.
    /// </summary>
    public static string FromResponseJson(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : "msg_test";
        var usage = root.TryGetProperty("usage", out var usageEl)
            ? usageEl.GetRawText()
            : """{"input_tokens":0,"output_tokens":0}""";
        var stopReason = root.TryGetProperty("stop_reason", out var srEl) ? srEl.GetString() : "end_turn";

        // Built by concatenation rather than raw-string interpolation: the payloads are
        // themselves brace-heavy JSON, which fights the `$$"""..."""` delimiters.
        var sb = new StringBuilder();
        void Frame(string json) => sb.Append("data: ").Append(json).Append("\n\n");
        static string Str(string? value) => JsonSerializer.Serialize(value);

        Frame("{\"type\":\"message_start\",\"message\":{\"id\":" + Str(id) + ",\"usage\":" + usage + "}}");

        var index = 0;
        if (root.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in contentEl.EnumerateArray())
            {
                var type = block.TryGetProperty("type", out var tEl) ? tEl.GetString() : null;
                var head = "{\"type\":\"content_block_start\",\"index\":" + index + ",\"content_block\":";
                var deltaHead = "{\"type\":\"content_block_delta\",\"index\":" + index + ",\"delta\":";

                switch (type)
                {
                    case "text":
                        var text = block.TryGetProperty("text", out var txtEl) ? txtEl.GetString() ?? "" : "";
                        Frame(head + "{\"type\":\"text\",\"text\":\"\"}}");
                        Frame(deltaHead + "{\"type\":\"text_delta\",\"text\":" + Str(text) + "}}");
                        break;

                    case "tool_use":
                        var name = block.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                        var input = block.TryGetProperty("input", out var iEl) ? iEl.GetRawText() : "{}";
                        Frame(head + "{\"type\":\"tool_use\",\"id\":\"toolu_test\",\"name\":" + Str(name) + ",\"input\":{}}}");
                        var half = input.Length / 2;
                        Frame(deltaHead + "{\"type\":\"input_json_delta\",\"partial_json\":" + Str(input[..half]) + "}}");
                        Frame(deltaHead + "{\"type\":\"input_json_delta\",\"partial_json\":" + Str(input[half..]) + "}}");
                        break;

                    default:
                        Frame(head + "{\"type\":" + Str(type) + "}}");
                        break;
                }

                Frame("{\"type\":\"content_block_stop\",\"index\":" + index + "}");
                index++;
            }
        }

        Frame("{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":" + Str(stopReason) + "},\"usage\":" + usage + "}");
        Frame("{\"type\":\"message_stop\"}");

        return sb.ToString();
    }
}
