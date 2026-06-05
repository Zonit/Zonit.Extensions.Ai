using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Perplexity;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PerplexityResponse))]
[JsonSerializable(typeof(PerplexityChoice))]
[JsonSerializable(typeof(PerplexityMessage))]
[JsonSerializable(typeof(PerplexityUsage))]
[JsonSerializable(typeof(PerplexityStreamChunk))]
[JsonSerializable(typeof(PerplexityStreamChoice))]
[JsonSerializable(typeof(PerplexityStreamDelta))]
[JsonSerializable(typeof(PerplexityChatRequest))]
[JsonSerializable(typeof(PerplexityRequestMessage))]
internal sealed partial class PerplexityJsonContext : JsonSerializerContext
{
}
