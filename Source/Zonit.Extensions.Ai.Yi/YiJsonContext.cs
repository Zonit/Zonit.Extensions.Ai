using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Yi;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(YiResponse))]
[JsonSerializable(typeof(YiChoice))]
[JsonSerializable(typeof(YiMessage))]
[JsonSerializable(typeof(YiUsage))]
[JsonSerializable(typeof(YiStreamChunk))]
[JsonSerializable(typeof(YiStreamChoice))]
[JsonSerializable(typeof(YiStreamDelta))]
[JsonSerializable(typeof(YiChatRequest))]
[JsonSerializable(typeof(YiRequestMessage))]
[JsonSerializable(typeof(YiResponseFormat))]
internal sealed partial class YiJsonContext : JsonSerializerContext
{
}
