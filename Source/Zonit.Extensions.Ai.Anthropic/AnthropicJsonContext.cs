using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Anthropic;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AnthropicResponse))]
[JsonSerializable(typeof(AnthropicContent))]
[JsonSerializable(typeof(AnthropicUsage))]
[JsonSerializable(typeof(StreamEvent))]
[JsonSerializable(typeof(StreamDelta))]
[JsonSerializable(typeof(AnthropicMessagesRequest))]
[JsonSerializable(typeof(AnthropicMessageItem))]
[JsonSerializable(typeof(AnthropicContentBlock))]
[JsonSerializable(typeof(AnthropicSource))]
[JsonSerializable(typeof(AnthropicTool))]
[JsonSerializable(typeof(AnthropicThinking))]
internal sealed partial class AnthropicJsonContext : JsonSerializerContext
{
}
