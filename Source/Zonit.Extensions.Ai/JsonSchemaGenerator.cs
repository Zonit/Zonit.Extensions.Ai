using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Generates JSON Schema from .NET types for structured outputs.
/// </summary>
public static class JsonSchemaGenerator
{
    /// <summary>
    /// Generates JSON Schema for a type.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and schema generation might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and schema generation might require types that cannot be statically analyzed and might need runtime code generation.")]
    public static JsonElement Generate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>() => Generate(typeof(T));

    /// <summary>
    /// Generates JSON Schema for a type.
    /// </summary>
    [RequiresUnreferencedCode("JSON serialization and schema generation might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization and schema generation might require types that cannot be statically analyzed and might need runtime code generation.")]
    public static JsonElement Generate([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        var schema = GenerateSchema(type);
        var json = JsonSerializer.Serialize(schema);
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Gets type description from [Description] attribute.
    /// </summary>
    public static string? GetDescription<T>() => GetDescription(typeof(T));

    /// <summary>
    /// Gets type description from [Description] attribute.
    /// </summary>
    public static string? GetDescription(Type type)
        => type.GetCustomAttribute<DescriptionAttribute>()?.Description;

    private static Dictionary<string, object> GenerateSchema([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        if (type == typeof(string))
            return new Dictionary<string, object> { ["type"] = "string" };

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return new Dictionary<string, object> { ["type"] = "integer" };

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return new Dictionary<string, object> { ["type"] = "number" };

        if (type == typeof(bool))
            return new Dictionary<string, object> { ["type"] = "boolean" };

        if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
        {
            var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = GenerateSchema(elementType)
            };
        }

        if (type.IsEnum)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(type)
            };
        }

        // Object type
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propSchema = GenerateSchema(prop.PropertyType);

            var description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (description != null)
                propSchema["description"] = description;

            var propName = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
            properties[propName] = propSchema;

            // OpenAI Structured Outputs with strict:true requires ALL fields to be in 'required'
            // For nullable types, use anyOf with null type instead of omitting from required
            required.Add(propName);
        }

        var result = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
            ["required"] = required  // Always include required with ALL property names for strict mode
        };

        var typeDescription = GetDescription(type);
        if (typeDescription != null)
            result["description"] = typeDescription;

        return result;
    }
}
