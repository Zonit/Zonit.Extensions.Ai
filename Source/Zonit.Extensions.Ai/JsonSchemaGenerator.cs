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

    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification =
            "GenerateSchema recurses into Type.GetElementType() (for arrays), " +
            "Type.GetGenericArguments() (for List<T>/IEnumerable<T>) and PropertyInfo.PropertyType. " +
            "The trimmer cannot statically prove that those types carry the same DAM(PublicProperties) " +
            "constraint as the parameter, but the public entry points (Generate<T>(), Generate(Type)) " +
            "are themselves marked [RequiresUnreferencedCode]/[RequiresDynamicCode], so callers are " +
            "already required to ensure the whole reachable type graph is preserved. The recursive " +
            "calls below merely propagate that contract.")]
    [UnconditionalSuppressMessage("Trimming", "IL2062",
        Justification = "Same as above — recursion stays inside the [RUC]-annotated public entry points.")]
    private static Dictionary<string, object> GenerateSchema([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, bool isNullable = false)
    {
        // Handle Nullable<T> - extract underlying type
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return GenerateSchema(underlyingType, isNullable: true);
        }

        // Handle Guid as string (UUID format)
        if (type == typeof(Guid))
        {
            var schema = new Dictionary<string, object> 
            { 
                ["type"] = isNullable ? new object[] { "string", "null" } : "string",
                ["description"] = "A globally unique identifier (UUID) in format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
            };
            return schema;
        }

        // Handle DateTime as ISO 8601 string
        if (type == typeof(DateTime))
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "string", "null" } : "string",
                ["description"] = "Date and time in ISO 8601 format: YYYY-MM-DDTHH:mm:ss"
            };
            return schema;
        }

        // Handle DateTimeOffset as ISO 8601 string
        if (type == typeof(DateTimeOffset))
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "string", "null" } : "string",
                ["description"] = "Date and time with offset in ISO 8601 format: YYYY-MM-DDTHH:mm:ss±HH:mm"
            };
            return schema;
        }

        // Handle DateOnly as ISO 8601 date string
        if (type == typeof(DateOnly))
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "string", "null" } : "string",
                ["description"] = "Date in ISO 8601 format: YYYY-MM-DD"
            };
            return schema;
        }

        // Handle TimeOnly as time string
        if (type == typeof(TimeOnly))
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "string", "null" } : "string",
                ["description"] = "Time in format: HH:mm:ss"
            };
            return schema;
        }

        // Handle TimeSpan as duration string
        if (type == typeof(TimeSpan))
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "string", "null" } : "string",
                ["description"] = "Duration/time span in format: d.hh:mm:ss or hh:mm:ss"
            };
            return schema;
        }

        // Handle Uri as string
        if (type == typeof(Uri))
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "string", "null" } : "string",
                ["description"] = "A valid URI/URL string"
            };
            return schema;
        }

        if (type == typeof(string))
        {
            if (isNullable)
                return new Dictionary<string, object> { ["type"] = new[] { "string", "null" } };
            return new Dictionary<string, object> { ["type"] = "string" };
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || 
            type == typeof(byte) || type == typeof(sbyte) || 
            type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort))
        {
            if (isNullable)
                return new Dictionary<string, object> { ["type"] = new[] { "integer", "null" } };
            return new Dictionary<string, object> { ["type"] = "integer" };
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            if (isNullable)
                return new Dictionary<string, object> { ["type"] = new[] { "number", "null" } };
            return new Dictionary<string, object> { ["type"] = "number" };
        }

        if (type == typeof(bool))
        {
            if (isNullable)
                return new Dictionary<string, object> { ["type"] = new[] { "boolean", "null" } };
            return new Dictionary<string, object> { ["type"] = "boolean" };
        }

        // Handle IEnumerable types (arrays, lists, etc.) but not string
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "array", "null" } : "array",
                ["items"] = GenerateSchema(elementType)
            };
            return schema;
        }

        if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>) ||
                                    type.GetGenericTypeDefinition() == typeof(IList<>) ||
                                    type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                                    type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
        {
            var elementType = type.GetGenericArguments()[0];
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "array", "null" } : "array",
                ["items"] = GenerateSchema(elementType)
            };
            return schema;
        }

        if (type.IsEnum)
        {
            // Get enum values as lowercase strings to match API expectations
            var enumValues = Enum.GetNames(type).Select(n => n.ToLowerInvariant()).ToArray();
            var schema = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new object[] { "string", "null" } : "string",
                ["enum"] = isNullable 
                    ? enumValues.Cast<object>().Append(null!).ToArray()
                    : enumValues
            };
            return schema;
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
