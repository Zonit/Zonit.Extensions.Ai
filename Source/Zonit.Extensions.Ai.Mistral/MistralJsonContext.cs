using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Mistral;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MistralResponse))]
[JsonSerializable(typeof(MistralChoice))]
[JsonSerializable(typeof(MistralMessage))]
[JsonSerializable(typeof(MistralUsage))]
[JsonSerializable(typeof(EmbeddingResponse))]
[JsonSerializable(typeof(EmbeddingData))]
[JsonSerializable(typeof(StreamChunk))]
[JsonSerializable(typeof(StreamChoice))]
[JsonSerializable(typeof(StreamDelta))]
[JsonSerializable(typeof(MistralChatRequest))]
[JsonSerializable(typeof(MistralRequestMessage))]
[JsonSerializable(typeof(MistralResponseFormat))]
[JsonSerializable(typeof(MistralEmbedRequest))]
internal sealed partial class MistralJsonContext : JsonSerializerContext
{
}
