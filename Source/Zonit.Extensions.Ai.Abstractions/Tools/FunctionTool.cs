using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Function calling tool for custom functions.
/// </summary>
public class FunctionTool : IToolBase
{
    /// <summary>
    /// Function name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Function description for the AI.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// JSON Schema for function parameters.
    /// </summary>
    public required JsonElement Parameters { get; init; }

    /// <summary>
    /// Whether to use strict schema validation.
    /// </summary>
    public bool Strict { get; init; } = true;

    /// <summary>
    /// Creates a FunctionTool from a type and parameters object.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and deserialization might require types that cannot be statically analyzed and might need runtime code generation.")]
    public static FunctionTool Create(string name, string description, object parameters)
    {
        var json = JsonSerializer.Serialize(parameters);
        return new FunctionTool
        {
            Name = name,
            Description = description,
            Parameters = JsonSerializer.Deserialize<JsonElement>(json)
        };
    }
}
