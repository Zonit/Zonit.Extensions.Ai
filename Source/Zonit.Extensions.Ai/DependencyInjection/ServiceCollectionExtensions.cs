using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using System.Net.Http.Headers;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Abstractions.Options;
using Zonit.Extensions.Ai.Services.OpenAi;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiExtension(this IServiceCollection services, Action<AiOptions>? options = null)
    {
        services.AddOptions<AiOptions>()
            .Configure<IConfiguration>(
                (options, configuration) =>
                    configuration.GetSection("Ai").Bind(options));

        if (options is not null)
            services.PostConfigure(options);

        services.AddTransient<IImageClient, ImageService>();

        services
            .AddTransient<OpenAiImageService>()
            .AddHttpClient<OpenAiImageService>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
                client.BaseAddress = new Uri("https://api.openai.com/");
                client.Timeout = TimeSpan.FromMinutes(10);

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.Value.OpenAiKey);
            })
            .AddResilienceHandler("OpenAiImageService-standard", builder =>
            {
                builder
                    .AddTimeout(TimeSpan.FromMinutes(5))
                    .AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(5)
                    })
                    .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                    {
                        FailureRatio = 0.5,
                        MinimumThroughput = 10,
                        SamplingDuration = TimeSpan.FromMinutes(10),
                        BreakDuration = TimeSpan.FromSeconds(15)
                    });
            });
        //.AddHttpClient<OpenAiImageService>((serviceProvider, client) =>
        //{
        //    var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
        //    client.BaseAddress = new Uri("https://api.openai.com/");
        //    client.Timeout = Timeout.InfiniteTimeSpan; 

        //    client.DefaultRequestHeaders.Authorization =
        //        new AuthenticationHeaderValue("Bearer", options.Value.OpenAiKey);
        //})
        //.AddStandardResilienceHandler()
        //.Configure(options =>
        //{
        //    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
        //    options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);

        //    options.Retry.MaxRetryAttempts = 3;
        //    options.Retry.Delay = TimeSpan.FromSeconds(5);
        //    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        //    options.Retry.UseJitter = true; // zabezpieczenie przed "retry storm"

        //    options.CircuitBreaker.FailureRatio = 0.5;
        //    options.CircuitBreaker.MinimumThroughput = 10;
        //    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(10);
        //    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        //});

        return services;
    }
}