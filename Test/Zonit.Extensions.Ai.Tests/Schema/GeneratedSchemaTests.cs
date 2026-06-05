using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.Tests.Schema;

/// <summary>
/// Verifies the AOT-safe build-time pipeline: the <c>AiJsonSchemaGenerator</c> registers a
/// schema for every <see cref="PromptBase{TResponse}"/> response type, that schema is
/// byte-for-byte equivalent to the reflection-based <see cref="JsonSchemaGenerator"/>
/// (so switching providers onto it changes nothing the model sees), and the
/// <c>AiJsonTypeInfoGenerator</c> produces a working <c>JsonTypeInfo&lt;T&gt;</c> used by
/// <see cref="JsonResponseParser.DeserializeStructured{T}"/>.
/// </summary>
public class GeneratedSchemaTests
{
    public static readonly object[][] ResponseTypes =
    [
        [typeof(FlatResponse)],
        [typeof(NestedResponse)],
        [typeof(EnumResponse)],
        [typeof(CollectionResponse)],
        [typeof(SpecialResponse)],
    ];

    [Theory]
    [MemberData(nameof(ResponseTypes))]
    public void Generator_RegistersSchema_ForEveryPromptResponseType(System.Type responseType)
    {
        // If this fails, the source generator did not run / did not emit for the type —
        // the whole AOT-safe schema story is then silently falling back to reflection.
        AiSchemaRegistry.IsRegistered(responseType).Should().BeTrue(
            $"AiJsonSchemaGenerator should have emitted a build-time schema for {responseType.Name}");
    }

    [Theory]
    [MemberData(nameof(ResponseTypes))]
    public void GeneratedSchema_IsEquivalentTo_ReflectionSchema(System.Type responseType)
    {
        // GetSchema returns the source-generated schema (registered above);
        // JsonSchemaGenerator.Generate is the reflection reference implementation.
        var generated = AiSchemaRegistry.GetSchema(responseType);
        var reflection = JsonSchemaGenerator.Generate(responseType);

        Canonical(generated).Should().Be(Canonical(reflection));
    }

    [Fact]
    public void DeserializeStructured_RoundTrips_ThroughGeneratedTypeInfo()
    {
        const string json = """
            {"name":"Ada","age":36,"active":true,"score":9.5,"note":"hi","tag":"PIONEER"}
            """;

        var value = JsonResponseParser.DeserializeStructured<FlatResponse>(json);

        value.Name.Should().Be("Ada");          // required + init  -> UnsafeAccessor path
        value.Age.Should().Be(36);              // init
        value.Active.Should().BeTrue();         // set
        value.Score.Should().Be(9.5);
        value.Note.Should().Be("hi");           // nullable ref
        value.Tag.Should().Be(SampleEnum.Pioneer); // case-insensitive enum converter
    }

    [Fact]
    public void DeserializeStructured_ShortCircuits_StringResponses()
    {
        JsonResponseParser.DeserializeStructured<string>("plain text").Should().Be("plain text");
    }

    // Recursively canonicalises a schema so equivalence ignores object-key order and
    // array order (JSON Schema treats `required`/`enum`/type-unions as sets).
    private static string Canonical(JsonElement element) => Normalize(element)?.ToJsonString() ?? "null";

    private static JsonNode? Normalize(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, System.StringComparer.Ordinal))
                    obj[p.Name] = Normalize(p.Value);
                return obj;
            case JsonValueKind.Array:
                var nodes = element.EnumerateArray().Select(Normalize).ToList();
                nodes.Sort((a, b) => string.CompareOrdinal(a?.ToJsonString() ?? "null", b?.ToJsonString() ?? "null"));
                var arr = new JsonArray();
                foreach (var n in nodes) arr.Add(n);
                return arr;
            case JsonValueKind.Null:
                return null;
            default:
                return JsonValue.Create(element);
        }
    }
}

// --- Test prompt + response models (top-level & public so both generators see them) ---

public sealed class FlatPrompt : PromptBase<FlatResponse> { public override string Prompt => "x"; }

[Description("A flat response.")]
public sealed class FlatResponse
{
    [Description("The person's name.")]
    public required string Name { get; init; }
    public int Age { get; init; }
    public bool Active { get; set; }
    public double Score { get; init; }
    public string? Note { get; init; }
    public SampleEnum Tag { get; init; }
}

public enum SampleEnum { Pioneer, Builder, Maintainer }

public sealed class NestedPrompt : PromptBase<NestedResponse> { public override string Prompt => "x"; }

public sealed class NestedResponse
{
    public Inner Child { get; init; } = new();
    public string Title { get; init; } = "";
}

public sealed class Inner
{
    [Description("Inner value.")]
    public string Value { get; init; } = "";
    public int Weight { get; init; }
}

public sealed class EnumPrompt : PromptBase<EnumResponse> { public override string Prompt => "x"; }

public sealed class EnumResponse
{
    public SampleEnum Status { get; init; }
    public SampleEnum? Priority { get; init; }
}

public sealed class CollectionPrompt : PromptBase<CollectionResponse> { public override string Prompt => "x"; }

public sealed class CollectionResponse
{
    public List<string> Tags { get; init; } = [];
    public int[] Numbers { get; init; } = [];
    public List<Inner> Items { get; init; } = [];
}

public sealed class SpecialPrompt : PromptBase<SpecialResponse> { public override string Prompt => "x"; }

public sealed class SpecialResponse
{
    public System.Guid Id { get; init; }
    public System.DateTime When { get; init; }
    public System.DateOnly Day { get; init; }
    public System.Uri? Link { get; init; }
    public int? Count { get; init; }
    public System.Guid? Maybe { get; init; }
}
