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
        if (type == typeof(string) || type.IsPrimitive || type == typeof(bool) || type == typeof(decimal))
        {
            return new Dictionary<string, object> { ["type"] = GetJsonType(type) };
        }

        if (type.IsEnum)
        {
            var enumValues = Enum.GetNames(type).ToList();
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = enumValues
            };
        }

        // Obsługa nullable enum
        if (Nullable.GetUnderlyingType(type)?.IsEnum == true)
        {
            var underlyingEnumType = Nullable.GetUnderlyingType(type)!;
            var enumValues = Enum.GetNames(underlyingEnumType).ToList();
            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = enumValues
            };
        }

        if (type.IsArray || IsGenericList(type))
        {
            var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = GenerateJsonSchemaForType(elementType),
                ["additionalProperties"] = false
            };
        }

        var properties = new Dictionary<string, object>();
        var requiredProperties = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var propSchema = GenerateJsonSchemaForType(prop.PropertyType);

            // Dodaj description z atrybutu, jeśli istnieje
            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttr != null && !string.IsNullOrEmpty(descriptionAttr.Description))
            {
                propSchema["description"] = descriptionAttr.Description;
            }

            properties[prop.Name] = propSchema;

            // W strict mode OpenAI wymaga wszystkich właściwości w required
            requiredProperties.Add(prop.Name);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
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
