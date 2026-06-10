using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai;

namespace Zonit.Extensions.Ai.Tests.Schema;

/// <summary>
/// Regression coverage for <see cref="JsonResponseParser.Parse{T}"/>.
/// <para>
/// Both parse entry points must tolerate the way models actually emit JSON:
/// enums as (case-insensitive) strings and dates in ISO-ish text. The bug this
/// guards against: <c>Parse&lt;T&gt;</c> used <c>DefaultOptions</c> which — unlike
/// <c>ProviderResponseOptions</c> used by <see cref="JsonResponseParser.DeserializeStructured{T}"/>
/// — carried no enum/date converters, so the source-generated POCO JsonTypeInfo
/// (which leaves enum properties with <c>Converter = null</c>) resolved STJ's
/// numeric-only enum converter and threw on <c>"pioneer"</c>. The two paths now
/// share the same resilient converters.
/// </para>
/// </summary>
public class JsonResponseParserTests
{
    // Parse<T> is annotated [RequiresUnreferencedCode]/[RequiresDynamicCode] for its
    // reflection fallback; these types have generated JsonTypeInfo so the AOT path runs.
#pragma warning disable IL2026, IL3050

    [Fact]
    public void Parse_ComplexType_WithStringEnum_Succeeds()
    {
        const string json = """
            {"name":"Ada","age":36,"active":true,"score":9.5,"note":"hi","tag":"pioneer"}
            """;

        var value = JsonResponseParser.Parse<FlatResponse>(json);

        value.Name.Should().Be("Ada");
        value.Tag.Should().Be(SampleEnum.Pioneer); // string enum, case-insensitive
    }

    [Fact]
    public void Parse_ComplexType_WithStringEnum_MatchesDeserializeStructured()
    {
        const string json = """
            {"status":"builder","priority":"maintainer"}
            """;

        var viaParse = JsonResponseParser.Parse<EnumResponse>(json);
        var viaStructured = JsonResponseParser.DeserializeStructured<EnumResponse>(json);

        viaParse.Status.Should().Be(SampleEnum.Builder);
        viaParse.Priority.Should().Be(SampleEnum.Maintainer);
        viaParse.Status.Should().Be(viaStructured.Status);
        viaParse.Priority.Should().Be(viaStructured.Priority);
    }

    [Fact]
    public void Parse_ComplexType_FromMarkdownFencedJson_WithStringEnum_Succeeds()
    {
        const string response = """
            Here you go:

            ```json
            {"name":"Grace","age":40,"active":false,"score":8.0,"note":null,"tag":"maintainer"}
            ```
            """;

        var value = JsonResponseParser.Parse<FlatResponse>(response);

        value.Name.Should().Be("Grace");
        value.Tag.Should().Be(SampleEnum.Maintainer);
    }

#pragma warning restore IL2026, IL3050
}
