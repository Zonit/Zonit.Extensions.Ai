using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Baidu;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BaiduResponse))]
[JsonSerializable(typeof(BaiduChoice))]
[JsonSerializable(typeof(BaiduMessage))]
[JsonSerializable(typeof(BaiduUsage))]
[JsonSerializable(typeof(BaiduStreamChunk))]
[JsonSerializable(typeof(BaiduStreamChoice))]
[JsonSerializable(typeof(BaiduStreamDelta))]
[JsonSerializable(typeof(BaiduChatRequest))]
[JsonSerializable(typeof(BaiduRequestMessage))]
internal sealed partial class BaiduJsonContext : JsonSerializerContext
{
}
