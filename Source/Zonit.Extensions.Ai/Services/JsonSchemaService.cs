using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Services;

/// <summary>
/// Generator schematów JSON dla typów .NET zgodny z OpenAI JSON Schema.
/// </summary>
public class JsonSchemaService
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _wrapInResult;
    private readonly bool _includeDescriptions;

    /// <summary>
    /// Inicjalizuje nową instancję generatora schematów JSON.
    /// </summary>
    /// <param name="wrapInResult">Czy owinąć schemat w obiekt "result"</param>
    /// <param name="includeDescriptions">Czy dołączać opisy z atrybutów Description</param>
    /// <param name="jsonOptions">Opcje serializacji JSON</param>
    public JsonSchemaService(bool wrapInResult = true, bool includeDescriptions = true, JsonSerializerOptions? jsonOptions = null)
    {
        _wrapInResult = wrapInResult;
        _includeDescriptions = includeDescriptions;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Generuje schemat JSON dla podanego typu generycznego.
    /// </summary>
    /// <typeparam name="T">Typ dla którego generowany jest schemat</typeparam>
    /// <returns>Schemat JSON jako string</returns>
    public string GenerateSchema<T>()
    {
        return GenerateSchema(typeof(T));
    }

    /// <summary>
    /// Generuje schemat JSON dla podanego typu.
    /// </summary>
    /// <param name="type">Typ dla którego generowany jest schemat</param>
    /// <returns>Schemat JSON jako string</returns>
    public string GenerateSchema(Type type)
    {
        var schema = GenerateSchemaForType(type);

        if (_wrapInResult)
        {
            schema = WrapWithResult(schema);
        }

        return JsonSerializer.Serialize(schema, _jsonOptions);
    }

    /// <summary>
    /// Generuje obiekt schematu JSON dla danego typu.
    /// </summary>
    private Dictionary<string, object> GenerateSchemaForType(Type type)
    {
        // Obsługa nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            return GenerateNullableSchema(underlyingType);
        }

        // Obsługa enumów
        if (type.IsEnum)
        {
            return GenerateEnumSchema(type);
        }

        // Obsługa typów prostych
        if (IsSimpleType(type))
        {
            return GenerateSimpleTypeSchema(type);
        }

        // Obsługa kolekcji
        if (IsCollectionType(type, out var elementType))
        {
            return GenerateArraySchema(elementType);
        }

        // Obsługa słowników
        if (IsDictionaryType(type, out var keyType, out var valueType))
        {
            return GenerateDictionarySchema(keyType!, valueType!);
        }

        // Obsługa typów złożonych (obiektów)
        return GenerateObjectSchema(type);
    }

    /// <summary>
    /// Generuje schemat dla typu nullable.
    /// </summary>
    private Dictionary<string, object> GenerateNullableSchema(Type underlyingType)
    {
        var schema = GenerateSchemaForType(underlyingType);

        // Dodaj "null" do możliwych typów
        if (schema.TryGetValue("type", out var typeValue))
        {
            schema["type"] = new List<object> { typeValue, "null" };
        }

        return schema;
    }

    /// <summary>
    /// Generuje schemat dla typu enum.
    /// </summary>
    /// <summary>
    /// Generuje schemat dla typu enum.
    /// </summary>
    private Dictionary<string, object> GenerateEnumSchema(Type enumType)
    {
        var enumSchemas = new List<Dictionary<string, object>>();
        var simpleEnumValues = new List<string>();
        bool hasDescriptions = false;

        foreach (var value in Enum.GetValues(enumType))
        {
            var name = value.ToString()!;
            var field = enumType.GetField(name);

            // Pobierz nazwę dla JSON
            var jsonPropertyAttribute = field?.GetCustomAttribute<JsonPropertyNameAttribute>();
            var jsonName = jsonPropertyAttribute != null
                ? jsonPropertyAttribute.Name
                : (_jsonOptions.PropertyNamingPolicy?.ConvertName(name) ?? name);

            simpleEnumValues.Add(jsonName);

            // Sprawdź czy ma opis
            if (_includeDescriptions)
            {
                var descriptionAttribute = field?.GetCustomAttribute<DescriptionAttribute>();
                if (descriptionAttribute != null)
                {
                    hasDescriptions = true;
                    enumSchemas.Add(new Dictionary<string, object>
                    {
                        ["const"] = jsonName,
                        ["description"] = descriptionAttribute.Description
                    });
                }
                else
                {
                    enumSchemas.Add(new Dictionary<string, object>
                    {
                        ["const"] = jsonName
                    });
                }
            }
        }

        Dictionary<string, object> schema;

        // Jeśli są opisy, użyj oneOf, w przeciwnym razie standardowy enum
        if (hasDescriptions && _includeDescriptions)
        {
            schema = new Dictionary<string, object>
            {
                ["oneOf"] = enumSchemas
            };
        }
        else
        {
            schema = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = simpleEnumValues
            };
        }

        // Dodaj ogólny opis typu enum
        if (_includeDescriptions)
        {
            var typeDescription = enumType.GetCustomAttribute<DescriptionAttribute>();
            if (typeDescription != null)
            {
                schema["description"] = typeDescription.Description;
            }
        }

        return schema;
    }

    /// <summary>
    /// Generuje schemat dla typów prostych.
    /// </summary>
    private Dictionary<string, object> GenerateSimpleTypeSchema(Type type)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = GetJsonType(type)
        };

        // Dodaj dodatkowe informacje dla specyficznych typów
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            schema["format"] = "date-time";
        }
        else if (type == typeof(DateOnly))
        {
            schema["format"] = "date";
        }
        else if (type == typeof(TimeOnly))
        {
            schema["format"] = "time";
        }
        else if (type == typeof(Guid))
        {
            schema["format"] = "uuid";
        }
        else if (type == typeof(TimeSpan))
        {
            schema["format"] = "duration";
        }
        else if (type == typeof(Uri))
        {
            schema["format"] = "uri";
        }

        return schema;
    }

    /// <summary>
    /// Generuje schemat dla tablic i list.
    /// </summary>
    private Dictionary<string, object> GenerateArraySchema(Type elementType)
    {
        return new Dictionary<string, object>
        {
            ["type"] = "array",
            ["items"] = GenerateSchemaForType(elementType)
        };
    }

    /// <summary>
    /// Generuje schemat dla słowników.
    /// </summary>
    private Dictionary<string, object> GenerateDictionarySchema(Type keyType, Type valueType)
    {
        // Słowniki są reprezentowane jako obiekty z dodatkowymi właściwościami
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["additionalProperties"] = GenerateSchemaForType(valueType)
        };

        if (_includeDescriptions)
        {
            schema["description"] = $"Dictionary with {keyType.Name} keys and {valueType.Name} values";
        }

        return schema;
    }

    /// <summary>
    /// Generuje schemat dla typów złożonych (obiektów).
    /// </summary>
    private Dictionary<string, object> GenerateObjectSchema(Type type)
    {
        var properties = new Dictionary<string, object>();
        var requiredProperties = new List<string>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Pomiń właściwości tylko do odczytu lub z JsonIgnore
            if (!property.CanWrite || property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                continue;

            var propertyName = GetPropertyName(property);
            var propertySchema = GenerateSchemaForType(property.PropertyType);

            // Dodaj opis właściwości jeśli istnieje
            if (_includeDescriptions)
            {
                var descriptionAttribute = property.GetCustomAttribute<DescriptionAttribute>();
                if (descriptionAttribute != null)
                {
                    propertySchema["description"] = descriptionAttribute.Description;
                }
            }

            properties[propertyName] = propertySchema;

            // Sprawdź czy właściwość jest wymagana
            if (!IsNullableProperty(property))
            {
                requiredProperties.Add(propertyName);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };

        if (requiredProperties.Any())
        {
            schema["required"] = requiredProperties;
        }

        // Dodaj opis typu jeśli ma atrybut Description
        if (_includeDescriptions)
        {
            var descriptionAttribute = type.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttribute != null)
            {
                schema["description"] = descriptionAttribute.Description;
            }
        }

        return schema;
    }

    /// <summary>
    /// Opakowuje schemat w obiekt "result".
    /// </summary>
    private Dictionary<string, object> WrapWithResult(Dictionary<string, object> schema)
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
    /// Sprawdza czy typ jest typem prostym.
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(Uri);
    }

    /// <summary>
    /// Sprawdza czy typ jest kolekcją i zwraca typ elementu.
    /// </summary>
    private static bool IsCollectionType(Type type, out Type elementType)
    {
        elementType = null!;

        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        // Sprawdź czy to string - string implementuje IEnumerable<char> ale nie chcemy go traktować jako kolekcji
        if (type == typeof(string))
        {
            return false;
        }

        var enumerableInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface != null)
        {
            elementType = enumerableInterface.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sprawdza czy typ jest słownikiem i zwraca typy klucza i wartości.
    /// </summary>
    private static bool IsDictionaryType(Type type, out Type? keyType, out Type? valueType)
    {
        keyType = null;
        valueType = null;

        var dictionaryInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (dictionaryInterface != null)
        {
            var genericArgs = dictionaryInterface.GetGenericArguments();
            keyType = genericArgs[0];
            valueType = genericArgs[1];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Pobiera nazwę właściwości z uwzględnieniem atrybutów i polityki nazewnictwa.
    /// </summary>
    private string GetPropertyName(PropertyInfo property)
    {
        var jsonPropertyAttribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyAttribute != null)
        {
            return jsonPropertyAttribute.Name;
        }

        return _jsonOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
    }

    /// <summary>
    /// Sprawdza czy właściwość może przyjmować wartość null.
    /// </summary>
    private static bool IsNullableProperty(PropertyInfo property)
    {
        // Sprawdź nullable reference types (C# 8.0+)
        var nullableContext = property.DeclaringType?.GetCustomAttribute<System.Runtime.CompilerServices.NullableContextAttribute>();
        if (nullableContext != null && nullableContext.Flag == 1)
        {
            var nullable = property.GetCustomAttribute<System.Runtime.CompilerServices.NullableAttribute>();
            if (nullable != null && nullable.NullableFlags.Length > 0)
            {
                return nullable.NullableFlags[0] == 2;
            }
        }

        // Sprawdź nullable value types
        return Nullable.GetUnderlyingType(property.PropertyType) != null;
    }

    /// <summary>
    /// Mapuje typ .NET na typ JSON Schema.
    /// </summary>
    private static string GetJsonType(Type type) => type switch
    {
        _ when type == typeof(string) => "string",
        _ when type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) => "integer",
        _ when type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte) => "integer",
        _ when type == typeof(bool) => "boolean",
        _ when type == typeof(float) || type == typeof(double) || type == typeof(decimal) => "number",
        _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly) => "string",
        _ when type == typeof(Guid) => "string",
        _ when type == typeof(TimeSpan) => "string",
        _ when type == typeof(Uri) => "string",
        _ => "object"
    };
}