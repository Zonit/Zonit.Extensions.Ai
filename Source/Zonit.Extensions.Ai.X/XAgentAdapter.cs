using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai.X;

/// <summary>
/// <see cref="IAgentProviderAdapter"/> implementation for xAI's OpenAI-compatible
/// Responses API. Each agent session POSTs sequentially to <c>/v1/responses</c>
/// and replays the full conversation as <c>input</c> (X does not yet support
/// <c>previous_response_id</c> chaining).
/// </summary>
public sealed class XAgentAdapter : IAgentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<XOptions> _options;
    private readonly ILogger<XAgentAdapter> _logger;

    public XAgentAdapter(
        HttpClient httpClient,
        IOptions<XOptions> options,
        ILogger<XAgentAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool SupportsAgent(ILlm llm) => llm is XChatBase;

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    public IAgentSession BeginSession(AgentSessionContext context)
    {
        if (context.Llm is not XChatBase)
        {
            throw new InvalidOperationException(
                $"XAgentAdapter does not support model of type {context.Llm.GetType().FullName}.");
        }

        EnsureHttpClientConfigured();
        return new XAgentSession(_httpClient, context, _logger);
    }

    private bool _configured;

    private void EnsureHttpClientConfigured()
    {
        if (_configured) return;
        _configured = true;

        var baseUrl = _options.Value.BaseUrl ?? "https://api.x.ai";
        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.Value.ApiKey)
            && _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.Value.ApiKey);
        }
    }
}
