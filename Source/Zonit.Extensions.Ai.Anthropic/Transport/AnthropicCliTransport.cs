using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Claude Code CLI transport (<see cref="AnthropicTransport.Sdk"/>): runs
/// <c>claude -p</c> as a subprocess so requests use the machine's <c>claude login</c>
/// session instead of an API key. Translates the canonical
/// <see cref="AnthropicMessagesRequest"/> into a CLI invocation and synthesizes an
/// <see cref="AnthropicResponse"/> from the CLI's JSON output, so
/// <see cref="AnthropicProvider"/> parses the result through its normal path.
/// </summary>
/// <remarks>
/// <para>
/// What the CLI cannot represent — image/PDF attachments, function/server tools (the
/// agent loop) — is detected up front and transparently sent through
/// <see cref="AnthropicApiTransport"/> when an <see cref="AiProviderOptions.ApiKey"/> is
/// configured; without a key it throws <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// Prompt caching is automatic inside the CLI/SDK, so the request's
/// <c>cache_control</c> markers are simply ignored here.
/// </para>
/// </remarks>
internal sealed class AnthropicCliTransport : IAnthropicTransport
{
    private readonly IClaudeCliRunner _runner;
    private readonly AnthropicApiTransport _apiFallback;
    private readonly AnthropicOptions _options;
    private readonly TimeSpan _fallbackTimeout;
    private readonly ILogger<AnthropicCliTransport> _logger;

    public AnthropicCliTransport(
        IClaudeCliRunner runner,
        AnthropicApiTransport apiFallback,
        IOptions<AnthropicOptions> options,
        IOptions<AiOptions> aiOptions,
        ILogger<AnthropicCliTransport> logger)
    {
        _runner = runner;
        _apiFallback = apiFallback;
        _options = options.Value;
        _fallbackTimeout = aiOptions.Value.Resilience.TotalRequestTimeout;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AnthropicResponse> SendAsync(
        ILlm llm,
        AnthropicMessagesRequest request,
        string operation,
        CancellationToken cancellationToken)
    {
        if (TryGetUnsupportedReason(request) is { } reason)
            return await FallbackOrThrowAsync(reason, () => _apiFallback.SendAsync(llm, request, operation, cancellationToken));

        var invocation = BuildInvocation(request, streaming: false);
        _logger.LogDebug("Claude CLI {Operation}: {Exe} (model {Model})", operation, invocation.ExecutablePath, request.Model);

        var process = await _runner.RunAsync(invocation, cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(BuildCliFailureMessage(llm, process));

        var parsed = ParseJsonResult(process);
        if (parsed.IsError || parsed.Type == "error")
            throw new InvalidOperationException(
                $"Claude CLI for model '{llm.Name}' returned an error result: {parsed.Result ?? parsed.Subtype ?? "(no detail)"}");

        var text = parsed.Result;
        if (string.IsNullOrEmpty(text))
            throw AnthropicProvider.BuildEmptyResponseError(_logger, operation, llm, stopReason: null, requestId: parsed.SessionId);

        return new AnthropicResponse
        {
            Id = parsed.SessionId is { Length: > 0 } sid ? sid : "cli",
            StopReason = "end_turn",
            Content = [new AnthropicContent { Type = "text", Text = text }],
            Usage = parsed.Usage is { } u
                ? new AnthropicUsage
                {
                    InputTokens = u.InputTokens,
                    OutputTokens = u.OutputTokens,
                    CacheReadInputTokens = u.CacheReadInputTokens,
                    CacheCreationInputTokens = u.CacheCreationInputTokens,
                }
                : null,
        };
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamAsync(
        ILlm llm,
        AnthropicMessagesRequest request,
        string operation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (TryGetUnsupportedReason(request) is { } reason)
        {
            await foreach (var delta in FallbackStreamOrThrow(reason, llm, request, operation, cancellationToken))
                yield return delta;
            yield break;
        }

        var invocation = BuildInvocation(request, streaming: true);
        _logger.LogDebug("Claude CLI {Operation} (stream): {Exe} (model {Model})", operation, invocation.ExecutablePath, request.Model);

        var emittedAny = false;
        string? terminalResult = null;
        var errored = false;

        await foreach (var line in _runner.StreamLinesAsync(invocation, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            ClaudeCliStreamLine? evt;
            try { evt = JsonSerializer.Deserialize(line, AnthropicJsonContext.Default.ClaudeCliStreamLine); }
            catch (JsonException) { continue; } // tolerate non-JSON diagnostic lines

            if (evt is null) continue;

            switch (evt.Type)
            {
                case "assistant" when evt.Message?.Content is { } blocks:
                    foreach (var block in blocks)
                    {
                        if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
                        {
                            emittedAny = true;
                            yield return block.Text;
                        }
                    }
                    break;

                case "result":
                    terminalResult = evt.Result;
                    errored = evt.IsError;
                    break;
            }
        }

        if (errored)
            throw new InvalidOperationException(
                $"Claude CLI stream for model '{llm.Name}' ended with an error: {terminalResult ?? "(no detail)"}");

        // Some CLI builds emit the answer only on the terminal `result` line (no
        // incremental `assistant` lines). Fall back to it so a valid answer is not lost.
        if (!emittedAny && !string.IsNullOrEmpty(terminalResult))
        {
            emittedAny = true;
            yield return terminalResult;
        }

        if (!emittedAny && !cancellationToken.IsCancellationRequested)
            throw AnthropicProvider.BuildEmptyResponseError(_logger, operation, llm, stopReason: null, requestId: null);
    }

    // ---- request → invocation -------------------------------------------------

    private ClaudeCliInvocation BuildInvocation(AnthropicMessagesRequest request, bool streaming)
    {
        var args = new List<string>
        {
            "--print",
            BuildPromptText(request),
            "--output-format", streaming ? "stream-json" : "json",
        };
        if (streaming)
            args.Add("--verbose"); // required by the CLI for stream-json output

        args.Add("--model");
        args.Add(request.Model);

        var system = BuildSystemPrompt(request);
        if (!string.IsNullOrEmpty(system))
        {
            args.Add("--append-system-prompt");
            args.Add(system);
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
            Timeout = _options.Cli.Timeout ?? _options.Timeout ?? _fallbackTimeout,
        };
    }

    /// <summary>
    /// Flattens the message history into a single prompt. A lone user turn is sent
    /// verbatim; multi-turn history becomes a role-labelled transcript (the CLI takes
    /// one prompt, so prior turns are folded in as context).
    /// </summary>
    private static string BuildPromptText(AnthropicMessagesRequest request)
    {
        if (request.Messages.Count == 1 && request.Messages[0].Role == "user")
            return ExtractText(request.Messages[0]);

        var sb = new StringBuilder();
        foreach (var message in request.Messages)
        {
            var text = ExtractText(message);
            if (text.Length == 0) continue;
            var label = message.Role switch
            {
                "assistant" => "Assistant",
                "user" => "User",
                _ => message.Role,
            };
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(label).Append(": ").Append(text);
        }
        return sb.ToString();
    }

    private static string ExtractText(AnthropicMessageItem message)
    {
        var sb = new StringBuilder();
        foreach (var block in message.Content)
        {
            string? piece = block.Type switch
            {
                "text" => block.Text,
                "tool_result" => block.Content,
                _ => null,
            };
            if (string.IsNullOrEmpty(piece)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(piece);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Builds the <c>--append-system-prompt</c> text: the request's system blocks, plus
    /// — for structured output — an instruction carrying the JSON Schema (the CLI cannot
    /// use the synthetic <c>respond_json</c> tool, so the schema is enforced by prompt).
    /// </summary>
    private static string BuildSystemPrompt(AnthropicMessagesRequest request)
    {
        var sb = new StringBuilder();
        if (request.System is { } sys)
        {
            foreach (var block in sys)
            {
                if (string.IsNullOrEmpty(block.Text)) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(block.Text);
            }
        }

        var schema = request.Tools?
            .FirstOrDefault(t => t.Name == AnthropicProvider.StructuredToolName)?
            .InputSchema;
        if (schema is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } el)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append("Respond with ONLY a single JSON object that conforms to this JSON Schema. ")
              .Append("Do not include any prose, explanation, or markdown code fences.\n")
              .Append(el.GetRawText());
        }

        return sb.ToString();
    }

    private Dictionary<string, string?>? BuildEnvironment()
    {
        // Ambient environment is always inherited (so `claude login` works). Layer on
        // explicit credentials only when configured — never auto-inject ApiKey, which
        // belongs to the HTTP fallback transport, not the subscription-based CLI.
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

    // ---- response parsing -----------------------------------------------------

    private static ClaudeCliResult ParseJsonResult(ClaudeProcessResult process)
    {
        var stdout = process.StandardOutput.Trim();
        try
        {
            var parsed = JsonSerializer.Deserialize(stdout, AnthropicJsonContext.Default.ClaudeCliResult);
            if (parsed is not null) return parsed;
        }
        catch (JsonException)
        {
            // fall through to the shared failure below
        }

        throw new InvalidOperationException(
            "Could not parse Claude CLI JSON output. " +
            $"stdout: {Truncate(process.StandardOutput)} | stderr: {Truncate(process.StandardError)}");
    }

    private static string BuildCliFailureMessage(ILlm llm, ClaudeProcessResult process)
        => $"Claude CLI for model '{llm.Name}' exited with code {process.ExitCode}. " +
           $"stderr: {Truncate(process.StandardError)} | stdout: {Truncate(process.StandardOutput)}";

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "(empty)";
        var trimmed = value.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500] + "…";
    }

    // ---- unsupported detection + fallback -------------------------------------

    /// <summary>
    /// Returns a human-readable reason when the request uses features the CLI transport
    /// cannot represent (so the caller routes to the API), or <c>null</c> when the CLI
    /// can handle it. Supported: plain text (with optional structured output). Not
    /// supported: image/document attachments, and any tool other than the synthetic
    /// <c>respond_json</c> structured-output tool.
    /// </summary>
    private static string? TryGetUnsupportedReason(AnthropicMessagesRequest request)
    {
        foreach (var message in request.Messages)
            foreach (var block in message.Content)
                if (block.Type is "image" or "document")
                    return $"a '{block.Type}' attachment";

        if (request.Tools is { } tools)
            foreach (var tool in tools)
                if (tool.Name != AnthropicProvider.StructuredToolName)
                    return $"tool '{tool.Name}'";

        return null;
    }

    private async Task<AnthropicResponse> FallbackOrThrowAsync(string reason, Func<Task<AnthropicResponse>> apiCall)
    {
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogDebug("Claude CLI cannot send {Reason}; falling back to the HTTP API transport.", reason);
            return await apiCall();
        }

        throw UnsupportedException(reason);
    }

    private async IAsyncEnumerable<string> FallbackStreamOrThrow(
        string reason,
        ILlm llm,
        AnthropicMessagesRequest request,
        string operation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
            throw UnsupportedException(reason);

        _logger.LogDebug("Claude CLI cannot stream {Reason}; falling back to the HTTP API transport.", reason);
        await foreach (var delta in _apiFallback.StreamAsync(llm, request, operation, cancellationToken))
            yield return delta;
    }

    private static NotSupportedException UnsupportedException(string reason)
        => new(
            $"The Claude Code CLI transport cannot send {reason}. " +
            "Configure AnthropicOptions.ApiKey to allow automatic fallback to the HTTP API for such requests, " +
            "or remove the unsupported content/tools.");
}
