using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Zhipu;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ZhipuResponse))]
[JsonSerializable(typeof(ZhipuChoice))]
[JsonSerializable(typeof(ZhipuMessage))]
[JsonSerializable(typeof(ZhipuUsage))]
[JsonSerializable(typeof(ZhipuStreamChunk))]
[JsonSerializable(typeof(ZhipuStreamChoice))]
[JsonSerializable(typeof(ZhipuStreamDelta))]
[JsonSerializable(typeof(ZhipuChatRequest))]
[JsonSerializable(typeof(ZhipuRequestMessage))]
[JsonSerializable(typeof(ZhipuResponseFormat))]
internal sealed partial class ZhipuJsonContext : JsonSerializerContext
{
}
