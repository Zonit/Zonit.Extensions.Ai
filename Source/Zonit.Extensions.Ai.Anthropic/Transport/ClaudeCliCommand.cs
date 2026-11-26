using System.Collections;
using System.Text;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// The parts of a <c>claude -p</c> invocation shared by the single-shot transport
/// (<see cref="AnthropicCliTransport"/>) and the agent session (<see cref="CliAgentSession"/>):
/// the system prompt passed as a file, the isolation flags, the working directory and the child
/// environment.
/// </summary>
/// <remarks>
/// Nothing large travels on the command line. The prompt goes to stdin and the system prompt and
/// MCP config to files, so the invocation stays far below the Windows limits (8,191 characters
/// through <c>cmd.exe</c> for the npm <c>claude.cmd</c> shim, 32,767 for <c>CreateProcess</c>).
/// </remarks>
internal static class ClaudeCliCommand
{
    /// <summary>
    /// Flags added when <see cref="AnthropicCliOptions.Isolated"/> is on: no built-in tools, no MCP
    /// servers besides an explicit <c>--mcp-config</c>, no user/project settings, CLAUDE.md or
    /// memory, no skills or slash commands, no saved session.
    /// </summary>
    internal static readonly IReadOnlyList<string> IsolationArguments =
    [
        "--tools", "",
        "--strict-mcp-config",
        "--setting-sources", "local",
        "--disable-slash-commands",
        "--no-session-persistence",
    ];

    /// <summary>
    /// <c>CLAUDE_CODE_*</c> variables that carry the caller's own credentials or routing and are
    /// therefore kept when the environment is scrubbed.
    /// </summary>
    private static readonly HashSet<string> KeptClaudeCodeVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLAUDE_CODE_OAUTH_TOKEN",
        "CLAUDE_CODE_USE_BEDROCK",
        "CLAUDE_CODE_USE_VERTEX",
        "CLAUDE_CODE_USE_FOUNDRY",
        "CLAUDE_CODE_SKIP_BEDROCK_AUTH",
        "CLAUDE_CODE_SKIP_VERTEX_AUTH",
        "CLAUDE_CODE_SKIP_FOUNDRY_AUTH",
        "CLAUDE_CODE_GIT_BASH_PATH",
        "CLAUDE_CODE_CLIENT_CERT",
        "CLAUDE_CODE_CLIENT_KEY",
        "CLAUDE_CODE_CLIENT_KEY_PASSPHRASE",
    };

    /// <summary>Appends the isolation flags when <see cref="AnthropicCliOptions.Isolated"/> is on.</summary>
    internal static void AddIsolation(List<string> args, AnthropicCliOptions cli)
    {
        if (cli.Isolated)
            args.AddRange(IsolationArguments);
    }

    /// <summary>
    /// Passes the system text as a file. Isolated, it <b>replaces</b> Claude Code's own system
    /// prompt (an empty file leaves the model with no system prompt, as on the API); otherwise it
    /// is appended to Claude Code's prompt, and omitted when empty.
    /// </summary>
    internal static void AddSystemPrompt(List<string> args, ClaudeCliScratch scratch, string? systemText, AnthropicCliOptions cli)
    {
        if (cli.Isolated)
        {
            args.Add("--system-prompt-file");
            args.Add(scratch.WriteFile("system-prompt.md", systemText ?? string.Empty));
        }
        else if (!string.IsNullOrEmpty(systemText))
        {
            args.Add("--append-system-prompt-file");
            args.Add(scratch.WriteFile("append-system-prompt.md", systemText));
        }
    }

    /// <summary>
    /// The configured <see cref="AnthropicCliOptions.WorkingDirectory"/>; otherwise, when isolated,
    /// a fresh empty directory inside <paramref name="scratch"/>; otherwise <c>null</c> (the host
    /// process directory).
    /// </summary>
    internal static string? WorkingDirectory(AnthropicCliOptions cli, ClaudeCliScratch scratch)
    {
        if (!string.IsNullOrWhiteSpace(cli.WorkingDirectory)) return cli.WorkingDirectory;
        return cli.Isolated ? scratch.CreateDirectory("cwd") : null;
    }

    /// <summary>
    /// Environment changes for the child process: when isolated, removes the variables a parent
    /// Claude Code session hands its children and <c>ANTHROPIC_API_KEY</c>; then layers the
    /// configured tokens and <see cref="AnthropicCliOptions.AdditionalEnvironment"/> (applied last,
    /// so it can re-add anything). A <c>null</c> value removes the variable.
    /// </summary>
    internal static Dictionary<string, string?>? BuildEnvironment(AnthropicCliOptions cli, IDictionary? ambient = null)
    {
        Dictionary<string, string?>? env = null;
        Dictionary<string, string?> Env() => env ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (cli.Isolated)
        {
            foreach (DictionaryEntry entry in ambient ?? Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string name && IsInheritedSessionVariable(name))
                    Env()[name] = null;
            }
        }

        if (!string.IsNullOrEmpty(cli.OAuthToken)) Env()["CLAUDE_CODE_OAUTH_TOKEN"] = cli.OAuthToken;
        if (!string.IsNullOrEmpty(cli.AuthToken)) Env()["ANTHROPIC_AUTH_TOKEN"] = cli.AuthToken;

        if (cli.AdditionalEnvironment is { } extra)
            foreach (var kv in extra)
                Env()[kv.Key] = kv.Value;

        return env;
    }

    /// <summary>
    /// Whether <paramref name="name"/> is a variable an isolated run removes: the markers and
    /// settings of a parent Claude Code / Agent SDK session (<c>CLAUDECODE</c>, <c>CLAUDE_CODE_*</c>
    /// except credentials and routing, <c>CLAUDE_AGENT_SDK_*</c>) and <c>ANTHROPIC_API_KEY</c>, which
    /// would bill the API instead of the subscription.
    /// </summary>
    internal static bool IsInheritedSessionVariable(string name)
    {
        if (name.Equals("CLAUDECODE", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.Equals("ANTHROPIC_API_KEY", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("CLAUDE_AGENT_SDK_", StringComparison.OrdinalIgnoreCase)) return true;
        return name.StartsWith("CLAUDE_CODE_", StringComparison.OrdinalIgnoreCase)
            && !KeptClaudeCodeVariables.Contains(name);
    }
}

/// <summary>
/// A per-invocation temporary directory holding the files a <c>claude</c> run reads (system
/// prompt, MCP config) and, for an isolated run, its empty working directory. Disposing deletes it.
/// </summary>
internal sealed class ClaudeCliScratch : IDisposable
{
    public ClaudeCliScratch()
    {
        Root = Path.Combine(Path.GetTempPath(), $"zonit-claude-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(Root);
    }

    /// <summary>Absolute path of the temporary directory.</summary>
    public string Root { get; }

    /// <summary>Writes <paramref name="content"/> as UTF-8 (no BOM) and returns the file's path.</summary>
    public string WriteFile(string fileName, string content)
    {
        var path = Path.Combine(Root, fileName);
        System.IO.File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    /// <summary>Creates (once) and returns an empty subdirectory.</summary>
    public string CreateDirectory(string name)
    {
        var path = Path.Combine(Root, name);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Root, recursive: true); }
        catch { /* best effort: a file may still be held by an exiting process */ }
    }
}
