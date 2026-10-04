using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai.Prompts;

namespace Zonit.Extensions.Ai.Tests.Prompts;

/// <summary>
/// Rendering checks for <see cref="TranslatePrompt"/>: every dedicated language section must
/// state its currency, unit and percent conventions explicitly (issue #29 — models filled the
/// gap differently and mixed conventions), and the fallback must cover them generically.
/// </summary>
public class TranslatePromptTests
{
    private static readonly string[] DedicatedLanguages =
        ["en", "pl", "de", "es", "fr", "it", "pt", "nl", "sv", "da", "no", "fi", "ru", "uk", "cs", "sk", "hu", "tr", "ar"];

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var code in DedicatedLanguages)
            data.Add(code);
        return data;
    }

    private static string Render(string target) =>
        new ScribanPromptRenderer().Render(new TranslatePrompt { Content = "SOURCE", Target = target });

    [Theory]
    [MemberData(nameof(Languages))]
    public void DedicatedSection_StatesCurrencyUnitsAndPercent(string target)
    {
        var text = Render(target);

        text.Should().Contain("- **Currency:**", "each culture states where its symbol or code goes");
        text.Should().Contain("- **Units:**", "each culture states its unit names and rate form");
        text.Should().MatchRegex(@"- \*\*Numbers:\*\*[^\n]*%", "each culture states its percent spacing");
        text.Should().NotContain("No dedicated section is defined");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void UniversalRules_CoverCurrenciesUnitsAndConsistency(string target)
    {
        var text = Render(target);

        text.Should().Contain("- **Currencies.**");
        text.Should().Contain("currencies are never converted");
        text.Should().Contain("- **Units of measure.**");
        text.Should().Contain("write each currency and unit in one form throughout");
    }

    [Fact]
    public void Polish_GivesTheFormsFromTheIssue()
    {
        var text = Render("pl-PL");

        text.Should().Contain("`4 327,29 USD`");
        text.Should().Contain("`USD za galon`");
        text.Should().Contain("`12,5%`");
        text.Should().Contain("(`98,62 dolara`)");
    }

    [Fact]
    public void Turkish_PutsThePercentSignFirst()
        => Render("tr").Should().Contain("`%12,5`");

    [Fact]
    public void Fallback_CoversMoneyAndUnitsGenerically()
    {
        var text = Render("ja");

        text.Should().Contain("No dedicated section is defined");
        text.Should().Contain("money format");
        text.Should().Contain("\"per\" construction");
        text.Should().NotContain("- **Currency:**");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Renders_WithoutLeftoverTemplateSyntax(string target)
    {
        var text = Render(target);

        // The only braces left are the literal `{{name}}` placeholder example.
        text.Replace("{{name}}", string.Empty).Should().NotContain("{{").And.NotContain("}}");
        text.Should().Contain("SOURCE");
    }
}
