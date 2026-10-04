using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.Prompts;

namespace Zonit.Extensions.Ai.Tests.Prompts;

/// <summary>
/// LIVE check of the currency / unit rules in <see cref="TranslatePrompt"/> (issue #29) on the
/// kind of market text that exposed the gap: EN → pl through two model families.
/// </summary>
/// <remarks>
/// Opt-in only — real credits. <c>ZONIT_OPENAI_SMOKE=1</c> + <c>ZONIT_OPENAI_KEY</c>,
/// <c>ZONIT_ANTHROPIC_SMOKE=1</c> + <c>ZONIT_ANTHROPIC_KEY</c>.
/// </remarks>
public class TranslatePromptLiveSmokeTests
{
    private const string MarketText = """
        Gold settled at $4,327.29/oz, up 1.53%, while silver held at $52.10/oz.
        RBOB gasoline futures rose to 3.3995 USD/gal as refinery outages lifted the crack spread.
        WTI traded between $96.04 and $98.62/bbl; open interest reached 96,038 contracts.
        Corn eased to 4.12 USD/bu, Henry Hub gas climbed to 3.85 USD/MMBtu and TTF settled at 41.20 EUR/MWh.
        """;

    private readonly ITestOutputHelper _output;
    public TranslatePromptLiveSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task OpenAi_Polish_UsesPolishCurrencyAndUnitForms()
    {
        var ai = Build("OPENAI", (s, key) => s.AddAiOpenAi(key));
        if (ai is null) return;

        AssertPolish(await Translate(ai, new Luna6(), "pl"));
    }

    [Fact]
    public async Task Anthropic_Polish_UsesPolishCurrencyAndUnitForms()
    {
        var ai = Build("ANTHROPIC", (s, key) => s.AddAiAnthropic(key));
        if (ai is null) return;

        AssertPolish(await Translate(ai, new Sonnet55(), "pl"));
    }

    private async Task<string> Translate(IAiProvider ai, ILlm llm, string target)
    {
        var result = await ai.GenerateAsync(llm, new TranslatePrompt { Content = MarketText, Target = target, Source = "en" });
        _output.WriteLine($"{llm.Name} → {target} ({result.MetaData.Usage.TotalCost}):\n{result.Value}");

        // Non-breaking and thin spaces are legitimate grouping/percent spaces; compare on plain ones.
        return result.Value.Replace(' ', ' ').Replace(' ', ' ').Replace(' ', ' ');
    }

    private static void AssertPolish(string text)
    {
        text.Should().Contain("4 327,29", "the amount takes Polish separators");
        text.Should().NotContain("$4", "a $ sign becomes the code after the amount");
        text.Should().NotContain("USD/gal", "an English rate abbreviation is written out in Polish");
        text.Should().NotContain("USD/galon", "no half-translated rate form");
        text.Should().Contain("za galon");
        text.Should().Contain("96 038");
        text.Should().Contain("EUR/MWh", "SI-based rate codes stay as-is");
        text.Should().Contain("MMBtu");
    }

    private IAiProvider? Build(string provider, Action<IServiceCollection, string> register)
    {
        if (Environment.GetEnvironmentVariable($"ZONIT_{provider}_SMOKE") != "1") return null;

        var key = Environment.GetEnvironmentVariable($"ZONIT_{provider}_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            _output.WriteLine($"ZONIT_{provider}_KEY not set — skipping.");
            return null;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        register(services, key);

        return services.BuildServiceProvider().GetRequiredService<IAiProvider>();
    }
}
