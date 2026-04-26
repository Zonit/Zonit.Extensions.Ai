using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.X;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(XResponse))]
[JsonSerializable(typeof(XOutput))]
[JsonSerializable(typeof(XOutputContent))]
[JsonSerializable(typeof(XUsage))]
[JsonSerializable(typeof(XInputTokensDetails))]
[JsonSerializable(typeof(XOutputTokensDetails))]
[JsonSerializable(typeof(XPromptTokensDetails))]
[JsonSerializable(typeof(XTokenDetails))]
[JsonSerializable(typeof(StreamChunk))]
[JsonSerializable(typeof(XImageResponse))]
[JsonSerializable(typeof(XImageData))]
[JsonSerializable(typeof(XVideoTaskResponse))]
[JsonSerializable(typeof(XVideoStatusResponse))]
[JsonSerializable(typeof(XVideoData))]
[JsonSerializable(typeof(XVideoOutput))]
[JsonSerializable(typeof(XResponsesRequest))]
[JsonSerializable(typeof(XInputItem))]
[JsonSerializable(typeof(XContentPart))]
[JsonSerializable(typeof(XResponseFormat))]
[JsonSerializable(typeof(XJsonSchemaSpec))]
[JsonSerializable(typeof(XTool))]
[JsonSerializable(typeof(XImageRequest))]
[JsonSerializable(typeof(XVideoRequest))]
[JsonSerializable(typeof(XVideoUrlRef))]
internal sealed partial class XJsonContext : JsonSerializerContext
{
}
