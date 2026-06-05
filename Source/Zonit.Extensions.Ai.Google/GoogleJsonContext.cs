using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Google;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GeminiResponse))]
[JsonSerializable(typeof(GeminiCandidate))]
[JsonSerializable(typeof(GeminiContent))]
[JsonSerializable(typeof(GeminiPart))]
[JsonSerializable(typeof(GeminiUsageMetadata))]
[JsonSerializable(typeof(EmbeddingResponse))]
[JsonSerializable(typeof(EmbeddingData))]
[JsonSerializable(typeof(GeminiRequest))]
[JsonSerializable(typeof(GeminiRequestContent))]
[JsonSerializable(typeof(GeminiPartItem))]
[JsonSerializable(typeof(GeminiInlineData))]
[JsonSerializable(typeof(GeminiFunctionResponse))]
[JsonSerializable(typeof(GeminiFunctionCall))]
[JsonSerializable(typeof(GeminiGenerationConfig))]
[JsonSerializable(typeof(GeminiSystemInstruction))]
[JsonSerializable(typeof(GeminiToolGroup))]
[JsonSerializable(typeof(GeminiFunctionDeclaration))]
[JsonSerializable(typeof(GeminiEmbedRequest))]
[JsonSerializable(typeof(GeminiEmbedContent))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class GoogleJsonContext : JsonSerializerContext
{
}
