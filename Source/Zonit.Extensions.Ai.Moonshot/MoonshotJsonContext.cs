using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Moonshot;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MoonshotResponse))]
[JsonSerializable(typeof(MoonshotChoice))]
[JsonSerializable(typeof(MoonshotMessage))]
[JsonSerializable(typeof(MoonshotUsage))]
[JsonSerializable(typeof(MoonshotStreamChunk))]
[JsonSerializable(typeof(MoonshotStreamChoice))]
[JsonSerializable(typeof(MoonshotStreamDelta))]
[JsonSerializable(typeof(MoonshotChatRequest))]
[JsonSerializable(typeof(MoonshotRequestMessage))]
[JsonSerializable(typeof(MoonshotResponseFormat))]
internal sealed partial class MoonshotJsonContext : JsonSerializerContext
{
}
