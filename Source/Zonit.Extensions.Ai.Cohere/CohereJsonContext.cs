using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Cohere;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CohereResponse))]
[JsonSerializable(typeof(CohereMessage))]
[JsonSerializable(typeof(CohereContent))]
[JsonSerializable(typeof(CohereUsage))]
[JsonSerializable(typeof(CohereTokens))]
[JsonSerializable(typeof(CohereEmbedResponse))]
[JsonSerializable(typeof(CohereEmbeddings))]
[JsonSerializable(typeof(CohereMeta))]
[JsonSerializable(typeof(CohereBilledUnits))]
[JsonSerializable(typeof(CohereStreamChunk))]
[JsonSerializable(typeof(CohereDelta))]
[JsonSerializable(typeof(CohereDeltaMessage))]
[JsonSerializable(typeof(CohereDeltaContent))]
[JsonSerializable(typeof(CohereChatRequest))]
[JsonSerializable(typeof(CohereRequestMessage))]
[JsonSerializable(typeof(CohereResponseFormat))]
[JsonSerializable(typeof(CohereEmbedRequest))]
internal sealed partial class CohereJsonContext : JsonSerializerContext
{
}
