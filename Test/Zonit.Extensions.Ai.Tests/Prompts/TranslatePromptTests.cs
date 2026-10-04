using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai.Prompts;

namespace Zonit.Extensions.Ai.Tests.Prompts;

/// <summary>
/// Rendering checks for <see cref="TranslatePrompt"/>. Two groups: every language section states its
/// currency, unit and percent conventions (issue #29), and the template avoids the prompt
/// anti-patterns that leak into output — dash characters, literal currencies and units, bold
/// emphasis and capitalised mandates (issue #29, follow-up review).
/// </summary>
public class TranslatePromptTests
{
    private const string Source = "SOURCE";

    private static readonly string[] DedicatedLanguages =
        ["en", "pl", "de", "es", "fr", "it", "pt", "nl", "sv", "da", "no", "fi", "ru", "uk", "cs", "sk", "hu", "tr", "ar"];

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var code in DedicatedLanguages)
            data.Add(code);
        data.Add("ja"); // fallback
        return data;
    }

    public static TheoryData<string> DedicatedOnly()
    {
        var data = new TheoryData<string>();
        foreach (var code in DedicatedLanguages)
            data.Add(code);
        return data;
    }

    private static string Render(string target) =>
        new ScribanPromptRenderer().Render(new TranslatePrompt { Content = Source, Target = target });

    /// <summary>The language section alone: from its heading to the source text.</summary>
    private static string Section(string target)
    {
        var text = Render(target);
        var start = text.IndexOf(" conventions\n", StringComparison.Ordinal);
        var end = text.IndexOf("<source_text>\n", start, StringComparison.Ordinal);
        return text[start..end];
    }

    [Theory]
    [MemberData(nameof(DedicatedOnly))]
    public void DedicatedSection_StatesCurrencyUnitsAndPercent(string target)
    {
        var text = Section(target);

        text.Should().Contain("\n- Currency: ", "each culture states where its symbol or code goes");
        text.Should().Contain("\n- Units: ", "each culture states its per form for a rate");
        text.Should().MatchRegex(@"\n- Numbers: [^\n]*%", "each culture states its percent spacing");
        text.Should().NotContain("an educated native reader", "the fallback is only for languages without a section");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Template_StatesTheTaskAndKeepsTheOutputContractLast(string target)
    {
        var text = Render(target).TrimEnd();

        text.Should().StartWith("Translate the text inside <source_text> into ");
        text.Should().Contain("<source_text>\n" + Source + "\n</source_text>");
        text.Should().EndWith("Keep the structure, markup and tokens of the source.");
        text.Should().Contain("A number written next to an identifier, field name or code is still a number");
        text.Should().Contain("An amount stays in its source currency");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Template_HasNoDashCharacters(string target)
    {
        // A prompt's style is mirrored in its output, and several sections forbid these characters.
        Render(target).Should().NotContain("—").And.NotContain("–");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Template_ShowsFormatsWithPlaceholders_NotRealCurrenciesOrUnits(string target)
    {
        var text = Render(target);

        // Literal examples are copied into the translation and bias the currency or unit chosen.
        foreach (var literal in new[] { "USD", "EUR", "GBP", "PLN", "bbl", "gal", "oz", "lb", "MWh", "MMBtu" })
            text.Should().NotMatchRegex($@"\b{literal}\b");
        foreach (var symbol in new[] { "€", "£", "¥", "zł" })
            text.Should().NotContain(symbol);

        // `$` appears only in the `$VAR` placeholder example.
        text.Replace("$VAR", string.Empty).Should().NotContain("$");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Template_UsesPlainLabels_WithoutEmphasis(string target)
    {
        var text = Render(target);

        text.Should().NotContain("**", "bold labels are mirrored as emphasis");
        text.Should().NotContain("ONLY");
        text.Should().NotContain("You are ", "the first sentence states the task, not a persona");
    }

    [Fact]
    public void Polish_GivesThePlaceholderForms()
    {
        var text = Render("pl-PL");

        text.Should().Contain("- Currency: <amount> <code>, with a space before the code.");
        text.Should().Contain("<amount> <code> za <unit>");
        text.Should().Contain("(12,5%)");
    }

    [Fact]
    public void Turkish_PutsThePercentSignFirst()
        => Render("tr").Should().Contain("(%12,5)");

    [Fact]
    public void Fallback_CoversMoneyAndUnitsGenerically()
    {
        var text = Section("ja");

        text.Should().Contain("an educated native reader of Japanese expects");
        text.Should().Contain("currency position and per form for units");
        text.Should().NotContain("\n- Currency: ");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Renders_WithoutLeftoverTemplateSyntax(string target)
    {
        var text = Render(target);

        // The only braces left are the literal `{{name}}` placeholder example.
        text.Replace("{{name}}", string.Empty).Should().NotContain("{{").And.NotContain("}}");
    }
}
