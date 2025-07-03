using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiExtension(this IServiceCollection services)
    {
        services
            .AddAiApplicationExtension()
            .AddAiInfrastructureExtension();

        return services;
    }
}