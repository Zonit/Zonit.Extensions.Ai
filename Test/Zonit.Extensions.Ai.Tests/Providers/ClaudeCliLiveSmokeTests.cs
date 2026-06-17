using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Zonit.Extensions;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions.Ai.Tests.Providers;

/// <summary>
/// LIVE end-to-end smoke test of the SDK transport: runs a real <c>claude -p</c> agent that
/// calls a C# tool through the loopback MCP bridge. Opt-in only — it spawns the real CLI and
/// consumes the user's Claude subscription, so it stays inert in CI: set
/// <c>ZONIT_CLAUDE_SMOKE=1</c> and <c>ZONIT_CLAUDE_PATH=&lt;path to claude.exe&gt;</c> to enable.
/// </summary>
public class ClaudeCliLiveSmokeTests
{
    private readonly ITestOutputHelper _output;
    public ClaudeCliLiveSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Agent_OverClaudeCli_CallsCSharpToolThroughBridge()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_CLAUDE_SMOKE") != "1")
            return; // disabled by default

        var claudePath = Environment.GetEnvironmentVariable("ZONIT_CLAUDE_PATH");
        if (string.IsNullOrEmpty(claudePath) || !System.IO.File.Exists(claudePath))
        {
            _output.WriteLine($"ZONIT_CLAUDE_PATH not set or missing: '{claudePath}' — skipping.");
            return;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAiAnthropic(AnthropicTransport.Sdk, o => o.Cli.ExecutablePath = claudePath); // agent via claude -p
        services.AddAiAgentToolBridge();                 // expose C# tools to the CLI over loopback MCP

        using var sp = services.BuildServiceProvider();
        var ai = sp.GetRequiredService<IAiProvider>();

        var result = await ai
            .Agent(new Sonnet46(),
                "Call the `echo` tool with the value \"ping\". " +
                "Then reply with ONLY the exact text the tool returned, nothing else.")
            .AddTool(new EchoTool())
            .MaxIterations(5)
            .RunAsync();

        _output.WriteLine("Agent result: " + result.Value);
        // "echo:ping" can only appear if the CLI actually reached our C# tool over the bridge.
        result.Value.Should().Contain("echo:ping");
    }

    /// <summary>
    /// Proves OS auto-discovery (no explicit path) actually locates the CLI on this machine —
    /// including the Claude Desktop-bundled binary, which is not on PATH. Opt-in via
    /// <c>ZONIT_CLAUDE_SMOKE=1</c>.
    /// </summary>
    [Fact]
    public void AutoDiscovery_FindsClaudeBinaryWithoutExplicitPath()
    {
        if (Environment.GetEnvironmentVariable("ZONIT_CLAUDE_SMOKE") != "1")
            return; // disabled by default

        var resolved = ClaudeCliLocator.Resolve(explicitPath: null); // pure auto-discovery
        _output.WriteLine("Auto-discovered claude at: " + resolved);

        System.IO.File.Exists(resolved).Should().BeTrue();
    }
}
