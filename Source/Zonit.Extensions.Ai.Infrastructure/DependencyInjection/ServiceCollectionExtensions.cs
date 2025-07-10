using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;
using Zonit.Extensions.Ai.Infrastructure.Repositories.X;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiInfrastructureExtension(this IServiceCollection services)
    {
        // Add HttpClient for OpenAiRepository
        services
            .AddHttpClient<OpenAiRepository>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
                client.BaseAddress = new Uri("https://api.openai.com/");
                client.Timeout = options.Value.Resilience.HttpClientTimeout;

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.Value.OpenAiKey);
            })
            .AddStandardResilienceHandler()
            .Configure((options, serviceProvider) =>
            {
                var aiOptions = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
                ConfigureResilience(options, aiOptions.Resilience);
            });

        services.AddKeyedTransient<ITextRepository>("OpenAi", (serviceProvider, key) =>
            serviceProvider.GetRequiredService<OpenAiRepository>());

        services.AddHttpClient();

        services
            .AddHttpClient<OpenAiImageRepository>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
                client.BaseAddress = new Uri("https://api.openai.com/");
                client.Timeout = options.Value.Resilience.HttpClientTimeout;

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.Value.OpenAiKey);
            })
            .AddStandardResilienceHandler()
            .Configure((options, serviceProvider) =>
            {
                var aiOptions = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
                ConfigureResilience(options, aiOptions.Resilience);
            });

        services.AddKeyedTransient<IImageRepository>("OpenAi", (serviceProvider, key) =>
            serviceProvider.GetRequiredService<OpenAiImageRepository>());

        // Add HttpClient for XRepository - now uses same configuration as other providers
        services
            .AddHttpClient<XRepository>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
                client.BaseAddress = new Uri("https://api.x.ai/");
                client.Timeout = options.Value.Resilience.HttpClientTimeout;
            })
            .AddStandardResilienceHandler()
            .Configure((options, serviceProvider) =>
            {
                var aiOptions = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
                ConfigureResilience(options, aiOptions.Resilience);
            });

        services.AddKeyedTransient<ITextRepository>("X", (serviceProvider, key) =>
            serviceProvider.GetRequiredService<XRepository>());

        return services;
    }

    /// <summary>
    /// Configures standardized resilience settings for all AI providers using modern .NET Resilience
    /// </summary>
    private static void ConfigureResilience(HttpStandardResilienceOptions options, ResilienceOptions resilienceConfig)
    {
        // Configure timeouts
        options.TotalRequestTimeout.Timeout = resilienceConfig.TotalRequestTimeout;
        options.AttemptTimeout.Timeout = resilienceConfig.AttemptTimeout;

        // Configure retry policy with exponential backoff and jitter
        options.Retry.MaxRetryAttempts = resilienceConfig.Retry.MaxRetryAttempts;
        options.Retry.Delay = resilienceConfig.Retry.BaseDelay;
        options.Retry.MaxDelay = resilienceConfig.Retry.MaxDelay;
        options.Retry.UseJitter = resilienceConfig.Retry.UseJitter;

        // Configure circuit breaker
        options.CircuitBreaker.FailureRatio = resilienceConfig.CircuitBreaker.FailureRatio;
        options.CircuitBreaker.MinimumThroughput = resilienceConfig.CircuitBreaker.MinimumThroughput;
        options.CircuitBreaker.SamplingDuration = resilienceConfig.CircuitBreaker.SamplingDuration;
        options.CircuitBreaker.BreakDuration = resilienceConfig.CircuitBreaker.BreakDuration;
    }
}