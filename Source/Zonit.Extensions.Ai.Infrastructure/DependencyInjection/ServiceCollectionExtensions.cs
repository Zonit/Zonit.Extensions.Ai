using System.Net.Http.Headers;
using System.Text;
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
        // Configure OpenAI repositories with shared configuration
        services.AddOpenAiRepositories();
        
        // Configure X repository
        services.AddXRepository();

        return services;
    }

    private static IServiceCollection AddOpenAiRepositories(this IServiceCollection services)
    {
        // Shared OpenAI HttpClient configuration
        static void ConfigureOpenAiClient(IServiceProvider serviceProvider, HttpClient client)
        {
            var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
            client.BaseAddress = new Uri("https://api.openai.com/");
            client.Timeout = options.Value.Resilience.HttpClientTimeout;
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", options.Value.OpenAiKey);
            
            // Add proper UTF-8 headers
            client.DefaultRequestHeaders.AcceptCharset.Clear();
            client.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json") 
            { 
                CharSet = "utf-8" 
            });
        }

        // Add OpenAI text repository
        services
            .AddHttpClient<OpenAiRepository>(ConfigureOpenAiClient)
            .AddStandardResilienceHandler()
            .Configure(ConfigureResilienceFromOptions);

        services.AddKeyedTransient<ITextRepository>("OpenAi", (serviceProvider, _) =>
            serviceProvider.GetRequiredService<OpenAiRepository>());

        // Add OpenAI image repository
        services
            .AddHttpClient<OpenAiImageRepository>(ConfigureOpenAiClient)
            .AddStandardResilienceHandler()
            .Configure(ConfigureResilienceFromOptions);

        services.AddKeyedTransient<IImageRepository>("OpenAi", (serviceProvider, _) =>
            serviceProvider.GetRequiredService<OpenAiImageRepository>());

        return services;
    }

    private static IServiceCollection AddXRepository(this IServiceCollection services)
    {
        services
            .AddHttpClient<XRepository>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
                client.BaseAddress = new Uri("https://api.x.ai/");
                client.Timeout = options.Value.Resilience.HttpClientTimeout;
                
                // Add proper UTF-8 headers for X AI as well
                client.DefaultRequestHeaders.AcceptCharset.Clear();
                client.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json") 
                { 
                    CharSet = "utf-8" 
                });
            })
            .AddStandardResilienceHandler()
            .Configure(ConfigureResilienceFromOptions);

        services.AddKeyedTransient<ITextRepository>("X", (serviceProvider, _) =>
            serviceProvider.GetRequiredService<XRepository>());

        return services;
    }

    /// <summary>
    /// Configures resilience settings from AiOptions
    /// </summary>
    private static void ConfigureResilienceFromOptions(HttpStandardResilienceOptions options, IServiceProvider serviceProvider)
    {
        var aiOptions = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
        ConfigureResilience(options, aiOptions.Resilience);
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