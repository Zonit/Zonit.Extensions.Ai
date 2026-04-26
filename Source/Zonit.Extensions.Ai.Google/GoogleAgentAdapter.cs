using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Zonit.Extensions.Ai.Google;

/// <summary>
/// <see cref="IAgentProviderAdapter"/> implementation for Google Gemini's
/// <c>generateContent</c> API. The session replays the entire conversation
/// (Gemini has no server-side state token).
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; the [DAM(PublicProperties)] propagation on TResponse preserves required members. Reflection fallback only fires when the source generator is disabled.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Internal pipeline routes user TResponse through source-generated JsonTypeInfo<T>; reflection paths only fire when the source generator is disabled.")]
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
