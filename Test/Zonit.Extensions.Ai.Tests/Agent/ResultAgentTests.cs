using System.Text.Json;
using FluentAssertions;
using Xunit;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Tests.Agent;

/// <summary>
/// Smoke tests for <see cref="ResultAgent{T}"/> and <see cref="ToolInvocation"/>.
/// Confirms the shape of the type and that <see cref="ResultAgent{T}"/>
/// remains assignable to <see cref="Result{T}"/>.
/// </summary>
public class ResultAgentTests
{
    [Fact]
    public void ResultAgent_ShouldBeAssignableToResult()
    {
        var result = BuildSample("hello");

        Result<string> asBase = result;

        asBase.Value.Should().Be("hello");
        asBase.Should().BeOfType<ResultAgent<string>>();
    }

    [Fact]
    public void ResultAgent_ExposesAggregatedTotals()
    {
        var result = BuildSample("ok");

        result.Iterations.Should().Be(3);
        result.ToolCalls.Should().HaveCount(2);
        result.TotalUsage.TotalTokens.Should().Be(1500);
        result.TotalCost.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ToolInvocation_IsError_ShouldBeTrueWhenErrorOrBlocked()
    {
        var ok = new ToolInvocation
        {
            Iteration = 1,
            Name = "ok",
            Input = JsonDocument.Parse("{}").RootElement,
            Output = JsonDocument.Parse("""{"ok":true}""").RootElement,
            Duration = TimeSpan.FromMilliseconds(10),
        };
        ok.IsError.Should().BeFalse();

        var failed = ok with { Output = null, Error = "boom", ErrorType = "System.Exception" };
        failed.IsError.Should().BeTrue();

        var blocked = ok with { Blocked = true };
        blocked.IsError.Should().BeTrue();
    }

    private static ResultAgent<string> BuildSample(string value)
    {
        var callA = new ToolInvocation
        {
            Iteration = 1,
            Name = "alpha",
            Input = JsonDocument.Parse("""{"q":"x"}""").RootElement,
            Output = JsonDocument.Parse("""{"n":1}""").RootElement,
            Duration = TimeSpan.FromMilliseconds(12),
        };

        var callB = new ToolInvocation
        {
            Iteration = 2,
            Name = "beta",
            Input = JsonDocument.Parse("""{"q":"y"}""").RootElement,
            Output = null,
            Error = "network",
            ErrorType = "System.Net.Http.HttpRequestException",
            Duration = TimeSpan.FromMilliseconds(50),
        };

        var usage = new TokenUsage
        {
            InputTokens = 1000,
            OutputTokens = 500,
            InputCost = new Price(0.01m),
            OutputCost = new Price(0.02m),
        };

        return new ResultAgent<string>
        {
            Value = value,
            MetaData = new MetaData
            {
                Model = new DummyLlm(),
                Provider = "Test",
                PromptName = "Sample",
                Usage = usage,
            },
            Iterations = 3,
            ToolCalls = new[] { callA, callB },
            TotalUsage = usage,
            TotalCost = usage.TotalCost,
        };
    }

    private sealed class DummyLlm : ILlm
    {
        public string Name => "dummy";
        public int MaxTokens => 1024;
        public decimal PriceInput => 0;
        public decimal PriceOutput => 0;
        public decimal? BatchPriceInput => null;
        public decimal? BatchPriceOutput => null;
        public int MaxInputTokens => 1024;
        public int MaxOutputTokens => 1024;
        public ChannelType Input => ChannelType.Text;
        public ChannelType Output => ChannelType.Text;
        public ToolsType SupportedTools => ToolsType.None;
        public FeaturesType SupportedFeatures => FeaturesType.None;
        public EndpointsType SupportedEndpoints => EndpointsType.None;
        public decimal GetInputPrice(long tokenCount) => 0;
        public decimal GetOutputPrice(long tokenCount) => 0;
        public IToolBase[]? Tools => null;
    }
}
