using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Zonit.Extensions;
using Zonit.Extensions.Ai.X;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// LIVE end-to-end smoke test of the X (Grok) agent tool-calling loop against the real
/// xAI Responses API. Opt-in only — it spends real credits, so it stays inert in CI:
/// set <c>ZONIT_X_SMOKE=1</c> and <c>ZONIT_X_KEY=&lt;xai-...&gt;</c> to enable.
/// </summary>
public class XAgentLiveSmokeTests
{
    private readonly ITestOutputHelper _output;
    public XAgentLiveSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Agent_OnGrok_CallsCSharpTool_AndUsesItsResult()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_X_SMOKE") != "1")
            return; // disabled by default

        var key = Environment.GetEnvironmentVariable("ZONIT_X_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            _output.WriteLine("ZONIT_X_KEY not set — skipping.");
            return;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAiX(key!);

        using var sp = services.BuildServiceProvider();
        var ai = sp.GetRequiredService<IAiProvider>();

        var result = await ai
            .Agent(new Grok43(),
                "What is the secret access code? Call the get_secret_code tool to find out, "
                + "then reply with ONLY the code value, nothing else.")
            .AddTool(new SecretCodeTool())
            .MaxIterations(5)
            .RunAsync();

        _output.WriteLine($"Iterations: {result.Iterations}");
        _output.WriteLine($"Tool calls: {result.ToolCalls.Count}");
        _output.WriteLine($"Final: {result.Value}");

        // The model can only produce this token by actually invoking the C# tool.
        result.ToolCalls.Should().NotBeEmpty("the model must call the tool to learn the code");
        result.Value.Should().Contain("ZX-9173-QQ");
    }

    // SCENARIO 2: structured-output agent (Agent<TResponse>) + a tool. This forces
    // response_format=json_schema strict alongside the tools. If the model is grammar-
    // constrained to the schema from turn 1, it cannot emit a function_call and will
    // hallucinate the code instead of calling the tool — the "doesn't call tools" report.
    [Fact]
    public async Task Agent_OnGrok_WithStructuredOutput_StillCallsTool()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_X_SMOKE") != "1") return;
        var key = Environment.GetEnvironmentVariable("ZONIT_X_KEY");
        if (string.IsNullOrWhiteSpace(key)) { _output.WriteLine("ZONIT_X_KEY not set — skipping."); return; }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAiX(key!);
        using var sp = services.BuildServiceProvider();
        var ai = sp.GetRequiredService<IAiProvider>();

        var result = await ai
            .Agent(new Grok43(), new CodeAnswerPrompt
            {
                Text = "What is the secret access code? You MUST call the get_secret_code tool to find out. "
                     + "Do not guess. Put the exact code the tool returns into the 'code' field."
            })
            .AddTool(new SecretCodeTool())
            .MaxIterations(5)
            .RunAsync();

        _output.WriteLine($"Iterations: {result.Iterations}");
        _output.WriteLine($"Tool calls: {result.ToolCalls.Count}");
        _output.WriteLine($"Final code: {result.Value?.Code}");

        result.ToolCalls.Should().NotBeEmpty("the model must call the tool, not hallucinate the code");
        result.Value!.Code.Should().Contain("ZX-9173-QQ");
    }

    // SCENARIO 3: two SEQUENTIAL tool calls (forces a 3+ turn loop). Each turn replays
    // the full history including prior function_call items — exercises the history shape.
    [Fact]
    public async Task Agent_OnGrok_TwoSequentialToolCalls_CompleteLoop()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_X_SMOKE") != "1") return;
        var key = Environment.GetEnvironmentVariable("ZONIT_X_KEY");
        if (string.IsNullOrWhiteSpace(key)) { _output.WriteLine("ZONIT_X_KEY not set — skipping."); return; }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAiX(key!);
        using var sp = services.BuildServiceProvider();
        var ai = sp.GetRequiredService<IAiProvider>();

        var result = await ai
            .Agent(new Grok43(),
                "Compute step by step using the tools only. First call add with a=2 and b=3. "
                + "Then call multiply with a=(that result) and b=10. Reply with ONLY the final number.")
            .AddTool(new AddTool())
            .AddTool(new MultiplyTool())
            .MaxIterations(8)
            .RunAsync();

        _output.WriteLine($"Iterations: {result.Iterations}");
        _output.WriteLine($"Tool calls: {result.ToolCalls.Count}");
        _output.WriteLine($"Final: {result.Value}");

        result.ToolCalls.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Value.Should().Contain("50");
    }

    // SCENARIO 4: a raw ITool whose JSON schema is NOT strict-shaped (no
    // additionalProperties:false, no required) — like an MCP tool. XAgentSession
    // hardcodes Strict=true on these, which OpenAI's session learned trips HTTP 400.
    [Fact]
    public async Task Agent_OnGrok_WithNonStrictToolSchema_DoesNotError()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_X_SMOKE") != "1") return;
        var key = Environment.GetEnvironmentVariable("ZONIT_X_KEY");
        if (string.IsNullOrWhiteSpace(key)) { _output.WriteLine("ZONIT_X_KEY not set — skipping."); return; }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAiX(key!);
        using var sp = services.BuildServiceProvider();
        var ai = sp.GetRequiredService<IAiProvider>();

        var result = await ai
            .Agent(new Grok43(),
                "Call the echo tool with value \"ping\", then reply with ONLY what it returned.")
            .AddTool(new RawEchoTool())
            .MaxIterations(5)
            .RunAsync();

        _output.WriteLine($"Iterations: {result.Iterations}");
        _output.WriteLine($"Tool calls: {result.ToolCalls.Count}");
        _output.WriteLine($"Final: {result.Value}");

        result.ToolCalls.Should().NotBeEmpty();
        result.Value.Should().Contain("echo:ping");
    }

    // SCENARIO 5: plain text generation on grok-4.5 (the newest model). Confirms the
    // model id is accepted, structured/reasoning wiring builds, and — for the EU
    // rate-limit question — surfaces any 429/limit as a failed assertion with the body.
    [Fact]
    public async Task Generate_OnGrok45_ReturnsText()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_X_SMOKE") != "1") return;
        var key = Environment.GetEnvironmentVariable("ZONIT_X_KEY");
        if (string.IsNullOrWhiteSpace(key)) { _output.WriteLine("ZONIT_X_KEY not set — skipping."); return; }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        // Pin a US region endpoint if requested — the default host geo-routes by IP
        // to the nearest region (eu-west-1 from the EU), which does not serve grok-4.5.
        var region = Environment.GetEnvironmentVariable("ZONIT_X_REGION");
        services.AddAiX(o =>
        {
            o.ApiKey = key!;
            if (!string.IsNullOrWhiteSpace(region))
                o.BaseUrl = $"https://{region}.api.x.ai";
        });
        using var sp = services.BuildServiceProvider();
        var ai = sp.GetRequiredService<IAiProvider>();
        _output.WriteLine($"Endpoint region: {region ?? "(default api.x.ai)"}");

        Result<string> result;
        try
        {
            result = await ai.GenerateAsync(
                new Grok45 { Reason = ReasoningEffort.Low },
                "Reply with ONLY the word: pong");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("not available in your region"))
        {
            // grok-4.5 is region-locked by xAI (e.g. not served to EU as of 2026-07).
            // The request built and authenticated correctly — the restriction is xAI's,
            // not ours — so treat this as a skip rather than a failure.
            _output.WriteLine("grok-4.5 not available in this region — skipping: " + ex.Message);
            return;
        }

        _output.WriteLine($"Model: {result.MetaData.Model.Name}");
        _output.WriteLine($"Tokens in/out: {result.MetaData.Usage.InputTokens}/{result.MetaData.Usage.OutputTokens}");
        _output.WriteLine($"Cost: ${result.MetaData.Usage.InputCost + result.MetaData.Usage.OutputCost}");
        _output.WriteLine($"Duration: {result.MetaData.Duration}");
        _output.WriteLine($"Final: {result.Value}");

        result.Value.Should().ContainEquivalentOf("pong");
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

    // Raw ITool with a permissive (non-strict) schema, exactly like an MCP-provided tool.
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

    private sealed class SecretInput
    {
        [Description("Always pass an empty object; no parameters are required.")]
        public string? Reason { get; init; }
    }

    private sealed class SecretOutput
    {
        public string Code { get; init; } = "";
    }

    private sealed class SecretCodeTool : ToolBase<SecretInput, SecretOutput>
    {
        public override string Name => "get_secret_code";
        public override string Description => "Returns the secret access code. Call this to learn the code.";

        public override Task<SecretOutput> ExecuteAsync(IRunContext context, SecretInput input, CancellationToken cancellationToken)
            => Task.FromResult(new SecretOutput { Code = "ZX-9173-QQ" });
    }
}
