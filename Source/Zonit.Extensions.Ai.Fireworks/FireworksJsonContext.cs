using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Fireworks;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FireworksResponse))]
[JsonSerializable(typeof(FireworksChoice))]
[JsonSerializable(typeof(FireworksMessage))]
[JsonSerializable(typeof(FireworksUsage))]
[JsonSerializable(typeof(FireworksStreamChunk))]
[JsonSerializable(typeof(FireworksStreamChoice))]
[JsonSerializable(typeof(FireworksStreamDelta))]
[JsonSerializable(typeof(FireworksChatRequest))]
[JsonSerializable(typeof(FireworksRequestMessage))]
[JsonSerializable(typeof(FireworksResponseFormat))]
internal sealed partial class FireworksJsonContext : JsonSerializerContext
{
}
