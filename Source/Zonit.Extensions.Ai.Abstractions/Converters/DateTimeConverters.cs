using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zonit.Extensions.Ai.Converters;

/// <summary>
/// JSON converter for DateOnly type supporting ISO 8601 format (YYYY-MM-DD).
/// </summary>
public class DateOnlyConverter : JsonConverter<DateOnly>
{
    private static readonly string[] Formats = 
    [
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "dd-MM-yyyy",
        "dd/MM/yyyy",
        "MM-dd-yyyy",
        "MM/dd/yyyy"
    ];

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        var stringValue = reader.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return default;

        // Try standard ISO format first
        if (DateOnly.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;

        // Try multiple formats
        foreach (var format in Formats)
        {
            if (DateOnly.TryParseExact(stringValue, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;
        }

        throw new JsonException($"Unable to convert \"{stringValue}\" to DateOnly");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// JSON converter for nullable DateOnly type.
/// </summary>
public class NullableDateOnlyConverter : JsonConverter<DateOnly?>
{
    private readonly DateOnlyConverter _innerConverter = new();

    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        return _innerConverter.Read(ref reader, typeof(DateOnly), options);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            _innerConverter.Write(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// JSON converter for TimeOnly type supporting common time formats.
/// </summary>
public class TimeOnlyConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = 
    [
        "HH:mm:ss",
        "HH:mm:ss.fff",
        "HH:mm",
        "h:mm:ss tt",
        "h:mm tt"
    ];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        var stringValue = reader.GetString();
        if (string.IsNullOrEmpty(stringValue))
            return default;

        // Try standard formats first
        if (TimeOnly.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;

        // Try multiple formats
        foreach (var format in Formats)
        {
            if (TimeOnly.TryParseExact(stringValue, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return result;
        }

        throw new JsonException($"Unable to convert \"{stringValue}\" to TimeOnly");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// JSON converter for nullable TimeOnly type.
/// </summary>
public class NullableTimeOnlyConverter : JsonConverter<TimeOnly?>
{
    private readonly TimeOnlyConverter _innerConverter = new();

    public override TimeOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        return _innerConverter.Read(ref reader, typeof(TimeOnly), options);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            _innerConverter.Write(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// Factory that creates converters for DateOnly and TimeOnly types.
/// </summary>
public class DateTimeConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(DateOnly) ||
               typeToConvert == typeof(DateOnly?) ||
               typeToConvert == typeof(TimeOnly) ||
               typeToConvert == typeof(TimeOnly?);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(DateOnly))
            return new DateOnlyConverter();
        if (typeToConvert == typeof(DateOnly?))
            return new NullableDateOnlyConverter();
        if (typeToConvert == typeof(TimeOnly))
            return new TimeOnlyConverter();
        if (typeToConvert == typeof(TimeOnly?))
            return new NullableTimeOnlyConverter();

        throw new ArgumentException($"Unsupported type: {typeToConvert}");
    }
}
