using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions.Ai;
using Zonit.Extensions.Ai.Abstractions.Options;
using Zonit.Extensions.Ai.Services;
using Zonit.Extensions.Ai.Services.OpenAi;

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