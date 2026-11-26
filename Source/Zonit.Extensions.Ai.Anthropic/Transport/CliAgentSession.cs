using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
/// <para>
/// The run is read as <c>stream-json</c>, so the terminal turn reports what happened inside it:
/// <see cref="AgentTurn.CompletedToolCalls"/> (every tool call, with input, result and duration)
/// and <see cref="AgentTurn.Iterations"/> (model round-trips). The runner's iteration budget is
/// passed as <c>--max-turns</c>; a run that hits it fails with
/// <see cref="AgentIterationLimitException"/>, as on the HTTP path.
/// </para>
/// <para>
/// Still CLI-owned: the framework's <c>ToolExecutor</c> does not run here, so
/// <c>MaxParallelToolCalls</c>, <c>OnToolCall</c>, the per-tool timeout and nested-usage tracking
/// do not apply; usage is taken from the CLI's reported token counts.
/// </para>
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
        using var scratch = new ClaudeCliScratch();
        try
        {
            if (tools.Count > 0)
            {
                if (_bridge is null)
                    throw new NotSupportedException(
                        "Agent has tools but no IAgentToolBridge is registered — the Claude Code CLI cannot call them. " +
                        "Install Zonit.Extensions.Ai.Mcp.Server and call AddAiAgentToolBridge().");

                // The CLI invokes tools over the bridge through the context-less ITool path, so
                // contextual tools (ToolBase<TInput,TOutput> reading IRunContext) and sub-agent tools
                // must have their trusted context (and seeded chat) bound here — the in-process
                // ToolExecutor cannot run on this transport.
                var bridgeTools = AgentToolContextBinder.Bind(tools, _context.Context, _context.InitialChat);
                bridgeSession = await _bridge.StartAsync(bridgeTools, cancellationToken).ConfigureAwait(false);
            }

            var invocation = BuildInvocation(bridgeSession, scratch);
            _logger.LogDebug(
                "Claude CLI agent run: {Exe} (model {Model}, tools {ToolCount}, max turns {MaxTurns})",
                invocation.ExecutablePath, _context.Llm.Name, tools.Count, _context.MaxIterations);

            var log = new ClaudeCliRunLog(bridgeSession?.ServerName);
            var sw = Stopwatch.StartNew();
            try
            {
                await foreach (var line in _runner.StreamLinesAsync(invocation, cancellationToken).ConfigureAwait(false))
                    log.Accept(line, sw.Elapsed);
            }
            catch (InvalidOperationException) when (log.Result is not null)
            {
                // The CLI exits non-zero after it reported a failed run (e.g. error_max_turns);
                // the result line already says why, and is handled below.
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Claude CLI agent run for model '{_context.Llm.Name}' failed: {ex.Message}", ex);
            }
            sw.Stop();

            var result = log.Result
                ?? throw new InvalidOperationException(
                    $"Claude CLI agent run for model '{_context.Llm.Name}' ended without a result.");

            var toolCalls = log.ToolCalls();
            var iterations = log.Iterations > 0 ? log.Iterations : Math.Max(result.NumTurns ?? 1, 1);
            var usage = BuildUsage(result);

            if (result.Subtype == "error_max_turns")
            {
                var limit = _context.MaxIterations ?? iterations;
                throw new AgentIterationLimitException(limit, new AgentPartialResult
                {
                    Iterations = iterations,
                    ToolCalls = toolCalls,
                    TotalUsage = usage,
                    TotalCost = usage.InputCost + usage.OutputCost,
                });
            }

            if (result.IsError || result.Type == "error")
                throw new InvalidOperationException(
                    $"Claude CLI agent run for model '{_context.Llm.Name}' returned an error: {result.Result ?? result.Subtype ?? "(no detail)"}");

            _finalTurn = new AgentTurn
            {
                ToolCalls = Array.Empty<PendingToolCall>(),
                FinalText = result.Result ?? string.Empty,
                Usage = usage,
                Duration = sw.Elapsed,
                RequestId = result.SessionId,
                CompletedToolCalls = toolCalls,
                Iterations = iterations,
            };
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
    private ClaudeCliInvocation BuildInvocation(IAgentToolBridgeSession? bridgeSession, ClaudeCliScratch scratch)
    {
        var cli = _options.Cli;
        var (systemText, prompt) = BuildPrompt();

        var args = new List<string>
        {
            "--print",
            "--output-format", "stream-json",
            "--verbose", // required by the CLI for stream-json output
            "--model", _context.Llm.Name,
        };

        // The runner's iteration budget, so MaxIterations means the same on both transports.
        if (_context.MaxIterations is int maxTurns and > 0)
        {
            args.Add("--max-turns");
            args.Add(maxTurns.ToString(CultureInfo.InvariantCulture));
        }

        ClaudeCliCommand.AddSystemPrompt(args, scratch, systemText, cli);
        ClaudeCliCommand.AddIsolation(args, cli);

        if (bridgeSession is not null)
        {
            // Passed as a file, not inline JSON: on Windows an npm `claude.cmd` shim runs through
            // cmd.exe, which mangles the quotes and braces of an inline {"mcpServers":…} argument.
            args.Add("--mcp-config");
            args.Add(scratch.WriteFile("mcp-config.json", BuildMcpConfig(bridgeSession)));

            // Pre-approve our bridge's tools so the CLI runs them unattended.
            foreach (var name in bridgeSession.ToolNames)
            {
                args.Add("--allowedTools");
                args.Add($"mcp__{bridgeSession.ServerName}__{name}");
            }
        }

        if (!string.IsNullOrWhiteSpace(cli.PermissionMode))
        {
            args.Add("--permission-mode");
            args.Add(cli.PermissionMode);
        }

        if (cli.AdditionalArguments is { Length: > 0 } extra)
            args.AddRange(extra);

        return new ClaudeCliInvocation
        {
            ExecutablePath = ClaudeCliLocator.Resolve(cli.ExecutablePath),
            Arguments = args,
            StandardInput = prompt,
            WorkingDirectory = ClaudeCliCommand.WorkingDirectory(cli, scratch),
            EnvironmentOverrides = ClaudeCliCommand.BuildEnvironment(cli),
            Timeout = cli.Timeout ?? _options.Timeout ?? _resilience.TotalRequestTimeout,
        };
    }

    /// <summary>
    /// Splits the run into system text and the prompt sent on stdin, the way the HTTP agent session
    /// builds its request: a run seeded with a conversation uses <c>Prompt.Text</c> as the system
    /// instruction and the conversation (flattened to a transcript) as the prompt; a standalone run
    /// sends <c>Prompt.Text</c> as the prompt with no system text. A JSON-schema directive follows
    /// the prompt when structured output is expected.
    /// </summary>
    [RequiresUnreferencedCode("AiSchemaRegistry.GetSchema may use reflection over the response type.")]
    [RequiresDynamicCode("AiSchemaRegistry.GetSchema may use reflection over the response type.")]
    private (string? SystemText, string Prompt) BuildPrompt()
    {
        var transcript = new StringBuilder();
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
                if (transcript.Length > 0) transcript.Append("\n\n");
                transcript.Append(label).Append(": ").Append(text);
            }
        }

        string? systemText = null;
        var prompt = new StringBuilder();
        if (transcript.Length > 0 && !string.IsNullOrEmpty(_context.Prompt.Text))
        {
            systemText = _context.Prompt.Text;
            prompt.Append(transcript);
        }
        else
        {
            prompt.Append(transcript);
            if (prompt.Length > 0) prompt.Append("\n\n");
            prompt.Append(_context.Prompt.Text);
        }

        if (_context.ResponseType is { } responseType)
        {
            var schema = AiSchemaRegistry.GetSchema(responseType);
            prompt.Append("\n\nWhen you are ready to produce the final answer (after all tool calls), ")
                  .Append("respond with a SINGLE JSON object (no markdown fences) matching this schema:\n")
                  .Append(schema.ToString());
        }

        return (systemText, prompt.ToString());
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

    private TokenUsage BuildUsage(ClaudeCliResult result)
    {
        var u = result.Usage;
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

        return new TokenUsage
        {
            InputTokens = totalInput,
            OutputTokens = output,
            CachedTokens = cacheRead,
            CacheWriteTokens = cacheWrite,
            InputCost = inputCost,
            OutputCost = outputCost,
        };
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
