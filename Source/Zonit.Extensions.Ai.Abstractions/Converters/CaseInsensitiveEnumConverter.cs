using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Converters;

/// <summary>
/// Case-insensitive enum converter that handles all naming conventions:
/// PascalCase, camelCase, snake_case, lowercase, uppercase, etc.
/// </summary>
public class CaseInsensitiveEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum || 
               (Nullable.GetUnderlyingType(typeToConvert)?.IsEnum == true);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2071",
        Justification =
            "EnumConverter<TEnum> and NullableEnumConverter<TEnum> only require the parameterless " +
            "constructor of TEnum, which always exists for value types (enum is a struct). " +
            "Activator.CreateInstance therefore never needs metadata that the trimmer would remove.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification =
            "MakeGenericType is called only with concrete enum types passed in by the JSON serializer. " +
            "The set of enums declared by the consumer is rooted by their use as property types on " +
            "response/options POCOs, which are themselves preserved via DAM(PublicProperties). " +
            "Native AOT will therefore have the closed generic instantiation available at runtime.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        bool isNullable = Nullable.GetUnderlyingType(typeToConvert) != null;

        Type converterType = isNullable
            ? typeof(NullableEnumConverter<>).MakeGenericType(enumType)
            : typeof(EnumConverter<>).MakeGenericType(enumType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private class EnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                // Handle integer values
                if (reader.TryGetInt32(out int intValue))
                {
                    return (TEnum)Enum.ToObject(typeof(TEnum), intValue);
                }
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? stringValue = reader.GetString();
                if (string.IsNullOrEmpty(stringValue))
                {
                    return default;
                }

                // Try exact match first
                if (Enum.TryParse<TEnum>(stringValue, ignoreCase: true, out TEnum result))
                {
                    return result;
                }

                // Try normalized (remove underscores, hyphens)
                string normalized = stringValue.Replace("_", "").Replace("-", "");
                if (Enum.TryParse<TEnum>(normalized, ignoreCase: true, out result))
                {
                    return result;
                }

                throw new JsonException($"Unable to convert \"{stringValue}\" to enum {typeof(TEnum).Name}");
            }

            throw new JsonException($"Unexpected token type {reader.TokenType} when parsing enum {typeof(TEnum).Name}");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    private class NullableEnumConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum
    {
        private readonly EnumConverter<TEnum> _innerConverter = new();

        public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            return _innerConverter.Read(ref reader, typeof(TEnum), options);
        }

        public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                _innerConverter.Write(writer, value.Value, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
