using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Alibaba;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AlibabaResponse))]
[JsonSerializable(typeof(AlibabaChoice))]
[JsonSerializable(typeof(AlibabaMessage))]
[JsonSerializable(typeof(AlibabaUsage))]
[JsonSerializable(typeof(AlibabaStreamChunk))]
[JsonSerializable(typeof(AlibabaStreamChoice))]
[JsonSerializable(typeof(AlibabaStreamDelta))]
[JsonSerializable(typeof(AlibabaChatRequest))]
[JsonSerializable(typeof(AlibabaRequestMessage))]
[JsonSerializable(typeof(AlibabaResponseFormat))]
[JsonSerializable(typeof(AlibabaJsonSchemaSpec))]
internal sealed partial class AlibabaJsonContext : JsonSerializerContext
{
}
