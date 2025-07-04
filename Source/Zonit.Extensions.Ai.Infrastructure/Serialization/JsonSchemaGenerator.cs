using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Zonit.Extensions.Ai.Infrastructure.Serialization;

internal static partial class JsonSchemaGenerator
{
    /// <summary>
    /// Pobierz opis całego schematu na podstawie atrybutu Description klasy.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string? GetSchemaDescription<T>()
    {
        var type = typeof(T);

        // Sprawdź czy klasa ma atrybut Description
        var descriptionAttr = type.GetCustomAttribute<DescriptionAttribute>();
        if (descriptionAttr != null && !string.IsNullOrEmpty(descriptionAttr.Description))
        {
            return descriptionAttr.Description;
        }

        // Alternatywnie możesz utworzyć domyślny opis na podstawie nazwy typu
        return $"Response format for {type.Name}";
    }

    /// <summary>
    /// Wygeneruj schemat JSON dla danego typu.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string GenerateJsonSchema<T>()
    {
        Type type = typeof(T);
        var schema = GenerateJsonSchemaForType(type);

        return JsonSerializer.Serialize(WrapWithResult(schema));
    }

    /// <summary>
    /// Wygeneruj schemat JSON dla danego typu z obsługą atrybutów Description.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static Dictionary<string, object> GenerateJsonSchemaForType(Type type)
    {
        // Sprawdź czy typ jest nullable
        var underlyingType = Nullable.GetUnderlyingType(type);
        bool isNullable = underlyingType != null;
        var actualType = underlyingType ?? type;

        if (actualType == typeof(string))
        {
            // String jest naturalnie nullable, ale sprawdzamy czy to string? czy string
            bool isExplicitlyNullable = type == typeof(string) || isNullable;
            return new Dictionary<string, object>
            {
                ["type"] = isExplicitlyNullable ? new[] { "string", "null" } : "string"
            };
        }

        if (actualType.IsPrimitive || actualType == typeof(bool) || actualType == typeof(decimal))
        {
            var jsonType = GetJsonType(actualType);
            return new Dictionary<string, object>
            {
                ["type"] = isNullable ? new[] { jsonType, "null" } : jsonType
            };
        }

        if (actualType.IsEnum)
        {
            var enumValues = Enum.GetNames(actualType).ToList();
            var schemaE = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new[] { "string", "null" } : "string",
                ["enum"] = enumValues
            };
            return schemaE;
        }

        if (actualType.IsArray || IsGenericList(actualType))
        {
            var elementType = actualType.IsArray ? actualType.GetElementType()! : actualType.GetGenericArguments()[0];
            var schemaA = new Dictionary<string, object>
            {
                ["type"] = isNullable ? new[] { "array", "null" } : "array",
                ["items"] = GenerateJsonSchemaForType(elementType),
                ["additionalProperties"] = false
            };
            return schemaA;
        }

        var properties = new Dictionary<string, object>();
        var requiredProperties = new List<string>();

        foreach (var prop in actualType.GetProperties())
        {
            var propSchema = GenerateJsonSchemaForType(prop.PropertyType);

            // Dodaj description z atrybutu, jeśli istnieje
            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttr != null && !string.IsNullOrEmpty(descriptionAttr.Description))
            {
                propSchema["description"] = descriptionAttr.Description;
            }

            properties[prop.Name] = propSchema;

            // OpenAI w strict mode wymaga wszystkich właściwości w required
            // niezależnie od tego czy są nullable czy nie
            requiredProperties.Add(prop.Name);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = isNullable ? new[] { "object", "null" } : "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };

        // W strict mode zawsze dodaj required z wszystkimi właściwościami
        if (requiredProperties.Count > 0)
        {
            schema["required"] = requiredProperties;
        }

        return schema;
    }

    /// <summary>
    /// Sprawdź czy właściwość jest nullable.
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    static bool IsPropertyNullable(PropertyInfo property)
    {
        // Sprawdź czy typ właściwości jest Nullable<T>
        if (Nullable.GetUnderlyingType(property.PropertyType) != null)
            return true;

        // Sprawdź czy typ właściwości to reference type (może być null)
        if (!property.PropertyType.IsValueType)
            return true;

        return false;
    }

    /// <summary>
    /// Obejmij schemat odpowiedzi zewnętrznej w obiekcie "result".
    /// </summary>
    /// <param name="schema"></param>
    /// <returns></returns>
    static Dictionary<string, object> WrapWithResult(Dictionary<string, object> schema)
    {
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object> { ["result"] = schema },
            ["required"] = new List<string> { "result" },
            ["additionalProperties"] = false
        };
    }

    /// <summary>
    /// Sprawdź, czy dany typ jest listą generyczną.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static bool IsGenericList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

    /// <summary>
    /// Pobierz typ JSON dla danego typu.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static string GetJsonType(Type type) => type switch
    {
        _ when type == typeof(string) => "string",
        _ when type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) => "integer",
        _ when type == typeof(bool) => "boolean",
        _ when type == typeof(float) || type == typeof(double) || type == typeof(decimal) => "number",
        _ when type.IsArray || IsGenericList(type) => "array",
        _ => "object"
    };

    [GeneratedRegex(@"\{\{\$(\w+)\}\}")]
    private static partial Regex VariablePlaceholderRegex();
}