using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Infrastructure.Serialization;

internal class EnumJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        // Handle nullable enums
        if (typeToConvert.IsGenericType && 
            typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>) &&
            typeToConvert.GetGenericArguments()[0].IsEnum)
        {
            return true;
        }

        // Handle non-nullable enums
        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle nullable enums
        if (typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            Type enumType = typeToConvert.GetGenericArguments()[0];
            Type converterType = typeof(NullableEnumConverter<>).MakeGenericType(enumType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        // Handle non-nullable enums
        Type nonNullableConverterType = typeof(EnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(nonNullableConverterType)!;
    }

    private class NullableEnumConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum
    {
        public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            // Obs³uga dla wartoœci prostych (string)
            if (reader.TokenType == JsonTokenType.String)
            {
                string enumValue = reader.GetString()!;
                if (Enum.TryParse<TEnum>(enumValue, true, out var result))
                {
                    return result;
                }
                return null;
            }

            // Obs³uga dla formatu obiektu {"HasValue":true,"Value":"Educational"}
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                string? enumValue = null;
                bool hasValue = false;

                // Odczytaj w³aœciwoœci obiektu
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propertyName = reader.GetString()!;
                        reader.Read();

                        if (propertyName.Equals("Value", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.String)
                        {
                            enumValue = reader.GetString();
                        }
                        else if (propertyName.Equals("HasValue", StringComparison.OrdinalIgnoreCase) && reader.TokenType == JsonTokenType.True)
                        {
                            hasValue = true;
                        }
                    }
                }

                if (hasValue && enumValue != null)
                {
                    if (Enum.TryParse<TEnum>(enumValue, true, out var result))
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToString());
        }
    }

    private class EnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Obs³uga dla wartoœci prostych (string)
            if (reader.TokenType == JsonTokenType.String)
            {
                string enumValue = reader.GetString()!;
                if (Enum.TryParse<TEnum>(enumValue, true, out var result))
                {
                    return result;
                }
                
                // Jeœli nie mo¿na sparsowaæ, rzuæ wyj¹tek z u¿ytecznymi informacjami
                var validValues = string.Join(", ", Enum.GetNames<TEnum>());
                throw new JsonException($"Invalid enum value '{enumValue}' for type '{typeof(TEnum).Name}'. Valid values are: {validValues}");
            }

            // Obs³uga dla liczb (w przypadku gdy enum jest serializowany jako liczba)
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out var intValue))
                {
                    if (Enum.IsDefined(typeof(TEnum), intValue))
                    {
                        return (TEnum)(object)intValue;
                    }
                }
            }

            throw new JsonException($"Unable to convert token type '{reader.TokenType}' to enum '{typeof(TEnum).Name}'");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}