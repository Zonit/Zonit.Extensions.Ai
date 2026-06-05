using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Zonit.Extensions.Ai;

/// <summary>
/// AOT-safe registry of JSON Schemas precomputed at build time by the
/// <c>AiJsonSchemaGenerator</c> source generator. Each registered entry is the
/// finished schema JSON for a structured-output response type (the <c>T</c> of a
/// <see cref="PromptBase{TResponse}"/>) or a tool input type (the <c>TInput</c>
/// of <c>ToolBase&lt;TInput, TOutput&gt;</c>) — built by walking the type graph
/// at compile time, i.e. <b>zero reflection at runtime</b>.
/// </summary>
/// <remarks>
/// <para>
/// Registration happens through <see cref="Register"/>, called from a
/// <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/> the
/// generator emits in every consumer assembly. By the time any provider builds a
/// request, every schema the generator could compute is already present.
/// </para>
/// <para>
/// <see cref="GetSchema"/> returns the precomputed schema when available and
/// otherwise falls back to the reflection-based <see cref="JsonSchemaGenerator"/>.
/// The fallback is the <em>only</em> reflection touchpoint on the structured-output
/// path; under a source-generated build (the package default) it never executes for
/// the documented <see cref="PromptBase{TResponse}"/> pattern.
/// </para>
/// </remarks>
public static class AiSchemaRegistry
{
    // Raw schema JSON emitted by the generator, keyed by runtime type.
    private static readonly ConcurrentDictionary<Type, string> _schemas = new();

    // Parsed + cloned JsonElement cache (one parse per type, reused across calls).
    private static readonly ConcurrentDictionary<Type, JsonElement> _parsed = new();

    /// <summary>
    /// Registers a precomputed JSON schema for <paramref name="type"/>. Called by
    /// source-generated module initializers; application code rarely calls this.
    /// </summary>
    public static void Register(Type type, string schemaJson)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(schemaJson);
        _schemas[type] = schemaJson;
    }

    /// <summary>
    /// Returns <c>true</c> when a precomputed schema is registered for <paramref name="type"/>.
    /// </summary>
    public static bool IsRegistered(Type type) => _schemas.ContainsKey(type);

    /// <summary>
    /// Returns the JSON Schema for <paramref name="type"/>. Uses the AOT-safe
    /// source-generated schema when present; otherwise falls back to the
    /// reflection-based <see cref="JsonSchemaGenerator"/>.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification =
            "The AiJsonSchemaGenerator source generator emits a precomputed schema for every " +
            "PromptBase<T> response type and ToolBase<TInput,_> input type in the consumer " +
            "compilation (registered via a module initializer). The reflection-based " +
            "JsonSchemaGenerator.Generate fallback is reached only for types the generator did " +
            "not see — e.g. an ad-hoc SimplePrompt<T> whose T is shaped at runtime. Under the " +
            "source-generated build (the package default) the fallback never executes for the " +
            "documented typed-prompt pattern, so no unreferenced code is required.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification =
            "Same as above: the reflection-based JsonSchemaGenerator.Generate fallback only runs " +
            "for response/input types the source generator did not emit. Source-generated schemas " +
            "are plain JSON parsed with JsonDocument (no runtime code generation).")]
    public static JsonElement GetSchema(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (_parsed.TryGetValue(type, out var cached))
            return cached;

        if (_schemas.TryGetValue(type, out var json))
        {
            using var doc = JsonDocument.Parse(json);
            var element = doc.RootElement.Clone();
            _parsed[type] = element;
            return element;
        }

        // Fallback: the generator did not produce a schema for this type. This is
        // the lone reflection touchpoint and is genuinely gated (see remarks).
        return JsonSchemaGenerator.Generate(type);
    }
}
