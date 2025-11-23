using System.Text.Json;
using System.Text.Json.Serialization;
using Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

namespace Zonit.Extensions.Ai.Infrastructure.Serialization;

/// <summary>
/// JSON serialization context for AOT compatibility with Native AOT compilation.
/// This provides source-generated JSON serializers for all types used in AI operations.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(OpenAiResponsesApiResponse))]
[JsonSerializable(typeof(ResponseOutput))]
[JsonSerializable(typeof(ResponseContent))]
[JsonSerializable(typeof(ResponseReasoning))]
[JsonSerializable(typeof(ResponseText))]
[JsonSerializable(typeof(ResponseTextFormat))]
[JsonSerializable(typeof(ResponseUsageInfo))]
[JsonSerializable(typeof(ResponseInputTokensDetails))]
[JsonSerializable(typeof(ResponseOutputTokensDetails))]
[JsonSerializable(typeof(OpenAiResponse))]
[JsonSerializable(typeof(Choice))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(UsageInfo))]
[JsonSerializable(typeof(CompletionTokensDetails))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(JsonElement))]
internal partial class AiJsonSerializerContext : JsonSerializerContext
{
}
