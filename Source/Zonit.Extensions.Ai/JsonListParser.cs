using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Specialized parser for list/array responses from AI.
/// Handles various formats: JSON arrays, numbered lists, bullet points, comma-separated values.
/// </summary>
public static partial class JsonListParser
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Parses AI response as a list of items.
    /// Handles JSON arrays, numbered lists, bullet points, and comma-separated values.
    /// </summary>
    /// <typeparam name="T">Type of list items.</typeparam>
    /// <param name="response">Raw AI response string.</param>
    /// <param name="options">Optional JSON serializer options.</param>
    /// <returns>List of parsed items.</returns>
    [RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
    public static List<T> ParseList<T>(string response, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(response))
            return [];

        var opts = options ?? DefaultOptions;
        var trimmed = response.Trim();

        // Remove markdown code blocks
        trimmed = RemoveMarkdownCodeBlocks(trimmed);

        // Try JSON array first
        if (TryParseJsonArray<T>(trimmed, opts, out var jsonResult))
            return jsonResult;

        // For string lists, try text-based parsing
        if (typeof(T) == typeof(string))
        {
            var textList = ParseTextList(trimmed);
            return textList.Cast<T>().ToList();
        }

        // For primitive types, try to extract and parse
        if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
        {
            return ParsePrimitiveList<T>(trimmed, opts);
        }

        // Try to parse as JSON with wrapper object
        if (TryParseWrappedList<T>(trimmed, opts, out var wrappedResult))
            return wrappedResult;

        throw new JsonException($"Cannot parse response as List<{typeof(T).Name}>: {trimmed[..Math.Min(100, trimmed.Length)]}...");
    }

    /// <summary>
    /// Tries to parse AI response as a list.
    /// </summary>
    [RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
    public static bool TryParseList<T>(string response, [NotNullWhen(true)] out List<T>? result, JsonSerializerOptions? options = null)
    {
        try
        {
            result = ParseList<T>(response, options);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Parses text-based list formats (numbered, bullet points, newline-separated).
    /// </summary>
    public static List<string> ParseTextList(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return [];

        var trimmed = RemoveMarkdownCodeBlocks(response.Trim());

        // Try JSON array of strings first
        if (trimmed.StartsWith('['))
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (items is not null)
                    return items;
            }
            catch { /* Continue with text parsing */ }
        }

        var lines = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        foreach (var line in lines)
        {
            var cleaned = CleanListItem(line.Trim());
            if (!string.IsNullOrWhiteSpace(cleaned))
                result.Add(cleaned);
        }

        // If only one line, try comma-separated
        if (result.Count == 1 && result[0].Contains(','))
        {
            return result[0]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        return result;
    }

    private static string RemoveMarkdownCodeBlocks(string text)
    {
        var match = MarkdownCodeBlockPattern().Match(text);
        return match.Success ? match.Groups[1].Value.Trim() : text;
    }

    [RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
    private static bool TryParseJsonArray<T>(string json, JsonSerializerOptions options, [NotNullWhen(true)] out List<T>? result)
    {
        result = null;

        if (!json.StartsWith('['))
            return false;

        try
        {
            result = JsonSerializer.Deserialize<List<T>>(json, options);
            return result is not null;
        }
        catch
        {
            // Try to fix common issues
            var cleaned = CleanJsonArray(json);
            try
            {
                result = JsonSerializer.Deserialize<List<T>>(cleaned, options);
                return result is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    [RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
    private static bool TryParseWrappedList<T>(string json, JsonSerializerOptions options, [NotNullWhen(true)] out List<T>? result)
    {
        result = null;

        if (!json.StartsWith('{'))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Look for array properties
            string[] arrayFields = ["items", "list", "data", "results", "values", "elements", "array"];
            foreach (var field in arrayFields)
            {
                if (root.TryGetProperty(field, out var prop) && prop.ValueKind == JsonValueKind.Array)
                {
                    result = JsonSerializer.Deserialize<List<T>>(prop.GetRawText(), options);
                    return result is not null;
                }
            }

            // Try first array property
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    result = JsonSerializer.Deserialize<List<T>>(prop.Value.GetRawText(), options);
                    return result is not null;
                }
            }
        }
        catch { }

        return false;
    }

    [RequiresUnreferencedCode("JSON deserialization might require types that cannot be statically analyzed.")]
    private static List<T> ParsePrimitiveList<T>(string text, JsonSerializerOptions options)
    {
        var result = new List<T>();
        
        // Try to extract numbers or values
        var pattern = typeof(T) == typeof(int) || typeof(T) == typeof(long)
            ? IntegerPattern()
            : NumberPattern();

        foreach (Match match in pattern.Matches(text))
        {
            try
            {
                var value = JsonSerializer.Deserialize<T>($"{match.Value}", options);
                if (value is not null)
                    result.Add(value);
            }
            catch { }
        }

        return result;
    }

    private static string CleanListItem(string item)
    {
        // Remove common list prefixes: "1.", "1)", "-", "*", "•", "→"
        var cleaned = ListPrefixPattern().Replace(item, "").Trim();
        
        // Remove quotes if wrapped
        if (cleaned.StartsWith('"') && cleaned.EndsWith('"') && cleaned.Length > 2)
            cleaned = cleaned[1..^1];

        return cleaned;
    }

    private static string CleanJsonArray(string json)
    {
        // Remove trailing commas
        json = TrailingCommaPattern().Replace(json, "$1");
        return json;
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownCodeBlockPattern();

    [GeneratedRegex(@"^\s*(?:\d+[\.\)]\s*|[-*•→]\s*|>\s*)")]
    private static partial Regex ListPrefixPattern();

    [GeneratedRegex(@",\s*([}\]])")]
    private static partial Regex TrailingCommaPattern();

    [GeneratedRegex(@"-?\d+")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"-?\d+\.?\d*")]
    private static partial Regex NumberPattern();
}
