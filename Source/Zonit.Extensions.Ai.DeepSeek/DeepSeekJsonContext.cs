using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.DeepSeek;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DeepSeekResponse))]
[JsonSerializable(typeof(DeepSeekChoice))]
[JsonSerializable(typeof(DeepSeekMessage))]
[JsonSerializable(typeof(DeepSeekUsage))]
[JsonSerializable(typeof(StreamChunk))]
[JsonSerializable(typeof(StreamChoice))]
[JsonSerializable(typeof(StreamDelta))]
[JsonSerializable(typeof(DeepSeekChatRequest))]
[JsonSerializable(typeof(DeepSeekRequestMessage))]
[JsonSerializable(typeof(DeepSeekResponseFormat))]
[JsonSerializable(typeof(DeepSeekJsonSchemaSpec))]
internal sealed partial class DeepSeekJsonContext : JsonSerializerContext
{
}
