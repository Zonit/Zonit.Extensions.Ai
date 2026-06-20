using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// LIVE end-to-end smoke test of the Anthropic HTTP Messages API agent tool-calling loop.
/// Opt-in only — spends real credits, so it stays inert in CI: set <c>ZONIT_ANTHROPIC_SMOKE=1</c>
/// and <c>ZONIT_ANTHROPIC_KEY=&lt;sk-ant-...&gt;</c> to enable. Forces <c>Transport=Api</c> so
/// this never touches the local Claude CLI.
/// </summary>
public class AnthropicAgentLiveSmokeTests
{
    private readonly ITestOutputHelper _output;
    public AnthropicAgentLiveSmokeTests(ITestOutputHelper output) => _output = output;

    private IAiProvider? Build()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_ANTHROPIC_SMOKE") != "1") return null;
        var key = Environment.GetEnvironmentVariable("ZONIT_ANTHROPIC_KEY");
        if (string.IsNullOrWhiteSpace(key)) { _output.WriteLine("ZONIT_ANTHROPIC_KEY not set — skipping."); return null; }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAiAnthropic(AnthropicTransport.Api, o => o.ApiKey = key!);
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IAiProvider>();
    }

    [Fact]
    public async Task Agent_OnAnthropic_CallsCSharpTool_AndUsesItsResult()
    {
        var ai = Build();
        if (ai is null) return;

        var result = await ai
            .Agent(new Sonnet46(),
                "What is the secret access code? Call the get_secret_code tool to find out, "
                + "then reply with ONLY the code value, nothing else.")
            .AddTool(new SecretCodeTool())
            .MaxIterations(5)
            .RunAsync();

        _output.WriteLine($"Iterations: {result.Iterations}, ToolCalls: {result.ToolCalls.Count}, Final: {result.Value}");
        result.ToolCalls.Should().NotBeEmpty();
        result.Value.Should().Contain("ZX-9173-QQ");
    }

    [Fact]
    public async Task Agent_OnAnthropic_WithStructuredOutput_StillCallsTool()
    {
        var ai = Build();
        if (ai is null) return;

        var result = await ai
            .Agent(new Sonnet46(), new CodeAnswerPrompt
            {
                Text = "What is the secret access code? You MUST call the get_secret_code tool to find out. "
                     + "Do not guess. Put the exact code the tool returns into the 'code' field."
            })
            .AddTool(new SecretCodeTool())
            .MaxIterations(5)
            .RunAsync();

        _output.WriteLine($"Iterations: {result.Iterations}, ToolCalls: {result.ToolCalls.Count}, Code: {result.Value?.Code}");
        result.ToolCalls.Should().NotBeEmpty();
        result.Value!.Code.Should().Contain("ZX-9173-QQ");
    }

    [Fact]
    public async Task Agent_OnAnthropic_TwoSequentialToolCalls_CompleteLoop()
    {
        var ai = Build();
        if (ai is null) return;

        var result = await ai
            .Agent(new Sonnet46(),
                "Compute step by step using the tools only. First call add with a=2 and b=3. "
                + "Then call multiply with a=(that result) and b=10. Reply with ONLY the final number.")
            .AddTool(new AddTool())
            .AddTool(new MultiplyTool())
            .MaxIterations(8)
            .RunAsync();

        _output.WriteLine($"Iterations: {result.Iterations}, ToolCalls: {result.ToolCalls.Count}, Final: {result.Value}");
        result.ToolCalls.Should().NotBeEmpty();
        result.Value.Should().Contain("50");
    }

    [Fact]
    public async Task Agent_OnAnthropic_WithNonStrictToolSchema_DoesNotError()
    {
        var ai = Build();
        if (ai is null) return;

        var result = await ai
            .Agent(new Sonnet46(),
                "Call the echo tool with value \"ping\", then reply with ONLY what it returned.")
            .AddTool(new RawEchoTool())
            .MaxIterations(5)
            .RunAsync();

        _output.WriteLine($"Iterations: {result.Iterations}, ToolCalls: {result.ToolCalls.Count}, Final: {result.Value}");
        result.ToolCalls.Should().NotBeEmpty();
        result.Value.Should().Contain("echo:ping");
    }

    private sealed class CodeAnswer
    {
        [Description("The secret access code exactly as returned by the tool.")]
        public string Code { get; init; } = "";
    }

    private sealed class CodeAnswerPrompt : IPrompt<CodeAnswer>
    {
        public string? System { get; set; }
        public required string Text { get; set; }
        public IReadOnlyList<Asset>? Files { get; set; }
    }

    private sealed class SecretInput
    {
        [Description("Always pass an empty object; no parameters are required.")]
        public string? Reason { get; init; }
    }

    private sealed class SecretOutput { public string Code { get; init; } = ""; }

    private sealed class SecretCodeTool : ToolBase<SecretInput, SecretOutput>
    {
        public override string Name => "get_secret_code";
        public override string Description => "Returns the secret access code. Call this to learn the code.";
        public override Task<SecretOutput> ExecuteAsync(IRunContext context, SecretInput input, CancellationToken ct)
            => Task.FromResult(new SecretOutput { Code = "ZX-9173-QQ" });
    }

    private sealed class NumInput
    {
        [Description("First operand.")] public double A { get; init; }
        [Description("Second operand.")] public double B { get; init; }
    }

    private sealed class NumOutput { public double Result { get; init; } }

    private sealed class AddTool : ToolBase<NumInput, NumOutput>
    {
        public override string Name => "add";
        public override string Description => "Adds two numbers and returns their sum.";
        public override Task<NumOutput> ExecuteAsync(IRunContext context, NumInput input, CancellationToken ct)
            => Task.FromResult(new NumOutput { Result = input.A + input.B });
    }

    private sealed class MultiplyTool : ToolBase<NumInput, NumOutput>
    {
        public override string Name => "multiply";
        public override string Description => "Multiplies two numbers and returns their product.";
        public override Task<NumOutput> ExecuteAsync(IRunContext context, NumInput input, CancellationToken ct)
            => Task.FromResult(new NumOutput { Result = input.A * input.B });
    }

    private sealed class RawEchoTool : ITool
    {
        public string Name => "echo";
        public string Description => "Echoes the input value.";
        public System.Text.Json.JsonElement InputSchema =>
            System.Text.Json.JsonDocument.Parse(
                """{"type":"object","properties":{"value":{"type":"string"}}}""").RootElement;

        public Task<System.Text.Json.JsonElement> InvokeAsync(
            System.Text.Json.JsonElement arguments, CancellationToken cancellationToken)
        {
            var value = arguments.TryGetProperty("value", out var v) ? v.GetString() : "(none)";
            return Task.FromResult(System.Text.Json.JsonSerializer.SerializeToElement("echo:" + value));
        }
    }
}
