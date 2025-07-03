using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Domain.Repositories;
using Zonit.Extensions.Ai.Infrastructure.Repositories.OpenAi;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiInfrastructureExtension(this IServiceCollection services)
    {
        services.AddKeyedTransient<ITextRepository, OpenAiRepository>("OpenAi");
        //services.AddKeyedTransient<IImageRepository, OpenAiImageRepository>("OpenAi");

        services.AddHttpClient();

        services
            .AddHttpClient<OpenAiImageRepository>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>();
                client.BaseAddress = new Uri("https://api.openai.com/");
                client.Timeout = Timeout.InfiniteTimeSpan;

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.Value.OpenAiKey);
            })
            .AddStandardResilienceHandler()
            .Configure(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
                options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);

                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(5);
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true; // zabezpieczenie przed "retry storm"

                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 10;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(10);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            });

        services.AddKeyedTransient<IImageRepository>("OpenAi", (serviceProvider, key) =>
            serviceProvider.GetRequiredService<OpenAiImageRepository>());

        return services;
    }
}