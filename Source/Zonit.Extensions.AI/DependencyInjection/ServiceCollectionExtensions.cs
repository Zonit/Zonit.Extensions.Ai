using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddTransient<OpenAiImageService>();

        // Dodajemy HttpClient z odpowiednio dużym timeoutem dla OpenAiImageService
        services.AddHttpClient<OpenAiImageService>()
            .ConfigureHttpClient(client =>
            {
                // Ustawiamy długi timeout dla operacji, które mogą potrwać dłużej
                client.Timeout = TimeSpan.FromMinutes(15);
            });

        return services;
    }
}