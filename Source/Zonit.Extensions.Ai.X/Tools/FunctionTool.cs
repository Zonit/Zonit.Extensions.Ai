using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai.X.Tools;

/// <summary>
/// xAI-flavoured function tool. Same JSON-schema contract as the shared
/// <see cref="Zonit.Extensions.Ai.FunctionTool"/>; the extra
/// <see cref="IXTool"/> marker is what lets it be assigned to
/// <see cref="XBase.Tools"/>.
/// </summary>
public class FunctionTool : Zonit.Extensions.Ai.FunctionTool, IXTool
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
