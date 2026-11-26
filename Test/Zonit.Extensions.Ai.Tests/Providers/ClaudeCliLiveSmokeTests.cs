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

        // The run's trace comes back from the CLI's stream-json output (#34).
        _output.WriteLine($"Iterations: {result.Iterations}, tool calls: {string.Join(", ", result.ToolCalls.Select(c => c.Name))}");
        result.ToolCalls.Should().Contain(c => c.Name == "echo" && !c.IsError);
        result.Iterations.Should().BeGreaterThanOrEqualTo(2, "one response calls the tool, a later one answers");
    }

    /// <summary>
    /// A 42,000-character prompt — above cmd.exe's 8,191 and CreateProcess's 32,767 — reaches the
    /// CLI on stdin (#32). Opt-in via <c>ZONIT_CLAUDE_SMOKE=1</c>.
    /// </summary>
    [Fact]
    public async Task LongPrompt_TravelsOnStdin()
    {
        if (BuildProvider() is not { } ai) return;

        var prompt = string.Join(" ", Enumerable.Repeat("filler", 6000)) + "\nReply with the single word OK.";
        var result = await ai.GenerateAsync(new Haiku55(), prompt);

        _output.WriteLine("Answer: " + result.Value);
        result.Value.Should().Contain("OK");
    }

    /// <summary>
    /// An isolated run (the default) has no Claude Code tools and does not read the working
    /// directory or a CLAUDE.md above it (#33). Opt-in via <c>ZONIT_CLAUDE_SMOKE=1</c>.
    /// </summary>
    [Fact]
    public async Task Isolated_SeesNoToolsAndNoLocalContext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"zonit-isolation-{Guid.NewGuid():N}");
        var work = Path.Combine(root, "work");
        System.IO.Directory.CreateDirectory(work);
        try
        {
            System.IO.File.WriteAllText(Path.Combine(root, "CLAUDE.md"), "The magic word is BANANA.");
            System.IO.File.WriteAllText(Path.Combine(work, "notes.txt"), "the secret word is pineapple");
            if (BuildProvider(o => o.Cli.WorkingDirectory = work) is not { } ai) return;

            var result = await ai.GenerateAsync(new Haiku55(),
                "Answer in three lines. Line 1: the names of every tool you can call, comma-separated, or NONE. " +
                "Line 2: the secret word in notes.txt if you can read it, or UNKNOWN. " +
                "Line 3: the magic word from your instructions or memory, or UNKNOWN.");

            _output.WriteLine("Answer: " + result.Value);
            result.Value.Should().Contain("NONE");
            result.Value.Should().NotContainEquivalentOf("pineapple");
            result.Value.Should().NotContainEquivalentOf("BANANA");
        }
        finally
        {
            try { System.IO.Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }

    private IAiProvider? BuildProvider(Action<AnthropicOptions>? configure = null)
    {
        if (Environment.GetEnvironmentVariable("ZONIT_CLAUDE_SMOKE") != "1") return null;
        var claudePath = Environment.GetEnvironmentVariable("ZONIT_CLAUDE_PATH");
        if (string.IsNullOrEmpty(claudePath) || !System.IO.File.Exists(claudePath))
        {
            _output.WriteLine($"ZONIT_CLAUDE_PATH not set or missing: '{claudePath}' — skipping.");
            return null;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAiAnthropic(AnthropicTransport.Sdk, o =>
        {
            o.Cli.ExecutablePath = claudePath;
            configure?.Invoke(o);
        });
        return services.BuildServiceProvider().GetRequiredService<IAiProvider>();
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
