using System.Reflection;
using FluentAssertions;
using Xunit;
using Zonit.Extensions.Ai.Anthropic;
using Zonit.Extensions.Ai.OpenAi;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Models;

/// <summary>
/// Pins the compile-time guardrails: settings a provider answers with HTTP 400 must not
/// be expressible on the model type. A regression here (a level re-added to an enum, a
/// sampling property un-hidden) would compile again and fail only at runtime.
/// </summary>
public class ModelGuardrailTests
{
    [Theory]
    [InlineData(typeof(Sonnet55.ToolStepReasonType))]
    public void ToolStepReasoning_OffersOnlyLowMediumHigh(Type enumType)
        // between_tools at xhigh / max is a 400.
        => Enum.GetNames(enumType).Should().Equal("Low", "Medium", "High");

    [Theory]
    // thinking: disabled is a 400 on these models, so "None" must not exist.
    [InlineData(typeof(Opus55.ReasonType))]
    [InlineData(typeof(Sonnet55.ReasonType))]
    [InlineData(typeof(Fable5.ReasonType))]
    [InlineData(typeof(Fable51.ReasonType))]
    [InlineData(typeof(Mythos5.ReasonType))]
    [InlineData(typeof(Mythos51.ReasonType))]
    // reasoning effort "none" is a 400.
    [InlineData(typeof(OpenAiReasonEffortAlwaysOn))]
    [InlineData(typeof(Grok47.ReasonType))]
    public void AlwaysThinkingModels_HaveNoNoneLevel(Type enumType)
        => Enum.GetNames(enumType).Should().NotContain("None");

    [Theory]
    [InlineData(typeof(Grok47.ReasonType))]
#pragma warning disable CS0618 // superseded models keep their guardrails
    [InlineData(typeof(Grok46.ReasonType))]
    [InlineData(typeof(Grok45.ReasonType))]
    [InlineData(typeof(Grok43.ReasonType))]
#pragma warning restore CS0618
    public void GrokLevels_NeverIncludeMax(Type enumType)
        => Enum.GetNames(enumType).Should().NotContain("Max");

    [Fact]
    public void Grok45_HasNoXhigh()
#pragma warning disable CS0618
        => Enum.GetNames<Grok45.ReasonType>().Should().NotContain("Extra");
#pragma warning restore CS0618

    [Theory]
    [InlineData(typeof(Opus55))]
    [InlineData(typeof(Sonnet55))]
    [InlineData(typeof(Fable51))]
    [InlineData(typeof(Mythos51))]
#pragma warning disable CS0618
    [InlineData(typeof(Opus5))]
    [InlineData(typeof(Sonnet5))]
    [InlineData(typeof(Opus48))]
    [InlineData(typeof(Opus47))]
    [InlineData(typeof(Fable5))]
    [InlineData(typeof(Mythos5))]
#pragma warning restore CS0618
    public void ModelsThatRejectSampling_HideTemperatureAndTopPBehindACompileError(Type modelType)
    {
        foreach (var name in new[] { "Temperature", "TopP" })
        {
            var property = modelType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name == name)
                .OrderByDescending(p => Depth(p.DeclaringType!))
                .First();

            property.GetCustomAttribute<ObsoleteAttribute>()
                .Should().NotBeNull($"{modelType.Name}.{name} must not compile")
                .And.Match<ObsoleteAttribute>(a => a.IsError);
        }
    }

    [Theory]
#pragma warning disable CS0618
    [InlineData(typeof(Sonnet46))]
#pragma warning restore CS0618
    [InlineData(typeof(Haiku45))]
    public void ModelsThatAcceptSampling_KeepTemperatureUsable(Type modelType)
        => modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name == "Temperature")
            .Should().OnlyContain(p => p.GetCustomAttribute<ObsoleteAttribute>() == null);

    private static int Depth(Type type)
    {
        var depth = 0;
        for (var t = type; t is not null; t = t.BaseType) depth++;
        return depth;
    }
}
