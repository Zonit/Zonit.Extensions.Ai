namespace Zonit.Extensions.Ai;

/// <summary>
/// Helpers for capturing prompt/output text into the usage tree. Caps length so a
/// very large prompt or response can't balloon the in-memory tree — enough to
/// identify the call, not necessarily the full payload.
/// </summary>
internal static class UsageText
{
    private const int MaxChars = 8_000;

    /// <summary>Truncates <paramref name="text"/> to a capture-friendly preview; <c>null</c> stays <c>null</c>.</summary>
    public static string? Preview(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= MaxChars
            ? text
            : string.Concat(text.AsSpan(0, MaxChars), "… [truncated]");
    }

    /// <summary>
    /// Best-effort textual description of a result value for the <c>Output</c> field.
    /// Strings pass through; other types fall back to <c>ToString()</c>. No serialization
    /// or reflection — AOT-safe.
    /// </summary>
    public static string? Describe<T>(T value) => value switch
    {
        null => null,
        string s => Preview(s),
        _ => Preview(value.ToString()),
    };
}
