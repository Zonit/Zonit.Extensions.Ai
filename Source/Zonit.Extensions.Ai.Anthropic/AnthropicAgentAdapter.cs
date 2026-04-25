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
    private readonly ILogger<AnthropicAgentAdapter> _logger;

    public AnthropicAgentAdapter(
        HttpClient httpClient,
        IOptions<AnthropicOptions> options,
        ILogger<AnthropicAgentAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool SupportsAgent(ILlm llm) => llm is AnthropicBase;

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    public IAgentSession BeginSession(AgentSessionContext context)
    {
        if (context.Llm is not AnthropicBase)
        {
            throw new InvalidOperationException(
                $"AnthropicAgentAdapter does not support model of type {context.Llm.GetType().FullName}.");
        }

        EnsureHttpClientConfigured();
        return new AnthropicAgentSession(_httpClient, context, _logger);
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

        if (!string.IsNullOrEmpty(_options.Value.ApiKey)
            && !_httpClient.DefaultRequestHeaders.Contains("x-api-key"))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.Value.ApiKey);
        }
    }
}
