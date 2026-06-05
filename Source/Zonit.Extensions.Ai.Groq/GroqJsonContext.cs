using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Groq;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GroqResponse))]
[JsonSerializable(typeof(GroqChoice))]
[JsonSerializable(typeof(GroqMessage))]
[JsonSerializable(typeof(GroqUsage))]
[JsonSerializable(typeof(GroqStreamChunk))]
[JsonSerializable(typeof(GroqStreamChoice))]
[JsonSerializable(typeof(GroqStreamDelta))]
[JsonSerializable(typeof(GroqChatRequest))]
[JsonSerializable(typeof(GroqRequestMessage))]
[JsonSerializable(typeof(GroqResponseFormat))]
[JsonSerializable(typeof(GroqJsonSchemaSpec))]
internal sealed partial class GroqJsonContext : JsonSerializerContext
{
}
