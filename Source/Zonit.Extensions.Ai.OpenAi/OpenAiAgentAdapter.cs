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
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
public sealed class OpenAiAgentAdapter : IAgentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<OpenAiOptions> _options;
    private readonly ILogger<OpenAiAgentAdapter> _logger;

    public OpenAiAgentAdapter(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiAgentAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool SupportsAgent(ILlm llm) => llm is OpenAiBase;

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    public IAgentSession BeginSession(AgentSessionContext context)
    {
        if (context.Llm is not OpenAiBase)
        {
            throw new InvalidOperationException(
                $"OpenAiAgentAdapter does not support model of type {context.Llm.GetType().FullName}.");
        }

        EnsureHttpClientConfigured();

        return new OpenAiAgentSession(_httpClient, context, _logger);
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
