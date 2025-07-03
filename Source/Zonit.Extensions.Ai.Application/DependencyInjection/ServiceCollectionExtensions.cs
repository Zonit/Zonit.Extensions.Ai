using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Application.Options;
using Zonit.Extensions.Ai.Application.Services;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiApplicationExtension(this IServiceCollection services, Action<AiOptions>? options = null)
    {
        services.AddOptions<AiOptions>()
            .Configure<IConfiguration>(
                (options, configuration) =>
                    configuration.GetSection("Ai").Bind(options));

        if (options is not null)
            services.PostConfigure(options);

        services.AddTransient<IAiClient, AiService>();

        return services;
    }
}