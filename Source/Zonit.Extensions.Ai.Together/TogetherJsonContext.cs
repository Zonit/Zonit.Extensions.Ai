using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Together;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TogetherResponse))]
[JsonSerializable(typeof(TogetherChoice))]
[JsonSerializable(typeof(TogetherMessage))]
[JsonSerializable(typeof(TogetherUsage))]
[JsonSerializable(typeof(TogetherStreamChunk))]
[JsonSerializable(typeof(TogetherStreamChoice))]
[JsonSerializable(typeof(TogetherStreamDelta))]
[JsonSerializable(typeof(TogetherChatRequest))]
[JsonSerializable(typeof(TogetherRequestMessage))]
[JsonSerializable(typeof(TogetherResponseFormat))]
[JsonSerializable(typeof(TogetherJsonSchemaSpec))]
internal sealed partial class TogetherJsonContext : JsonSerializerContext
{
}
