using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai.OpenAi.Tools;

/// <summary>
/// OpenAI-flavoured function tool. Inherits the JSON-schema-based contract
/// from the shared <see cref="Zonit.Extensions.Ai.FunctionTool"/> and adds
/// the <see cref="IOpenAiTool"/> marker so it can be assigned to
/// <see cref="OpenAiBase.Tools"/> without unlocking foreign-provider tools.
/// </summary>
public class FunctionTool : Zonit.Extensions.Ai.FunctionTool, IOpenAiTool
{
    /// <inheritdoc cref="Zonit.Extensions.Ai.FunctionTool.Create" />
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public static new FunctionTool Create(string name, string description, object parameters)
    {
        var json = JsonSerializer.Serialize(parameters);
        return new FunctionTool
        {
            Name = name,
            Description = description,
            Parameters = JsonSerializer.Deserialize<JsonElement>(json),
        };
    }
}
