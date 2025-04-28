using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.AI;
using Zonit.Extensions.AI.Abstractions.Options;
using Zonit.Extensions.AI.Services;
using Zonit.Extensions.AI.Services.OpenAi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiExtension(this IServiceCollection services)
    {
        services.AddTransient<IImageClient, ImageService>();
        services.AddTransient<OpenAiImageService>();

        services.AddOptions<AiOptions>()
            .Configure<IConfiguration>(
                (options, configuration) =>
                    configuration.GetSection("Ai").Bind(options));

        return services;
    }
}