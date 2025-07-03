using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Infrastructure.Serialization;

internal class NullableEnumJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        return typeToConvert.GetGenericTypeDefinition() == typeof(Nullable<>) &&
               typeToConvert.GetGenericArguments()[0].IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type enumType = typeToConvert.GetGenericArguments()[0];
        Type converterType = typeof(NullableEnumConverter<>).MakeGenericType(enumType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private class NullableEnumConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum
    {
        public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            // Obsługa dla wartości prostych (string)
            if (reader.TokenType == JsonTokenType.String)
            {
                string enumValue = reader.GetString()!;
                return (TEnum)Enum.Parse(typeof(TEnum), enumValue);
            }

            // Obsługa dla formatu obiektu {"HasValue":true,"Value":"Educational"}
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                string? enumValue = null;
                bool hasValue = false;

                // Odczytaj właściwości obiektu
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
                    return (TEnum)Enum.Parse(typeof(TEnum), enumValue);
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
}
