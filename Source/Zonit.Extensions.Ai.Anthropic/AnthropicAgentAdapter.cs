using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// <see cref="IAgentProviderAdapter"/> implementation for Anthropic's Messages API.
/// </summary>
/// <remarks>
/// Unlike OpenAI's Responses API (server-side state via <c>previous_response_id</c>),
/// Anthropic's Messages API is stateless — the client must send the full message
/// history on every call. <see cref="AnthropicAgentSession"/> therefore maintains
/// a growing <c>messages</c> buffer client-side.
/// <para>
/// Claude returns <c>tool_use</c> content blocks when it wants to invoke a tool;
/// the client responds with a <c>user</c> message containing <c>tool_result</c>
/// content blocks — one per pending call, correlated by <c>tool_use_id</c>.
/// </para>
/// </remarks>
public sealed class AnthropicAgentAdapter : IAgentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AnthropicOptions> _options;
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly ILogger<AnthropicAgentAdapter> _logger;

    public AnthropicAgentAdapter(
        HttpClient httpClient,
        IOptions<AnthropicOptions> options,
        IOptions<AiOptions> aiOptions,
        ILogger<AnthropicAgentAdapter> logger)
    {
        _httpClient = httpClient;
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
    // returns drives the agent loop, whose final-response parse and tool
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

        EnsureHttpClientConfigured();
        return new AnthropicAgentSession(_httpClient, context, _aiOptions.Value.Resilience, _logger);
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

        // Opt-in to two betas (comma-separated; both no-ops when unused):
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
