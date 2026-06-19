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
/// LIVE end-to-end smoke test of the Anthropic <b>SDK/CLI</b> transport
/// (<c>Transport=Sdk</c>, <c>claude -p</c> + loopback MCP bridge). Opt-in only — spawns the
/// real CLI and consumes the subscription, so it stays inert in CI: set
/// <c>ZONIT_CLAUDE_SMOKE=1</c> and <c>ZONIT_CLAUDE_PATH=&lt;path to claude.exe&gt;</c>.
/// Mirrors the 4-scenario matrix used for the API providers so the SDK tool path is held to
/// the same bar (string agent, structured output, sequential tools, non-strict schema).
/// </summary>
public class AnthropicSdkAgentLiveSmokeTests
{
    private readonly ITestOutputHelper _output;
    public AnthropicSdkAgentLiveSmokeTests(ITestOutputHelper output) => _output = output;

    private IAiProvider? Build()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_CLAUDE_SMOKE") != "1") return null;
        var claudePath = Environment.GetEnvironmentVariable("ZONIT_CLAUDE_PATH");
        if (string.IsNullOrWhiteSpace(claudePath) || !System.IO.File.Exists(claudePath))
        {
            _output.WriteLine($"ZONIT_CLAUDE_PATH missing: '{claudePath}' — skipping.");
            return null;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAiAnthropic(AnthropicTransport.Sdk, o => o.Cli.ExecutablePath = claudePath);
        services.AddAiAgentToolBridge();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IAiProvider>();
    }

    [Fact]
    public async Task Sdk_CallsCSharpTool_AndUsesItsResult()
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

        _output.WriteLine($"Final: {result.Value}");
        result.Value.Should().Contain("ZX-9173-QQ");
    }

    [Fact]
    public async Task Sdk_WithStructuredOutput_StillCallsTool()
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

        _output.WriteLine($"Code: {result.Value?.Code}");
        result.Value!.Code.Should().Contain("ZX-9173-QQ");
    }

    [Fact]
    public async Task Sdk_TwoSequentialToolCalls_CompleteLoop()
    {
        var ai = Build();
        if (ai is null) return;

        var result = await ai
            .Agent(new Sonnet46(),
                "Compute using the tools only. First call add with a=2 and b=3. "
                + "Then call multiply with a=(that result) and b=10. Reply with ONLY the final number.")
            .AddTool(new AddTool())
            .AddTool(new MultiplyTool())
            .MaxIterations(8)
            .RunAsync();

        _output.WriteLine($"Final: {result.Value}");
        result.Value.Should().Contain("50");
    }

    [Fact]
    public async Task Sdk_WithNonStrictToolSchema_DoesNotError()
    {
        var ai = Build();
        if (ai is null) return;

        var result = await ai
            .Agent(new Sonnet46(),
                "Call the echo tool with value \"ping\", then reply with ONLY what it returned.")
            .AddTool(new RawEchoTool())
            .MaxIterations(5)
            .RunAsync();

        _output.WriteLine($"Final: {result.Value}");
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
        public override Task<SecretOutput> ExecuteAsync(SecretInput input, CancellationToken ct)
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
        public override Task<NumOutput> ExecuteAsync(NumInput input, CancellationToken ct)
            => Task.FromResult(new NumOutput { Result = input.A + input.B });
    }

    private sealed class MultiplyTool : ToolBase<NumInput, NumOutput>
    {
        public override string Name => "multiply";
        public override string Description => "Multiplies two numbers and returns their product.";
        public override Task<NumOutput> ExecuteAsync(NumInput input, CancellationToken ct)
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
