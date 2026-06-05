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
    // Matches the [RequiresUnreferencedCode]/[RequiresDynamicCode] on
    // IAgentProviderAdapter.BeginSession (mandatory per IL2046/IL3051 — the
    // annotations must match across interface implementations). The session it
    // returns drives the agent loop, whose final-response parse and tool
    // execution may use reflection; this factory itself performs none.
    [RequiresUnreferencedCode("Returned session drives the agent runner, whose final-response parse and tools may use reflection.")]
    [RequiresDynamicCode("Returned session drives the agent runner, whose final-response parse and tools may use reflection.")]
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
