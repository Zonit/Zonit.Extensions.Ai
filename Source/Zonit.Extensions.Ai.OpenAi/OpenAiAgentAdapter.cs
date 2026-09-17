using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai.OpenAi;

/// <summary>
/// <see cref="IAgentProviderAdapter"/> implementation for OpenAI's
/// Responses API. Each agent session issues sequential <c>POST /v1/responses</c>
/// calls, linking turns via <c>previous_response_id</c> so OpenAI keeps the
/// conversation state server-side.
/// </summary>
/// <remarks>
/// Supports both <see cref="OpenAiChatBase"/> (temperature / top_p) and
/// <see cref="OpenAiReasoningBase"/> (reasoning effort, verbosity). Custom
/// tools (<c>ITool</c>) are exposed as Responses API <c>function</c> tools;
/// provider-native tools (<c>WebSearchTool</c>, <c>FileSearchTool</c>,
/// <c>CodeInterpreterTool</c>) configured on <see cref="ILlm.Tools"/> are
/// passed through unchanged.
/// </remarks>
public sealed class OpenAiAgentAdapter : IAgentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<OpenAiOptions> _options;
    private readonly ILogger<OpenAiAgentAdapter> _logger;
    private readonly TimeSpan _interEventTimeout;

    /// <param name="httpClient">Typed client carrying the streaming resilience pipeline.</param>
    /// <param name="options">Provider options (key, organization, base URL).</param>
    /// <param name="logger">Adapter logger.</param>
    /// <param name="aiOptions">
    /// Global AI options, for the stream watchdog. Optional so the constructor stays
    /// source-compatible; DI always supplies it (<c>AddAi()</c> registers it).
    /// </param>
    public OpenAiAgentAdapter(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiAgentAdapter> logger,
        IOptions<AiOptions>? aiOptions = null)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _interEventTimeout = aiOptions?.Value.Resilience.InterEventTimeout ?? TimeSpan.Zero;
    }

    /// <inheritdoc />
    public bool SupportsAgent(ILlm llm) => llm is OpenAiBase;

    /// <inheritdoc />
    // RUC/RDC are required to match IAgentProviderAdapter.BeginSession (annotated in the
    // abstraction; enforced by IL2046/IL3051). The agent loop this starts parses the final
    // TResponse and may invoke reflection-based tools — the request build itself is AOT-safe.
    [RequiresUnreferencedCode("The agent loop started here parses the final TResponse and may invoke reflection-based tools.")]
    [RequiresDynamicCode("The agent loop started here may run reflection-based tool (de)serialization.")]
    public IAgentSession BeginSession(AgentSessionContext context)
    {
        if (context.Llm is not OpenAiBase)
        {
            throw new InvalidOperationException(
                $"OpenAiAgentAdapter does not support model of type {context.Llm.GetType().FullName}.");
        }

        EnsureHttpClientConfigured();

        return new OpenAiAgentSession(_httpClient, context, _logger, _interEventTimeout);
    }

    private bool _configured;

    private void EnsureHttpClientConfigured()
    {
        if (_configured) return;
        _configured = true;

        var baseUrl = _options.Value.BaseUrl ?? "https://api.openai.com";
        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(baseUrl);

        if (!string.IsNullOrEmpty(_options.Value.ApiKey)
            && _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.Value.ApiKey);
        }

        if (!string.IsNullOrEmpty(_options.Value.OrganizationId)
            && !_httpClient.DefaultRequestHeaders.Contains("OpenAI-Organization"))
        {
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", _options.Value.OrganizationId);
        }
    }
}
