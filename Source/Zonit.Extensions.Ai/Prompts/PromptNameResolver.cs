using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Resolves a user-facing prompt name from an <see cref="IPrompt"/> instance for
/// inclusion in <see cref="MetaData.PromptName"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two correctness concerns the naive <c>prompt.GetType().Name.Replace("Prompt", "")</c>
/// pattern previously used by every provider got wrong:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       The framework's <c>AiProvider.Materialize</c> wraps every
///       <c>PromptBase</c>-derived prompt in <c>RenderedPrompt&lt;T&gt;</c>
///       BEFORE the provider sees it, so <c>GetType().Name</c> returns the
///       wrapper's own CLR name (<c>"RenderedPrompt`1"</c>) — the original
///       prompt identity is lost. The <c>IRenderedPrompt</c> marker carries
///       the original name forward; this resolver prefers it when present.
///     </description>
///   </item>
///   <item>
///     <description>
///       <c>String.Replace("Prompt", "")</c> matches the substring anywhere,
///       not just at the end. <c>"PromptlyGreatPrompt".Replace("Prompt", "")</c>
///       yields <c>"lyGreat"</c>. We strip the suffix only.
///     </description>
///   </item>
///   <item>
///     <description>
///       Open generic prompts have a backtick + arity in their CLR name
///       (<c>"MyPrompt`1"</c>); we strip that too so the surfaced name is
///       always human-readable.
///     </description>
///   </item>
/// </list>
/// </remarks>
public static class PromptNameResolver
{
    private const string PromptSuffix = "Prompt";

    /// <summary>
    /// Returns the user-facing prompt name for <paramref name="prompt"/>.
    /// Never throws and never returns <c>null</c>; falls back to the empty
    /// string only if the prompt's CLR name is somehow blank.
    /// </summary>
    public static string Resolve(IPrompt? prompt)
    {
        if (prompt is null) return string.Empty;

        // Prefer the original (pre-Materialize) type name when the framework
        // facade has wrapped the prompt in RenderedPrompt<T>. Without this
        // every provider would surface "RenderedPrompt`1" instead of the
        // user's actual prompt name (e.g. "BriefPrompt").
        var typeName = prompt is IRenderedPrompt { OriginalPromptTypeName: { Length: > 0 } original }
            ? original
            : prompt.GetType().Name;

        // Strip generic arity suffix Type.Name appends to open generics
        // ("MyPrompt`1" → "MyPrompt").
        var backtick = typeName.IndexOf('`');
        if (backtick > 0) typeName = typeName[..backtick];

        // Strip the trailing "Prompt" suffix (suffix-only, NOT substring) so
        // "BriefPrompt" surfaces as "Brief" and "PromptlyPrompt" as "Promptly".
        if (typeName.Length > PromptSuffix.Length
            && typeName.EndsWith(PromptSuffix, StringComparison.Ordinal))
        {
            typeName = typeName[..^PromptSuffix.Length];
        }

        return typeName;
    }
}
