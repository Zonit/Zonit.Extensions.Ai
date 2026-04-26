using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// <see cref="IAgentProviderAdapter"/> implementation for Google Gemini's
/// <c>generateContent</c> API. The session replays the entire conversation
/// (Gemini has no server-side state token).
/// </summary>
public sealed class GoogleAgentAdapter : IAgentProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<GoogleOptions> _options;
    private readonly ILogger<GoogleAgentAdapter> _logger;

    public GoogleAgentAdapter(
        HttpClient httpClient,
        IOptions<GoogleOptions> options,
        ILogger<GoogleAgentAdapter> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool SupportsAgent(ILlm llm) => llm is GoogleBase;

    /// <inheritdoc />
    [RequiresUnreferencedCode("JSON serialization might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON serialization might require runtime code generation.")]
    public IAgentSession BeginSession(AgentSessionContext context)
    {
        if (context.Llm is not GoogleBase)
        {
            throw new InvalidOperationException(
                $"GoogleAgentAdapter does not support model of type {context.Llm.GetType().FullName}.");
        }

        EnsureHttpClientConfigured();
        return new GoogleAgentSession(_httpClient, _options, context, _logger);
    }

    private bool _configured;

    private void EnsureHttpClientConfigured()
    {
        if (_configured) return;
        _configured = true;

        var baseUrl = _options.Value.BaseUrl ?? "https://generativelanguage.googleapis.com";
        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(baseUrl);
    }
}
