using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Agent session that runs the whole agentic loop through the Claude Code CLI
/// (<c>claude -p</c>). Unlike the HTTP session, the CLI owns the loop and executes tools
/// itself — the framework's <see cref="ITool"/> set is exposed to it over the loopback
/// MCP bridge (<see cref="IAgentToolBridge"/>). One <see cref="RunTurnAsync"/> drives the
/// entire run to completion and returns a single terminal <see cref="AgentTurn"/>
/// (<c>ToolCalls</c> empty — tools already ran CLI-side), so the runner's loop ends after
/// one turn.
/// </summary>
/// <remarks>
/// Consequence of CLI-owned execution: the framework's <c>ToolExecutor</c>,
/// per-tool-timeout, <c>MaxIterations</c> and nested-usage tracking do not apply to this
/// path; usage is taken from the CLI's reported token counts.
/// </remarks>
internal sealed class CliAgentSession : IAgentSession
{
    private readonly AgentSessionContext _context;
    private readonly IClaudeCliRunner _runner;
    private readonly IAgentToolBridge? _bridge;
    private readonly AnthropicOptions _options;
    private readonly AiResilienceOptions _resilience;
    private readonly ILogger _logger;

    private bool _completed;
    private AgentTurn? _finalTurn;

    public CliAgentSession(
        AgentSessionContext context,
        IClaudeCliRunner runner,
        IAgentToolBridge? bridge,
        AnthropicOptions options,
        AiResilienceOptions resilience,
        ILogger logger)
    {
        _context = context;
        _runner = runner;
        _bridge = bridge;
        _options = options;
        _resilience = resilience;
        _logger = logger;
    }

    [RequiresUnreferencedCode("Structured-output schema lookup uses reflection over the response type when no source-generated schema exists.")]
    [RequiresDynamicCode("Structured-output schema lookup uses reflection over the response type when no source-generated schema exists.")]
    public async Task<AgentTurn> RunTurnAsync(IReadOnlyList<ToolResult>? toolResults, CancellationToken cancellationToken)
    {
        // The CLI runs the whole loop in one shot; subsequent turns never carry tool
        // calls. If the runner calls us again, return the already-computed terminal turn.
        if (_completed) return _finalTurn!;

        var tools = _context.Tools;
        IAgentToolBridgeSession? bridgeSession = null;
        try
        {
            if (tools.Count > 0)
            {
                if (_bridge is null)
                    throw new NotSupportedException(
                        "Agent has tools but no IAgentToolBridge is registered — the Claude Code CLI cannot call them. " +
                        "Install Zonit.Extensions.Ai.Mcp.Server and call AddAiAgentToolBridge().");

                // The CLI invokes tools over the bridge through the context-less ITool path, so scoped
                // tools (ToolBase<TScope,…>) and sub-agent tools must have their trusted context (and
                // seeded chat) bound here — the in-process ToolExecutor cannot run on this transport.
                var bridgeTools = AgentToolContextBinder.Bind(tools, _context.Context, _context.InitialChat);
                bridgeSession = await _bridge.StartAsync(bridgeTools, cancellationToken).ConfigureAwait(false);
            }

            var invocation = BuildInvocation(bridgeSession);
            _logger.LogDebug(
                "Claude CLI agent run: {Exe} (model {Model}, tools {ToolCount})",
                invocation.ExecutablePath, _context.Llm.Name, tools.Count);

            var sw = Stopwatch.StartNew();
            var process = await _runner.RunAsync(invocation, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Claude CLI agent run for model '{_context.Llm.Name}' exited with code {process.ExitCode}. " +
                    $"stderr: {Truncate(process.StandardError)}");

            var parsed = ParseResult(process);
            if (parsed.IsError || parsed.Type == "error")
                throw new InvalidOperationException(
                    $"Claude CLI agent run for model '{_context.Llm.Name}' returned an error: {parsed.Result ?? parsed.Subtype ?? "(no detail)"}");

            _finalTurn = BuildTurn(parsed, sw.Elapsed);
            _completed = true;
            return _finalTurn;
        }
        finally
        {
            if (bridgeSession is not null)
                await bridgeSession.DisposeAsync().ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode("Builds the prompt, which may resolve a structured-output schema via reflection.")]
    [RequiresDynamicCode("Builds the prompt, which may resolve a structured-output schema via reflection.")]
    private ClaudeCliInvocation BuildInvocation(IAgentToolBridgeSession? bridgeSession)
    {
        var args = new List<string>
        {
            "--print",
            BuildPrompt(),
            "--output-format", "json",
            "--model", _context.Llm.Name,
        };

        if (bridgeSession is not null)
        {
            args.Add("--mcp-config");
            args.Add(BuildMcpConfig(bridgeSession));

            // Pre-approve our bridge's tools so the CLI runs them unattended.
            foreach (var name in bridgeSession.ToolNames)
            {
                args.Add("--allowedTools");
                args.Add($"mcp__{bridgeSession.ServerName}__{name}");
            }
        }

        if (!string.IsNullOrWhiteSpace(_options.Cli.PermissionMode))
        {
            args.Add("--permission-mode");
            args.Add(_options.Cli.PermissionMode);
        }

        if (_options.Cli.AdditionalArguments is { Length: > 0 } extra)
            args.AddRange(extra);

        return new ClaudeCliInvocation
        {
            ExecutablePath = ClaudeCliLocator.Resolve(_options.Cli.ExecutablePath),
            Arguments = args,
            WorkingDirectory = _options.Cli.WorkingDirectory,
            EnvironmentOverrides = BuildEnvironment(),
            Timeout = _options.Cli.Timeout ?? _options.Timeout ?? _resilience.TotalRequestTimeout,
        };
    }

    /// <summary>
    /// Composes the single prompt for <c>claude -p</c>: any seeded conversation flattened
    /// to a transcript, then the agent instruction (<c>Prompt.Text</c>), plus a JSON-schema
    /// directive when structured output is expected — mirroring how the HTTP agent session
    /// folds the schema into the initial user message.
    /// </summary>
    [RequiresUnreferencedCode("AiSchemaRegistry.GetSchema may use reflection over the response type.")]
    [RequiresDynamicCode("AiSchemaRegistry.GetSchema may use reflection over the response type.")]
    private string BuildPrompt()
    {
        var sb = new StringBuilder();

        if (_context.InitialChat is { Count: > 0 } chat)
        {
            foreach (var message in chat)
            {
                var (label, text) = message switch
                {
                    Assistant a => ("Assistant", a.Text),
                    User u => ("User", u.Text),
                    Tool t => ("Tool result", t.ResultJson),
                    _ => (message.GetType().Name, null),
                };
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(label).Append(": ").Append(text);
            }
        }

        if (sb.Length > 0) sb.Append("\n\n");
        sb.Append(_context.Prompt.Text);

        if (_context.ResponseType is { } responseType)
        {
            var schema = AiSchemaRegistry.GetSchema(responseType);
            sb.Append("\n\nWhen you are ready to produce the final answer (after all tool calls), ")
              .Append("respond with a SINGLE JSON object (no markdown fences) matching this schema:\n")
              .Append(schema.ToString());
        }

        return sb.ToString();
    }

    private static string BuildMcpConfig(IAgentToolBridgeSession session)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WritePropertyName("mcpServers");
            w.WriteStartObject();
            w.WritePropertyName(session.ServerName);
            w.WriteStartObject();
            w.WriteString("type", "http");
            w.WriteString("url", session.Url.ToString());
            if (!string.IsNullOrEmpty(session.AuthToken))
            {
                w.WritePropertyName("headers");
                w.WriteStartObject();
                w.WriteString("Authorization", $"Bearer {session.AuthToken}");
                w.WriteEndObject();
            }
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private Dictionary<string, string?>? BuildEnvironment()
    {
        Dictionary<string, string?>? env = null;
        void Set(string key, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            (env ??= new Dictionary<string, string?>())[key] = value;
        }

        Set("CLAUDE_CODE_OAUTH_TOKEN", _options.Cli.OAuthToken);
        Set("ANTHROPIC_AUTH_TOKEN", _options.Cli.AuthToken);

        if (_options.Cli.AdditionalEnvironment is { } extra)
            foreach (var kv in extra)
                (env ??= new Dictionary<string, string?>())[kv.Key] = kv.Value;

        return env;
    }

    private static ClaudeCliResult ParseResult(ClaudeProcessResult process)
    {
        var stdout = process.StandardOutput.Trim();
        try
        {
            var parsed = JsonSerializer.Deserialize(stdout, AnthropicJsonContext.Default.ClaudeCliResult);
            if (parsed is not null) return parsed;
        }
        catch (JsonException)
        {
            // fall through
        }

        throw new InvalidOperationException(
            $"Could not parse Claude CLI agent JSON output. stdout: {Truncate(process.StandardOutput)} | stderr: {Truncate(process.StandardError)}");
    }

    private AgentTurn BuildTurn(ClaudeCliResult parsed, TimeSpan duration)
    {
        var u = parsed.Usage;
        var cacheRead = u?.CacheReadInputTokens ?? 0;
        var cacheWrite = u?.CacheCreationInputTokens ?? 0;
        // Mirror the API path: normalize InputTokens to the inclusive total so the
        // cost calculator (which subtracts the cache buckets) bills the uncached input.
        var totalInput = (u?.InputTokens ?? 0) + cacheRead + cacheWrite;
        var output = u?.OutputTokens ?? 0;

        var (inputCost, outputCost) = AiCostCalculator.CalculateCosts(_context.Llm, new TokenUsage
        {
            InputTokens = totalInput,
            OutputTokens = output,
            CachedTokens = cacheRead,
            CacheWriteTokens = cacheWrite,
        });

        return new AgentTurn
        {
            ToolCalls = Array.Empty<PendingToolCall>(),
            FinalText = parsed.Result ?? string.Empty,
            Usage = new TokenUsage
            {
                InputTokens = totalInput,
                OutputTokens = output,
                CachedTokens = cacheRead,
                CacheWriteTokens = cacheWrite,
                InputCost = inputCost,
                OutputCost = outputCost,
            },
            Duration = duration,
            RequestId = parsed.SessionId,
        };
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "(empty)";
        var trimmed = value.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500] + "…";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
