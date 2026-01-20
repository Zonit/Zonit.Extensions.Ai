using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Resilient JSON response parser that handles various AI response formats.
/// Supports String, Int, Bool, List, and complex object types.
/// Handles common issues like markdown code blocks, partial JSON, and type mismatches.
/// </summary>
public static partial class JsonResponseParser
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Parses AI response to the expected type with resilient error handling.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="response">Raw AI response string.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>Parsed value of type T.</returns>
    /// <exception cref="JsonException">When parsing fails after all recovery attempts.</exception>
    [RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
    public static T Parse<T>(string response, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(response))
            throw new JsonException("Response is empty or whitespace");

        var targetType = typeof(T);
        var opts = options ?? DefaultOptions;

        // Handle primitive types directly
        if (targetType == typeof(string))
            return (T)(object)ExtractTextContent(response);

        if (targetType == typeof(int))
            return (T)(object)ParseInt(response);

        if (targetType == typeof(long))
            return (T)(object)ParseLong(response);

        if (targetType == typeof(double))
            return (T)(object)ParseDouble(response);

        if (targetType == typeof(decimal))
            return (T)(object)ParseDecimal(response);

        if (targetType == typeof(bool))
            return (T)(object)ParseBool(response);

        // For complex types, extract JSON and parse
        var json = ExtractJson(response);

        try
        {
            return JsonSerializer.Deserialize<T>(json, opts)
                ?? throw new JsonException($"Deserialization returned null for type {targetType.Name}");
        }
        catch (JsonException ex)
        {
            // Try to recover with additional cleaning
            var cleanedJson = CleanJson(json);
            try
            {
                return JsonSerializer.Deserialize<T>(cleanedJson, opts)
                    ?? throw new JsonException($"Deserialization returned null for type {targetType.Name}");
            }
            catch
            {
                throw new JsonException($"Failed to parse response as {targetType.Name}: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Tries to parse AI response to the expected type.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="response">Raw AI response string.</param>
    /// <param name="result">Parsed value if successful.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    [RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
    public static bool TryParse<T>(string response, [NotNullWhen(true)] out T? result, JsonSerializerOptions? options = null)
    {
        try
        {
            result = Parse<T>(response, options);
            return result is not null;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Extracts JSON from a response that might contain markdown or other formatting.
    /// </summary>
    public static string ExtractJson(string response)
    {
        var trimmed = response.Trim();

        // Remove markdown code blocks
        trimmed = RemoveMarkdownCodeBlocks(trimmed);

        // If it looks like JSON, return as-is
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return trimmed;

        // Try to find JSON object or array in the response
        var jsonMatch = JsonPattern().Match(trimmed);
        if (jsonMatch.Success)
            return jsonMatch.Value;

        // Last resort: wrap string in quotes if not JSON
        return $"\"{EscapeJsonString(trimmed)}\"";
    }

    /// <summary>
    /// Extracts plain text content, removing any JSON wrapper.
    /// </summary>
    public static string ExtractTextContent(string response)
    {
        var trimmed = response.Trim();

        // Remove markdown code blocks
        trimmed = RemoveMarkdownCodeBlocks(trimmed);

        // If it's a JSON string literal, extract the content
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            try
            {
                return JsonSerializer.Deserialize<string>(trimmed) ?? trimmed;
            }
            catch
            {
                return trimmed.Trim('"');
            }
        }

        // If it's a JSON object with common text fields, extract them
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                // Try common text field names
                string[] textFields = ["text", "content", "message", "result", "response", "answer", "output"];
                foreach (var field in textFields)
                {
                    if (root.TryGetProperty(field, out var prop) && prop.ValueKind == JsonValueKind.String)
                        return prop.GetString() ?? "";
                }
            }
            catch
            {
                // Not valid JSON, return as-is
            }
        }

        return trimmed;
    }

    private static string RemoveMarkdownCodeBlocks(string text)
    {
        // Remove ```json ... ``` or ``` ... ```
        var pattern = MarkdownCodeBlockPattern();
        var match = pattern.Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : text;
    }

    private static string CleanJson(string json)
    {
        // Remove trailing commas before } or ]
        json = TrailingCommaPattern().Replace(json, "$1");

        // Fix common escape issues
        json = json.Replace("\\'", "'");

        return json;
    }

    private static int ParseInt(string response)
    {
        var text = ExtractTextContent(response);

        // Try direct parse
        if (int.TryParse(text, out var result))
            return result;

        // Extract first number from text
        var match = IntegerPattern().Match(text);
        if (match.Success && int.TryParse(match.Value, out result))
            return result;

        throw new JsonException($"Cannot parse '{text}' as integer");
    }

    private static long ParseLong(string response)
    {
        var text = ExtractTextContent(response);

        if (long.TryParse(text, out var result))
            return result;

        var match = IntegerPattern().Match(text);
        if (match.Success && long.TryParse(match.Value, out result))
            return result;

        throw new JsonException($"Cannot parse '{text}' as long");
    }

    private static double ParseDouble(string response)
    {
        var text = ExtractTextContent(response);

        if (double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;

        var match = NumberPattern().Match(text);
        if (match.Success && double.TryParse(match.Value, System.Globalization.CultureInfo.InvariantCulture, out result))
            return result;

        throw new JsonException($"Cannot parse '{text}' as double");
    }

    private static decimal ParseDecimal(string response)
    {
        var text = ExtractTextContent(response);

        if (decimal.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;

        var match = NumberPattern().Match(text);
        if (match.Success && decimal.TryParse(match.Value, System.Globalization.CultureInfo.InvariantCulture, out result))
            return result;

        throw new JsonException($"Cannot parse '{text}' as decimal");
    }

    private static bool ParseBool(string response)
    {
        var text = ExtractTextContent(response).ToLowerInvariant();

        return text switch
        {
            "true" or "yes" or "1" or "tak" or "prawda" => true,
            "false" or "no" or "0" or "nie" or "fałsz" => false,
            _ => throw new JsonException($"Cannot parse '{text}' as boolean")
        };
    }

    private static string EscapeJsonString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownCodeBlockPattern();

    [GeneratedRegex(@"[\[{][\s\S]*[\]}]")]
    private static partial Regex JsonPattern();

    [GeneratedRegex(@",\s*([}\]])")]
    private static partial Regex TrailingCommaPattern();

    [GeneratedRegex(@"-?\d+")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"-?\d+\.?\d*")]
    private static partial Regex NumberPattern();
}
