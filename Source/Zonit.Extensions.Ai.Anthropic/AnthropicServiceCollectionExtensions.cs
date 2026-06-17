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
/// The transport defaults to <see cref="AnthropicTransport.Api"/> (HTTP Messages API,
/// API-key auth). The HTTP API and the local Claude Code CLI are <b>not</b> behaviourally
/// identical (the CLI runs through Claude Code, which has its own system prompt), so the
/// non-API transport must be chosen <b>explicitly</b> — pass it as the first argument
/// rather than flipping a flag inside the options lambda. See <c>Instruction/sdk.md</c>.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// // HTTP API (default) — key from appsettings.json "Ai:Anthropic", inline, or options:
/// services.AddAiAnthropic();
/// services.AddAiAnthropic("sk-ant-your-api-key");
/// services.AddAiAnthropic(o => o.ApiKey = "sk-ant-...");
///
/// // Explicit transport (first argument), options second:
/// services.AddAiAnthropic(AnthropicTransport.Sdk);                     // local claude -p
/// services.AddAiAnthropic(AnthropicTransport.Auto, o => o.ApiKey = "sk-ant-..."); // CLI, API fallback
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
    /// Registers the Anthropic provider on an <b>explicit transport</b> — the recommended
    /// way to choose between the HTTP API and the local Claude Code CLI. The two are not
    /// behaviourally identical (the CLI runs through Claude Code, which has its own system
    /// prompt), so the choice is made visibly here rather than hidden in the options lambda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AnthropicTransport.Api"/> — HTTP Messages API (needs <c>ApiKey</c>).
    /// <see cref="AnthropicTransport.Sdk"/> — local <c>claude -p</c> only (subscription auth);
    /// throws if the CLI is unavailable. <see cref="AnthropicTransport.Auto"/> — prefer the
    /// CLI, fall back to the HTTP API for what it cannot do (needs <c>ApiKey</c>).
    /// </para>
    /// <para>
    /// The <paramref name="transport"/> argument is authoritative — it is applied after
    /// <paramref name="configure"/> and overrides any <c>Transport</c> set there or in
    /// configuration. Use <paramref name="configure"/> for the key, CLI options, base URL, etc.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="transport">Which transport carries requests.</param>
    /// <param name="configure">Optional configuration for keys, CLI options, base URL, etc.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAiAnthropic(
        this IServiceCollection services,
        AnthropicTransport transport,
        Action<AnthropicOptions>? configure = null)
    {
        return services.AddAiAnthropic(o =>
        {
            configure?.Invoke(o);
            o.Transport = transport;   // explicit argument wins over configure / configuration
        });
    }

    /// <summary>
    /// Registers Anthropic provider with optional configuration. The transport defaults to
    /// <see cref="AnthropicTransport.Api"/> unless overridden in configuration
    /// (<c>"Ai:Anthropic:Transport"</c>) or via the explicit-transport overload.
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
            sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.Transport == AnthropicTransport.Api
                ? sp.GetRequiredService<AnthropicApiTransport>()
                : sp.GetRequiredService<AnthropicCliTransport>());

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
}
