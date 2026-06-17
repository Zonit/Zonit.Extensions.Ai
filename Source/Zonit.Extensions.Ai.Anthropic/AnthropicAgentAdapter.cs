using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// <see cref="IAgentProviderAdapter"/> for Anthropic. Routes an agent run to the right
/// session per <see cref="AnthropicOptions.Transport"/>:
/// <list type="bullet">
///   <item><description><c>Api</c> → <see cref="AnthropicAgentSession"/> over the HTTP Messages API.</description></item>
///   <item><description><c>Sdk</c>/<c>Auto</c> → <see cref="CliAgentSession"/> driving <c>claude -p</c>,
///   which owns the loop and calls this app's C# tools over the loopback MCP bridge
///   (<see cref="IAgentToolBridge"/>).</description></item>
/// </list>
/// </summary>
/// <remarks>
/// On the CLI transport, tool-using agents need <c>AddAiAgentToolBridge()</c> registered.
/// When the CLI is unavailable or the bridge is missing, <c>Auto</c> falls back to the HTTP
/// API (if an <see cref="AiProviderOptions.ApiKey"/> is set); <c>Sdk</c> throws.
/// <para>
/// The constructor takes <see cref="IServiceProvider"/> rather than the internal
/// <c>IClaudeCliRunner</c>/<see cref="IAgentToolBridge"/> directly because this type is
/// public and a public constructor may not expose less-accessible parameter types (CS0051).
/// </para>
/// </remarks>
public sealed class AnthropicAgentAdapter : IAgentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<AnthropicOptions> _options;
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly ILogger<AnthropicAgentAdapter> _logger;

    public AnthropicAgentAdapter(
        HttpClient httpClient,
        IServiceProvider serviceProvider,
        IOptions<AnthropicOptions> options,
        IOptions<AiOptions> aiOptions,
        ILogger<AnthropicAgentAdapter> logger)
    {
        _httpClient = httpClient;
        _serviceProvider = serviceProvider;
        _options = options;
        _aiOptions = aiOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool SupportsAgent(ILlm llm) => llm is AnthropicBase;

    /// <inheritdoc />
    // Matches the [RequiresUnreferencedCode]/[RequiresDynamicCode] on
    // IAgentProviderAdapter.BeginSession (mandatory per IL2046/IL3051 — the
    // annotations must match across interface implementations). The session it
    // returns drives the agent runner, whose final-response parse and tool
    // execution may use reflection; this factory itself performs none.
    [RequiresUnreferencedCode("Returned session drives the agent runner, whose final-response parse and tools may use reflection.")]
    [RequiresDynamicCode("Returned session drives the agent runner, whose final-response parse and tools may use reflection.")]
    public IAgentSession BeginSession(AgentSessionContext context)
    {
        if (context.Llm is not AnthropicBase)
        {
            throw new InvalidOperationException(
                $"AnthropicAgentAdapter does not support model of type {context.Llm.GetType().FullName}.");
        }

        var options = _options.Value;
        if (options.Transport == AnthropicTransport.Api)
            return NewApiSession(context);

        // Sdk / Auto: prefer the Claude Code CLI. The CLI owns the loop and executes
        // tools itself; the framework's C# tools are exposed to it over the loopback MCP
        // bridge. The CLI can serve this run iff the binary is found AND (no tools, or a
        // bridge is registered to host them).
        var hasTools = context.Tools.Count > 0;
        var hasApiKey = !string.IsNullOrEmpty(options.ApiKey);
        var bridge = _serviceProvider.GetService<IAgentToolBridge>();
        var cliAvailable = TryResolveCli(options);

        if (cliAvailable && (!hasTools || bridge is not null))
        {
            var runner = _serviceProvider.GetRequiredService<IClaudeCliRunner>();
            return new CliAgentSession(context, runner, bridge, options, _aiOptions.Value.Resilience, _logger);
        }

        if (options.Transport == AnthropicTransport.Auto && hasApiKey)
        {
            _logger.LogDebug(
                "Anthropic agent: CLI {Reason}; falling back to the HTTP API (Transport=Auto).",
                cliAvailable ? "has tools but no IAgentToolBridge is registered" : "executable was not found");
            return NewApiSession(context);
        }

        throw new NotSupportedException(
            BuildUnavailableMessage(options.Transport, cliAvailable, hasTools, bridge is not null));
    }

    private IAgentSession NewApiSession(AgentSessionContext context)
    {
        EnsureHttpClientConfigured();
        return new AnthropicAgentSession(_httpClient, context, _aiOptions.Value.Resilience, _logger);
    }

    private static bool TryResolveCli(AnthropicOptions options)
    {
        try
        {
            ClaudeCliLocator.Resolve(options.Cli.ExecutablePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildUnavailableMessage(AnthropicTransport transport, bool cliAvailable, bool hasTools, bool hasBridge)
    {
        var why = !cliAvailable
            ? "the Claude Code CLI ('claude') was not found — install it and run `claude login`, or set AnthropicCliOptions.ExecutablePath"
            : "the agent uses tools but no IAgentToolBridge is registered — install Zonit.Extensions.Ai.Mcp.Server and call AddAiAgentToolBridge()";
        var fix = transport == AnthropicTransport.Auto
            ? "Set AnthropicOptions.ApiKey to fall back to the HTTP API, or resolve the cause above."
            : "Use Transport=Auto with AnthropicOptions.ApiKey set to fall back to the HTTP API, or resolve the cause above.";
        return $"Cannot start an Anthropic agent on the {transport} transport: {why}. {fix}";
    }

    private bool _configured;

    private void EnsureHttpClientConfigured()
    {
        if (_configured) return;
        _configured = true;

        var baseUrl = _options.Value.BaseUrl ?? "https://api.anthropic.com";
        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(baseUrl);

        if (!_httpClient.DefaultRequestHeaders.Contains("anthropic-version"))
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        // Opt-in to betas (comma-separated; all no-ops when unused):
        //   • extended-cache-ttl-2025-04-11 — enables cache_control ttl="1h".
        //   • context-1m-2025-08-07 — unlocks the 1M-token context window on
        //     models that support it (Opus / Sonnet 4.6+); harmless otherwise.
        //   • fast-mode-2026-02-01 — lets a request set speed:"fast" (see IFast);
        //     inert without the speed field, so safe to send globally.
        if (!_httpClient.DefaultRequestHeaders.Contains("anthropic-beta"))
            _httpClient.DefaultRequestHeaders.Add("anthropic-beta", "extended-cache-ttl-2025-04-11,context-1m-2025-08-07,fast-mode-2026-02-01");

        if (!string.IsNullOrEmpty(_options.Value.ApiKey)
            && !_httpClient.DefaultRequestHeaders.Contains("x-api-key"))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.Value.ApiKey);
        }
    }
}
