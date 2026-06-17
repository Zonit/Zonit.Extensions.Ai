using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Anthropic;

namespace Zonit.Extensions;

/// <summary>
/// Dependency injection extensions for Anthropic Claude provider.
/// </summary>
/// <remarks>
/// Provides extension methods for registering Anthropic as an AI provider.
/// <para>
/// <b>Usage:</b>
/// <code>
/// // From appsettings.json configuration
/// services.AddAiAnthropic();
/// 
/// // With API key
/// services.AddAiAnthropic("sk-ant-your-api-key");
/// 
/// // With custom configuration
/// services.AddAiAnthropic(options =>
/// {
///     options.ApiKey = "sk-ant-...";
/// });
/// </code>
/// </para>
/// </remarks>
public static class AnthropicServiceCollectionExtensions
{
    /// <summary>
    /// Registers Anthropic provider with the specified API key.
    /// </summary>
    /// <remarks>
    /// Automatically registers core AI services if not already registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">Anthropic API key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAnthropic(
        this IServiceCollection services,
        string apiKey)
    {
        return services.AddAiAnthropic(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Registers Anthropic provider with optional configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuration is loaded from <c>appsettings.json</c> section <c>"Ai:Anthropic"</c>.
    /// The <paramref name="options"/> action is applied after configuration binding via <c>PostConfigure</c>.
    /// </para>
    /// <para>
    /// Automatically registers core AI services if not already registered.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration action for Anthropic options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAnthropic(
        this IServiceCollection services,
        Action<AnthropicOptions>? options = null)
    {
        // Skip if already registered (idempotent - safe for multiple plugin registrations)
        if (services.IsProviderRegistered<AnthropicProvider>())
            return services;

        // Ensure core AI services are registered
        services.AddAi();

        // Bind configuration from appsettings.json (AOT-safe).
        services.AddAiOptionsFromConfiguration<AnthropicOptions>(AnthropicOptions.SectionName);

        // Apply additional configuration via PostConfigure
        if (options is not null)
            services.PostConfigure(options);

        // API transport — typed HttpClient with AI resilience (40min timeout, retry,
        // circuit breaker). Also serves as the CLI transport's fallback for requests
        // the CLI cannot represent (image/PDF attachments, tools / agent loop).
        services.AddHttpClient<AnthropicApiTransport>()
            .AddAiResilienceHandler();

        // CLI (claude -p) transport + its process runner (stateless singleton).
        services.TryAddSingleton<IClaudeCliRunner, ClaudeCliProcess>();
        services.AddTransient<AnthropicCliTransport>();

        // The active transport is selected by AnthropicOptions.Transport. Resolved per
        // request so a PostConfigure that flips Transport before the first resolution
        // still takes effect.
        services.AddTransient<IAnthropicTransport>(sp =>
            sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.Transport == AnthropicTransport.Sdk
                ? sp.GetRequiredService<AnthropicCliTransport>()
                : sp.GetRequiredService<AnthropicApiTransport>());

        // Provider resolves IAnthropicTransport. Registered as a concrete service so
        // TryAddModelProvider's IModelProvider factory can resolve it.
        services.AddTransient<AnthropicProvider>();

        // Register as IModelProvider (idempotent).
        services.TryAddModelProvider<AnthropicProvider>();

        // Agent adapter — dedicated typed HttpClient with the STREAMING
        // resilience handler. The agent loop reads SSE events for the
        // entire turn duration; Polly's per-attempt timeout (10 min
        // default) would cancel healthy long-running streams the moment
        // the model crosses 10 min of thinking — surfacing as
        // "The operation was canceled" with duration pinned to exactly
        // 600 s. Stream liveness is enforced client-side by the inter-
        // event SSE watchdog and HTTP/2 keepalive PING frames.
        services.AddHttpClient<AnthropicAgentAdapter>()
            .AddAiStreamingResilienceHandler();
        services.AddTransient<IAgentProviderAdapter>(
            sp => sp.GetRequiredService<AnthropicAgentAdapter>());

        return services;
    }

    /// <summary>
    /// Registers the Anthropic provider on the <b>Claude Code CLI</b> transport
    /// (<c>claude -p</c>) instead of the HTTP API. Requests authenticate with the
    /// machine's <c>claude login</c> session — no API key required for plain
    /// text / chat / streaming / structured-output calls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Equivalent to <c>AddAiAnthropic(o =&gt; { o.Transport = AnthropicTransport.Sdk; … })</c>.
    /// </para>
    /// <para>
    /// Requests the CLI cannot represent (image/PDF attachments, function tools / the
    /// agent loop) fall back to the HTTP API when <see cref="AiProviderOptions.ApiKey"/>
    /// is set, otherwise they throw. Set <see cref="AnthropicCliOptions.ExecutablePath"/>
    /// when <c>claude</c> is not on PATH. The CLI binary is auto-discovered per OS.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional additional configuration (applied after the transport is set to SDK).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAnthropicSdk(
        this IServiceCollection services,
        Action<AnthropicOptions>? options = null)
    {
        return services.AddAiAnthropic(o =>
        {
            o.Transport = AnthropicTransport.Sdk;
            options?.Invoke(o);
        });
    }

    /// <summary>
    /// Alias for <see cref="AddAiAnthropicSdk(IServiceCollection, Action{AnthropicOptions})"/>
    /// — registers the Anthropic provider on the Claude Code CLI transport.
    /// </summary>
    public static IServiceCollection AddAiAnthropicCli(
        this IServiceCollection services,
        Action<AnthropicOptions>? options = null)
        => services.AddAiAnthropicSdk(options);
}
